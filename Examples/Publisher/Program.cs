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
        static void Main(string[] args)
        {
            using var tp = TriggerPublisher<SomeDTO>.Create("SomeTriggerName", 10);
            CancellationTokenSource cts = new CancellationTokenSource();

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
                var dtNow = DateTime.Now;
                var obj = new SomeDTO() { Value = dtNow.ToFileTime() };
                tp.Set(obj);
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId} sent msg at {dtNow}");
                Thread.Sleep(10);
            } while (!cts.IsCancellationRequested);

        }
    }
}
