using Abhyanvaya.Application.DTOs.StudentFaceEmbedding;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application;

/// <summary>Maps <see cref="StudentFaceEmbedding"/> entities to DTOs.</summary>
public static class EmbeddingStorageMapper
{
    public static StudentFaceEmbeddingDto MapToDto(StudentFaceEmbedding embedding) =>
        new()
        {
            Id = embedding.Id,
            StudentId = embedding.StudentId,
            EmbeddingModel = embedding.EmbeddingModel,
            EmbeddingVersion = embedding.EmbeddingVersion,
            EmbeddingStatus = embedding.EmbeddingStatus,
            EmbeddingQuality = embedding.EmbeddingQuality,
            EmbeddingDimension = embedding.EmbeddingDimension,
            PhotoVersion = embedding.PhotoVersion,
            RetryCount = embedding.RetryCount,
            LastFailureUtc = embedding.LastFailureUtc,
            LastFailureReason = embedding.LastFailureReason,
            PhotoKey = embedding.PhotoKey,
            GeneratedUtc = embedding.GeneratedUtc,
            GeneratedBy = embedding.GeneratedBy,
            IsActive = embedding.IsActive,
            VectorDimensions = embedding.EmbeddingVector.Length
        };
}
