using System.Diagnostics;
using System.Threading.Channels;

var channel = Channel.CreateUnbounded<string>();

var producer = Task.Run(async () =>
{
    while (true)
    {
        var input = Console.ReadLine();
        if (input == "exit") break;
        await channel.Writer.WriteAsync(input);
    }
    await channel.Writer.WriteAsync("exit");
    channel.Writer.Complete();
    if (channel.Writer.TryWrite("Another Message"))
    {
        Console.WriteLine("Another Message");
    }
    else
    {
        Console.WriteLine("Channel is closed");
    }
});

var consumer = Task.Run(async () =>
{
    await foreach (var msg in channel.Reader.ReadAllAsync())
    {
        Console.WriteLine($"Received: {msg}");
    }
});

await Task.WhenAll(producer, consumer);