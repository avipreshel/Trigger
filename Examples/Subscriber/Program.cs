using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Trigger;

namespace Publisher
{
    struct SomeDTO
    {
        public long Value { get; set; }
    }

    internal class Program
    {
        static void OnTrigger(SomeDTO obj)
        {
            DateTime dtNow = DateTime.Now;
            DateTime dt = DateTime.FromFileTime(obj.Value);
            TimeSpan latency = dtNow - dt;
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} Got msg with latency of {latency.TotalMicroseconds}[microsec]");
        }

        static void Main(string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            using var subscriber = TriggerSubscriber<SomeDTO>.Create("SomeTriggerName", 10, OnTrigger, cts.Token);

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

            cts.Token.WaitHandle.WaitOne();

        }
    }
}
