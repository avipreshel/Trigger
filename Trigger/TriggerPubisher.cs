using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Trigger
{
    /// <summary>
    /// This class will be used to publish DTO with events. It's like Named Event on steroids.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TriggerPublisher<T> : IDisposable where T : struct
    {
        public int HeaderSize { get; } = 4;
        public string Name { get; private set; } = string.Empty;
        public uint QueueSize { get; private set; } = 0;
        public int TSize { get; private set; } = 0;
        MemoryMappedFile? _mmf = null;
        MemoryMappedViewAccessor? _accessor = null;
        EventWaitHandle? _evh = null;
        uint _currIndex = 0;
        private TriggerPublisher() { }

        public static TriggerPublisher<T> Create(string name, uint queueSize)
        {
            var t = new TriggerPublisher<T>() { Name = name, QueueSize = queueSize, TSize = Marshal.SizeOf(typeof(T)) };
            t._currIndex = queueSize - 1; // This will cause the first trigger to be 0-based
#pragma warning disable CA1416 // supress warning about Windows-only compatability
            t._mmf = MemoryMappedFile.CreateOrOpen(name, t.TSize * queueSize + t.HeaderSize);

            t._accessor = t._mmf.CreateViewAccessor();
            for (int i = 0; i < t._accessor.Capacity; i++)
            {
                t._accessor.Write(i, (byte)0);
            }

            t._evh = new EventWaitHandle(false, EventResetMode.AutoReset, $"ev{name}");
#pragma warning restore CA1416 // Validate platform compatibility
            t._evh.Reset();
            return t;
        }

        /// <summary>
        /// Not thread safe! 
        /// If this method is called by client code from multi-thread on the SAME INSTANCE,
        /// then client code must be responsible for locking this instance
        /// </summary>
        /// <param name="obj"></param>
        public void Set(T obj)
        {
            _currIndex = (_currIndex + 1) % QueueSize;
            _accessor?.Write(0, _currIndex);
            long address = HeaderSize + TSize * _currIndex;
            _accessor?.Write<T>(address,ref obj);
            _evh?.Set();
        }

        public void Dispose()
        {
            _mmf?.Dispose();
            _accessor?.Dispose();
            _evh?.Dispose();
        }
    }
}
