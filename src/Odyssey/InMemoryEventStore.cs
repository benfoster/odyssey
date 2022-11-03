namespace Odyssey;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using O9d.Guard;

public class InMemoryEventStore : IEventStore
{
    private static readonly IReadOnlyCollection<EventData> EmptyStream = Array.Empty<EventData>();
    private readonly ConcurrentDictionary<string, List<EventData>> _streams = new();

    public Task Initialize(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task AppendToStream(string streamId, IReadOnlyList<EventData> events, StreamState expectedState, CancellationToken cancellationToken = default)
    {
        streamId.NotNullOrWhiteSpace();
        events.NotNull();

        bool exists = _streams.TryGetValue(streamId, out List<EventData>? stream);

        switch (expectedState)
        {
            case { } when expectedState == StreamState.NoStream:
                if (exists)
                {
                    throw new ConcurrencyException("Stream expectation failed. Stream should not exist");
                }
                break;
            case { } when expectedState == StreamState.StreamExists:
                if (!exists)
                {
                    throw new ConcurrencyException("Stream expectation failed. Stream should exist");
                }
                break;
            case { } when expectedState >= 0:
                if ((stream!.Count - 1) != expectedState)
                {
                    throw new ConcurrencyException($"Stream expectation failed. Expected version {expectedState}. Actual version {stream.Count - 1}.");
                }
                break;
        }

        if (!exists)
        {
            stream = new();
            _streams.TryAdd(streamId, stream);
        }

        stream.NotNull();

        long currentVersion = stream.Count - 1;
        foreach (var @event in events)
        {
            @event.EventNumber = ++currentVersion;
            stream.Add(@event);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<EventData>> ReadStream(string streamId, ReadDirection direction, StreamPosition position, CancellationToken cancellationToken = default)
    {
        streamId.NotNullOrWhiteSpace();

        if (!_streams.ContainsKey(streamId))
        {
            return Task.FromResult(EmptyStream);
        }

        var events = _streams[streamId];

        if (direction == ReadDirection.Backwards)
        {
            var reversed = new List<EventData>(events.Count);
            for (int i = events.Count - 1; i >= 0; i--)
            {
                reversed.Add(events[i]);
            }

            return Task.FromResult<IReadOnlyCollection<EventData>>(reversed.AsReadOnly());
        }

        return Task.FromResult<IReadOnlyCollection<EventData>>(events.AsReadOnly());
    }
}