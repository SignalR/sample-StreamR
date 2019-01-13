using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

public class StreamHub : Hub
{
    private readonly StreamManager _streamManager;
    
    public StreamHub(StreamManager streamManager)
    {
        _streamManager = streamManager;
    }

    public List<string> ListStreams()
    {
        return _streamManager.ListStreams();
    }

    public ChannelReader<string> WatchStream(string streamName)
    {
        // TODO:
        // Allow client to stop watching a stream, or is that automatic if they cancel on the client (double check this)

        var stream = _streamManager.GetStream(streamName);
        return stream;
    }

    public async Task StartStream(string streamName, ChannelReader<string> streamContent)
    {
        // TODO:
        // Only allow each client to stream one at a time
        // Pass stream through to watchers

        var channel = Channel.CreateBounded<string>(options: new BoundedChannelOptions(2) {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        //var channel = Channel.CreateUnbounded<string>();

        if (!_streamManager.AddStream(streamName, channel))
        {
            throw new HubException("This stream name has already been taken.");
        }

        try
        {
            // TODO: I didn't think `Context.ConnectionAborted` was needed here... need to check that out
            while (await streamContent.WaitToReadAsync(Context.ConnectionAborted))
            {
                while (streamContent.TryRead(out var content))
                {
                    await channel.Writer.WriteAsync(content);
                }
            }
        }
        catch (Exception exception)
        {
            channel.Writer.Complete(exception);
        }
        finally
        {
            _streamManager.RemoveStream(streamName);
            channel.Writer.Complete();
        }
    }
}