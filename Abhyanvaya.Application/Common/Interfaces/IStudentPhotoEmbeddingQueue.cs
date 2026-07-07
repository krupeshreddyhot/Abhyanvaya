namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Queue for background face-embedding jobs after a student photo is uploaded.
/// In-memory channel today; replaceable with Hangfire/Quartz in future phases.
/// </summary>
public interface IStudentPhotoEmbeddingQueue
{
    ValueTask EnqueueAsync(StudentPhotoUploadedMessage message, CancellationToken cancellationToken = default);

    IAsyncEnumerable<StudentPhotoUploadedMessage> DequeueAllAsync(CancellationToken cancellationToken);

    bool IsPending(int studentId);

    void MarkCompleted(int studentId);

    /// <summary>Number of jobs currently buffered and not yet dequeued by the worker.</summary>
    int Count { get; }
}

/// <summary>Background job payload when a student photo is uploaded or regeneration is requested.</summary>
public sealed record StudentPhotoUploadedMessage(
    int TenantId,
    int StudentId,
    string PhotoKey,
    int? RequestedByUserId,
    DateTime EnqueuedUtc,
    bool Regenerate = false);
