namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Queue for background classroom photo recognition jobs.
/// </summary>
public interface IClassroomPhotoQueue
{
    ValueTask EnqueueAsync(ClassroomPhotoMessage message, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ClassroomPhotoMessage> DequeueAllAsync(CancellationToken cancellationToken);

    bool IsPending(Guid sessionId);

    void MarkCompleted(Guid sessionId);

    /// <summary>Number of jobs currently buffered and not yet dequeued by the worker.</summary>
    int Count { get; }
}
