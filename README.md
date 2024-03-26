# Trigger

***Trigger*** is the simplest and fastest IPC (Inter Process Communication) framework out there for dotnet "Core" (Dotnet v8.0 and up).

The idea is simple: Take **Named Events**, and add payload upon event arrival. That's it.

*Trigger* basicaly enables to implement a simple pub-sub topology across same process or multiple processes running on the same host OS.

## Dependencies
Dotnet 8.0 or later

## Supported OS
* Windows: Yes (10 or later)
* Linux: No (At least until DotNet will provide support for memory mapped files in Linux)

## Code Example
```

static void RunPubSub()
{

    // Publish Event
    using var publisher = TriggerPubisher<SomeDTO>.Create("SomeTriggerName", queueSize: 10);

    publisher.Set(new SomeDTO() { Value = 123 });

    CancellationTokenSource cts = new CancellationTokenSource();

    // Register to the event
    Action<SomeDTO> OnTrigger = new Action<SomeDTO>((obj) =>
    {
        Console.WriteLine($"Got {JsonSerializer.Serialize(obj)}");
        cts.Cancel();
    });
    
    using var subscriber = TriggerSubscriber<SomeDTO>.Create("SomeTriggerName", queueSize : 10, OnTrigger, cts.Token);

    cts.Token.WaitHandle.WaitOne();
}
