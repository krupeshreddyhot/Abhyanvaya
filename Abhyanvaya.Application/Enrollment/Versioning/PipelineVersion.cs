namespace Abhyanvaya.Application.Enrollment.Versioning;

/// <summary>
/// Monotonic identity of one whole enrollment pipeline configuration. A batch is pinned to exactly
/// one version for its entire lifetime (docs/AI20_PHASE2_PIPELINE_VERSIONING.md).
/// </summary>
public readonly record struct PipelineVersion(int Value) : IComparable<PipelineVersion>
{
    public int CompareTo(PipelineVersion other) => Value.CompareTo(other.Value);

    public override string ToString() => $"v{Value}";
}
