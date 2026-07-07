namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Orchestrates face-embedding generation: provider → validate → normalize → store.
/// </summary>
public interface IEmbeddingPipeline
{
    /// <summary>
    /// Runs the full embedding pipeline for a queued student photo job.
    /// </summary>
    Task GenerateAsync(StudentPhotoUploadedMessage message, CancellationToken cancellationToken = default);
}
