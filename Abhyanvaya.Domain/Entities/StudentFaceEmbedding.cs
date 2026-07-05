using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// Stores a face-embedding vector generated from a student photo.
/// Multiple rows may exist per student; at most one may be active at a time.
/// </summary>
public class StudentFaceEmbedding : ITenantScoped
{
    public Guid Id { get; set; }

    public int TenantId { get; set; }

    public int StudentId { get; set; }

    /// <summary>512-dimensional (or provider-specific) embedding vector.</summary>
    public float[] EmbeddingVector { get; set; } = [];

    public required string EmbeddingModel { get; set; }

    public required string EmbeddingVersion { get; set; }

    public EmbeddingStatus EmbeddingStatus { get; set; } = EmbeddingStatus.Pending;

    public EmbeddingQuality EmbeddingQuality { get; set; } = EmbeddingQuality.Unknown;

    /// <summary>Length of <see cref="EmbeddingVector"/> (e.g. 512, 768, 1024).</summary>
    public int EmbeddingDimension { get; set; }

    /// <summary>
    /// <see cref="Student.PhotoUploadedUtc"/> ticks at generation time; detects stale embeddings after photo replacement.
    /// </summary>
    public long PhotoVersion { get; set; }

    public int RetryCount { get; set; }

    public DateTime? LastFailureUtc { get; set; }

    public string? LastFailureReason { get; set; }

    /// <summary>Photo storage path used when this embedding was generated.</summary>
    public required string PhotoKey { get; set; }

    public DateTime GeneratedUtc { get; set; }

    public int? GeneratedBy { get; set; }

    /// <summary>When true, this embedding participates in recognition matching.</summary>
    public bool IsActive { get; set; }

    public Student Student { get; set; } = null!;
}
