using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

public class StreamManager
{
    private class StreamHolder
    {
        public Channel<string> Source;
        public List<Channel<string>> Viewers = new List<Channel<string>>();
        public SemaphoreSlim Lock = new SemaphoreSlim(1, 1);
    }

    private readonly ConcurrentDictionary<string, StreamHolder> _streams;

    public StreamManager()
    {
        _streams = new ConcurrentDictionary<string, StreamHolder>();
    }

    public List<string> ListStreams()
    {
        // Ewww, but it should only be called once per client, we'll send stream additions and deletions to clients individually
        var streams = _streams.ToArray();
        var streamList = new List<string>(streams.Length);
        foreach (var item in streams)
        {
            streamList.Add(item.Key);
        }
        return streamList;
    }

    public bool AddStream(string streamName, Channel<string> stream)
    {
        var streamHolder = new StreamHolder() { Source = stream };

        _ = Task.Run(async () =>
        {
            while (await stream.Reader.WaitToReadAsync())
            {
                while (stream.Reader.TryRead(out var item))
                {
                    await streamHolder.Lock.WaitAsync();
                    try
                    {
                        foreach (var viewer in streamHolder.Viewers)
                        {
                            try
                            {
                                await viewer.Writer.WriteAsync(item);
                            }
                            catch { }
                        }
                    }
                    finally
                    {
                        streamHolder.Lock.Release();
                    }
                }
            }
        });

        return _streams.TryAdd(streamName, streamHolder);
    }

    public void RemoveStream(string streamName)
    {
        _streams.TryRemove(streamName, out var streamHolder);
        foreach (var viewer in streamHolder.Viewers)
        {
            viewer.Writer.TryComplete();
        }
    }

    public ChannelReader<string> GetStream(string streamName, CancellationToken token)
    {
        if (!_streams.TryGetValue(streamName, out var source))
        {
            throw new HubException("stream doesn't exist");
        }

        var channel = Channel.CreateBounded<string>(2);
        source.Lock.Wait();
        try
        {
            source.Viewers.Add(channel);
        }
        finally
        {
            source.Lock.Release();
        }

        token.Register((s) =>
        {
           var streamHolder = s as StreamHolder;
           streamHolder.Lock.Wait();
           try
           {
               streamHolder.Viewers.Remove(channel);
           }
           finally
           {
               streamHolder.Lock.Release();
           }
        }, source);

        return channel.Reader;
    }
}