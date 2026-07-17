namespace Abhyanvaya.Domain.Entities;

/// <summary>Immutable audit record for enrollment embedding persistence attempts.</summary>
public class EnrollmentPersistenceAudit
{
    public Guid Id { get; set; }

    public Guid EnrollmentItemId { get; set; }

    public int TenantId { get; set; }

    public int StudentId { get; set; }

    public Guid? EmbeddingId { get; set; }

    public int PipelineVersion { get; set; }

    public int StorageVersion { get; set; }

    public int ValidationVersion { get; set; }

    public required string ModelVersion { get; set; }

    public int? UserId { get; set; }

    public DateTime TimestampUtc { get; set; }

    public Guid CorrelationId { get; set; }

    public required string Outcome { get; set; }

    public string? Detail { get; set; }

    public StudentEnrollmentItem EnrollmentItem { get; set; } = null!;
}
