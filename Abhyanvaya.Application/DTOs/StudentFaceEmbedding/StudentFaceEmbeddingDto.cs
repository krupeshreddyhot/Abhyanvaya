using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs.StudentFaceEmbedding;

public sealed class StudentFaceEmbeddingDto
{
    public Guid Id { get; set; }

    public int StudentId { get; set; }

    public string EmbeddingModel { get; set; } = null!;

    public string EmbeddingVersion { get; set; } = null!;

    public EmbeddingStatus EmbeddingStatus { get; set; }

    public EmbeddingQuality EmbeddingQuality { get; set; }

    public int EmbeddingDimension { get; set; }

    public long PhotoVersion { get; set; }

    public int RetryCount { get; set; }

    public DateTime? LastFailureUtc { get; set; }

    public string? LastFailureReason { get; set; }

    public string PhotoKey { get; set; } = null!;

    public DateTime GeneratedUtc { get; set; }

    public int? GeneratedBy { get; set; }

    public bool IsActive { get; set; }

    public int VectorDimensions { get; set; }
}
