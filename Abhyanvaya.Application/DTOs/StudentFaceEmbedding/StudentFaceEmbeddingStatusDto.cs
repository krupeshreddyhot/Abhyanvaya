using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs.StudentFaceEmbedding;

public sealed class StudentFaceEmbeddingStatusDto
{
    public int StudentId { get; set; }

    public bool HasPhoto { get; set; }

    public bool HasActiveEmbedding { get; set; }

    public EmbeddingStatus? ActiveStatus { get; set; }

    public EmbeddingQuality? ActiveQuality { get; set; }

    public string? ActiveModel { get; set; }

    public string? ActiveVersion { get; set; }

    public int? ActiveDimension { get; set; }

    public long? ActivePhotoVersion { get; set; }

    public long? CurrentPhotoVersion { get; set; }

    public bool IsPhotoVersionStale { get; set; }

    public DateTime? GeneratedUtc { get; set; }

    public bool GenerationPending { get; set; }

    public int TotalEmbeddings { get; set; }

    public int RetryCount { get; set; }

    public Guid? ActiveEmbeddingId { get; set; }
}
