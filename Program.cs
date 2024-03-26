using System.IO.MemoryMappedFiles;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trigger
{
    class TriggerPubisher<T> : IDisposable where T : struct
    {
        public int HeaderSize { get; } = 4;
        public string Name { get; private set; } = string.Empty;
        public uint QueueSize { get; private set; } = 0;
        public int TSize { get; private set; } = 0;
        MemoryMappedFile? _mmf = null;
        MemoryMappedViewAccessor? _accessor = null;
        EventWaitHandle? _evh = null;
        uint _currIndex = 0;
        private TriggerPubisher() { }

        public static TriggerPubisher<T> Create(string name,uint queueSize)
        {
            var t = new TriggerPubisher<T>() {  Name = name, QueueSize = queueSize, TSize = Marshal.SizeOf(typeof(T)) };

            #pragma warning disable CA1416 // supress warning about Windows-only compatability
            t._mmf = MemoryMappedFile.CreateOrOpen(name, t.TSize * queueSize + 4);
            #pragma warning restore CA1416 // Validate platform compatibility
            t._accessor = t._mmf.CreateViewAccessor();
            for (int i =0; i < t._accessor.Capacity;i++)
            {
                t._accessor.Write(i, (byte)0);
            }
            
            t._evh = new EventWaitHandle(false, EventResetMode.AutoReset, $"ev{name}");
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
            long address = HeaderSize + TSize * _currIndex;
            //Console.WriteLine($"Writing to address {address} at currIndex = {_currIndex}");
            _accessor?.Write<T>(address,ref obj);
            _evh?.Set();
            _currIndex = (_currIndex + 1) % QueueSize;
            _accessor?.Write(0, _currIndex);
        }

        public void Dispose()
        {
            _mmf?.Dispose();
            _accessor?.Dispose();
            _evh?.Dispose();
        }
    }

    class TriggerSubscriber<T> : IDisposable where T : struct
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

        public static TriggerSubscriber<T> Create(string name, uint queueSize,Action<T> callback,CancellationToken cancelToken)
        {
            var t = new TriggerSubscriber<T>() { Name = name, QueueSize = queueSize, TSize = Marshal.SizeOf(typeof(T)) };

            #pragma warning disable CA1416 // supress warning about Windows-only compatability
            t._mmf = MemoryMappedFile.CreateOrOpen(name, t.TSize * queueSize + 4);
            #pragma warning restore CA1416 // Validate platform compatibility
            t._accessor = t._mmf.CreateViewAccessor();
            
            t._evh = new EventWaitHandle(false, EventResetMode.AutoReset, $"ev{name}");
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
                        //Console.WriteLine($"Reading from address {address} at currIndex = {currIndex}");
                        t._accessor.Read<T>(address, out T obj);
                        t._callback.Invoke(obj);
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

        //public static void StructToByteArray<T>(T strct, byte[] buf) where T : struct
        //{
        //    int size = Marshal.SizeOf(strct);
        //    IntPtr ptr = Marshal.AllocHGlobal(size);
        //    Marshal.StructureToPtr(strct, ptr, true);
        //    Marshal.Copy(ptr, buf, 0, size);
        //    Marshal.FreeHGlobal(ptr);
        //}
    }




    struct PayloadType
    { 
        public long Value { get; set; }
    }


    internal class Program
    {
        static void OnTrigger(PayloadType obj)
        {
            DateTime dtNow = DateTime.Now;
            DateTime dt = DateTime.FromFileTime(obj.Value);
            TimeSpan latency = dtNow - dt;
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} Got msg with latency of {latency.TotalMicroseconds}[microsec]");
        }

        static void Main(string[] args)
        {
            string triggerName = Guid.NewGuid().ToString();
            using var tp = TriggerPubisher<PayloadType>.Create(triggerName, 10);
            CancellationTokenSource cts = new CancellationTokenSource();
            using var tc1 = TriggerSubscriber<PayloadType>.Create(triggerName, 10, OnTrigger, cts.Token);
            //using var tc2 = TriggerSubscriber<PayloadType>.Create(triggerName, 10, OnTrigger, cts.Token);
            //using var tc3 = TriggerSubscriber<PayloadType>.Create(triggerName, 10, OnTrigger, cts.Token);

            Task.Run(() =>
            {
                do
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Escape)
                    {
                        cts.Cancel();
                    }

                } while (!cts.IsCancellationRequested);
            });

            do
            {
                var obj = new PayloadType() { Value = DateTime.Now.ToFileTime() };
                //Console.WriteLine("Publishing " + JsonSerializer.Serialize(obj));
                tp.Set(obj);
                Thread.Sleep(10);
            } while (!cts.IsCancellationRequested);

        }
    }
}
