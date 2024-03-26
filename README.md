# Trigger

***Trigger*** is the likely the simplest, dummest-yet-fastest [IPC](https://en.wikipedia.org/wiki/Inter-process_communication) framework out there for Dotnet v8.0 and up.

The idea is simple: Take **Named Events**, and add payload upon event arrival. That's it.

If [ZeroMQ](https://zeromq.org) is self proclaimed as *sockets on steroids*, then Trigger can be proclaimed as *Named Events on steroids* :)

*Trigger* basicaly enables to implement a simple pub-sub topology across same process or multiple processes running on the same host OS.

## Why do you need it
It's insanely fast.

Named Events (aka "Named Semaphores" in Linux) are probably the fastest way to signal-across-process that a certain event has happened, 
however, sometime we not only want to signal that something has happened, we're also interested to say WHAT happened. 

Enter Trigger.

## Dependencies
Dotnet 8.0 or later

## Supported OS
* Windows: Yes (10 or later)
* Linux: No (As long as DotNet doesn't provide out-of-the-box API for memory mapped files in Linux)

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

```

## What you should prefer it over NetMQ pub-sub topology
If you're looking for a basic pub-sub topology and you only care about working in local host, then Trigger achieve better performance for FAR LESS boilerplate code compare to ZMQ. 
Also, NetMQ doesn't support named pipes yet (the [GitHub thread](https://github.com/zeromq/netmq/issues/331) is still open since 2015) so Trigger is MUCH faster.

## How does A publisher works
Publisher takes a struct DTO, copies it into a queue (which is located in a shared memory area) and then trigger a named event.
Once the queue is filled, the publisher goes back and overwrites the first item, in a round-robin manner.
It means that **If clients are too slow, they might skip and miss incoming events**

## How does A subscriber works
Subscriber is mapping a named event to a callback. Under the hood there is a fixed thread waiting on the event. 
Once an event is triggered, the thread reads the item from the queue (round robin manner) and then call to the provided callback (provided in the C'tor).
If the callback is not trivial then the best practice is to let another thread handle the work, and "release" the calling thread.

## Limitations
* Works only in Windows (for now)
* Supports only Structs without reference types

# TODO
* Add support for passing Strings and byte array
