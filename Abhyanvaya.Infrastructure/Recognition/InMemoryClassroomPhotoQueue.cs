using System.Threading.Channels;
using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Recognition;

/// <summary>In-memory channel queue for classroom photo recognition jobs.</summary>
public sealed class InMemoryClassroomPhotoQueue : IClassroomPhotoQueue
{
    private readonly Channel<ClassroomPhotoMessage> _channel =
        Channel.CreateUnbounded<ClassroomPhotoMessage>(new UnboundedChannelOptions { SingleReader = true });

    private readonly HashSet<Guid> _pendingSessions = [];
    private readonly object _gate = new();
    private int _queuedCount;

    public ValueTask EnqueueAsync(ClassroomPhotoMessage message, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _pendingSessions.Add(message.AttendanceSessionId);
        }

        Interlocked.Increment(ref _queuedCount);
        return _channel.Writer.WriteAsync(message, cancellationToken);
    }

    public async IAsyncEnumerable<ClassroomPhotoMessage> DequeueAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_channel.Reader.TryRead(out var message))
            {
                Interlocked.Decrement(ref _queuedCount);
                yield return message;
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Tracked manually because this channel is created with <c>SingleReader = true</c>, whose
    /// underlying <see cref="ChannelReader{T}"/> does not support <see cref="ChannelReader{T}.Count"/>.
    /// </remarks>
    public int Count => Volatile.Read(ref _queuedCount);

    public bool IsPending(Guid sessionId)
    {
        lock (_gate)
        {
            return _pendingSessions.Contains(sessionId);
        }
    }

    public void MarkCompleted(Guid sessionId)
    {
        lock (_gate)
        {
            _pendingSessions.Remove(sessionId);
        }
    }
}
