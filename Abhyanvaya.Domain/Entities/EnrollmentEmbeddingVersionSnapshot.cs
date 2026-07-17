using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities;

/// <summary>Immutable version snapshot captured at enrollment embedding persistence time.</summary>
public class EnrollmentEmbeddingVersionSnapshot : ITenantScoped
{
    public Guid Id { get; set; }

    public int TenantId { get; set; }

    public Guid StudentFaceEmbeddingId { get; set; }

    public Guid EnrollmentItemId { get; set; }

    public required string EmbeddingModel { get; set; }

    public required string EmbeddingModelVersion { get; set; }

    public int PipelineVersion { get; set; }

    public int ValidationVersion { get; set; }

    public int StorageVersion { get; set; }

    public int ManifestVersion { get; set; }

    public int ArtifactVersion { get; set; }

    public string? FrameworkVersion { get; set; }

    public string? OnnxVersion { get; set; }

    public Guid CorrelationId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public StudentFaceEmbedding StudentFaceEmbedding { get; set; } = null!;

    public StudentEnrollmentItem EnrollmentItem { get; set; } = null!;
}
