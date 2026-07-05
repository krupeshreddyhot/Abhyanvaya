using System.Collections.Concurrent;
using System.Threading.Channels;
using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Embedding;

/// <summary>
/// In-process channel queue for student photo embedding jobs.
/// Replace with Hangfire/Quartz-backed queue in a future phase.
/// </summary>
public sealed class InMemoryStudentPhotoEmbeddingQueue : IStudentPhotoEmbeddingQueue
{
    private readonly Channel<StudentPhotoUploadedMessage> _channel =
        Channel.CreateUnbounded<StudentPhotoUploadedMessage>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

    private readonly ConcurrentDictionary<int, byte> _pendingStudents = new();
    private int _queuedCount;

    public ValueTask EnqueueAsync(StudentPhotoUploadedMessage message, CancellationToken cancellationToken = default)
    {
        _pendingStudents[message.StudentId] = 0;
        Interlocked.Increment(ref _queuedCount);
        return _channel.Writer.WriteAsync(message, cancellationToken);
    }

    public async IAsyncEnumerable<StudentPhotoUploadedMessage> DequeueAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _queuedCount);
            yield return message;
        }
    }

    /// <inheritdoc />
    /// <remarks>Tracked manually so queue depth is available regardless of channel configuration.</remarks>
    public int Count => Volatile.Read(ref _queuedCount);

    public bool IsPending(int studentId) => _pendingStudents.ContainsKey(studentId);

    void IStudentPhotoEmbeddingQueue.MarkCompleted(int studentId) =>
        _pendingStudents.TryRemove(studentId, out _);
}
