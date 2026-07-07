using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Models;

/// <summary>
/// Immutable point-in-time capture of AI recognition evidence materialized into
/// <see cref="Entities.AttendanceDetail.RecognitionSnapshotJson"/>.
/// </summary>
public sealed class RecognitionSnapshot
{
    public Guid RecognitionId { get; init; }

    public RecognitionStatus RecognitionStatus { get; init; }

    public int? StudentId { get; init; }

    public string? StudentName { get; init; }

    public decimal? ConfidenceScore { get; init; }

    public decimal? EmbeddingDistance { get; init; }

    public int BoundingBoxX { get; init; }

    public int BoundingBoxY { get; init; }

    public int BoundingBoxWidth { get; init; }

    public int BoundingBoxHeight { get; init; }

    public string? RecognitionProvider { get; init; }

    public string? RecognitionModel { get; init; }

    public DateTime RecognitionTimestamp { get; init; }
}
