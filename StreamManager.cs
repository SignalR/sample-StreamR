using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;

public class StreamManager
{
    private readonly ConcurrentDictionary<string, Channel<string>> _streams;

    public StreamManager()
    {
        _streams = new ConcurrentDictionary<string, Channel<string>>();
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
        return _streams.TryAdd(streamName, stream);
    }

    public void RemoveStream(string streamName)
    {
        _streams.TryRemove(streamName, out _);
    }

    public ChannelReader<string> GetStream(string streamName)
    {
        if (!_streams.TryGetValue(streamName, out var channel))
        {
            throw new HubException("stream doesn't exist");
        }
        return channel.Reader;
    }
}