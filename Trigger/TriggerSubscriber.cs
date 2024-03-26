using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Trigger
{
    public class TriggerSubscriber<T> : IDisposable where T : struct
    {
        public int HeaderSize { get; } = 4;
        public string Name { get; private set; } = string.Empty;
        public uint QueueSize { get; private set; } = 0;
        public int TSize { get; private set; } = 0;
        MemoryMappedFile? _mmf = null;
        MemoryMappedViewAccessor? _accessor = null;
        EventWaitHandle? _evh = null;
        Action<T>? _callback;
        Thread? _threadDispatcher;
        
        private TriggerSubscriber() { }

        public static TriggerSubscriber<T> Create(string name, uint queueSize, Action<T> callback, CancellationToken cancelToken)
        {
            var t = new TriggerSubscriber<T>() { Name = name, QueueSize = queueSize, TSize = Marshal.SizeOf(typeof(T)) };

#pragma warning disable CA1416 // supress warning about Windows-only compatability
            t._mmf = MemoryMappedFile.CreateOrOpen(name, t.TSize * queueSize + t.HeaderSize);

            t._accessor = t._mmf.CreateViewAccessor();

            t._evh = new EventWaitHandle(false, EventResetMode.AutoReset, $"ev{name}");
#pragma warning restore CA1416 // Validate platform compatibility

            t._callback = callback;
            t._threadDispatcher = new Thread(() =>
            {
                uint currIndex = t._accessor.ReadUInt32(0);
                WaitHandle[] waitHandles = [t._evh, cancelToken.WaitHandle];
                do
                {
                    if (WaitHandle.WaitAny(waitHandles) == 0)
                    {
                        long address = t.HeaderSize + currIndex * t.TSize;
                        t._accessor.Read<T>(address, out T obj);
                        try
                        {
                            t._callback.Invoke(obj);
                        }
                        catch (Exception)
                        {
                            // Client should handle it's own problems
                        }
                        currIndex = (currIndex + 1) % t.QueueSize;
                    }

                } while (!cancelToken.IsCancellationRequested);
            });
            t._threadDispatcher.Start();
            return t;
        }

        public void Dispose()
        {
            _mmf?.Dispose();
            _accessor?.Dispose();
            _evh?.Dispose();
            _threadDispatcher?.Join();
        }
    }
}
