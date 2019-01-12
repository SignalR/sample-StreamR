using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

public class StreamHub : Hub
{
    public List<string> ListStreams()
    {
        return new List<string>() { "test" };
    }

    public ChannelReader<byte[]> WatchStream(string streamName)
    {
        // TODO:
        // Find stream
        // Allow client to stop watching a stream, or is that automatic if they cancel on the client (double check this)
        var channel = Channel.CreateUnbounded<byte[]>();
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000);
            channel.Writer.Complete();
        });
        return channel.Reader;
    }

    public async Task Stream(string streamName, ChannelReader<byte[]> streamContent)
    {
        // TODO:
        // Check if someone already has that stream name
        // Only allow each client to stream one at a time
        // Pass stream through to watchers

        await streamContent.WaitToReadAsync();
    }
}