namespace Abhyanvaya.Application.Scheduling.Capacity;

/// <summary>
/// AI-SCHED-CAP Prompt 3 — Which signal supplied PlacementSize for room-fit evaluation.
/// </summary>
public enum PlacementSizeSource : byte
{
    Unset = 0,
    ResolvedStudentCount = 1,
    ExpectedStudentCount = 2,
    SubjectExpectedCapacity = 3,
}

/// <summary>
/// Result of PlacementSize resolution. <see cref="HasValue"/> false means room-fit size cannot be evaluated.
/// </summary>
public readonly record struct PlacementSizeResolution(PlacementSizeSource Source, int Value)
{
    public bool HasValue => Source != PlacementSizeSource.Unset;

    public static PlacementSizeResolution Unset { get; } = new(PlacementSizeSource.Unset, 0);

    public static PlacementSizeResolution From(PlacementSizeSource source, int value) =>
        source == PlacementSizeSource.Unset ? Unset : new(source, value);
}

/// <summary>
/// Authoritative PlacementSize contract (AI-SCHED-CAP Prompt 2/3).
/// Precedence: Resolved (incl. 0) → Expected (&gt;0) → Subject.ExpectedCapacity (&gt;0) → Unset.
/// </summary>
public interface IPlacementSizeResolver
{
    /// <param name="resolvedStudentCount">
    /// null = unavailable (do not treat as zero).
    /// 0 = valid empty roster — must not fall through.
    /// </param>
    /// <param name="expectedStudentCount">null or ≤0 = unset.</param>
    /// <param name="subjectExpectedCapacity">null or ≤0 = unset.</param>
    PlacementSizeResolution Resolve(
        int? resolvedStudentCount,
        int? expectedStudentCount,
        int? subjectExpectedCapacity);
}

/// <summary>Shared PlacementSize implementation — no DB access; no TG inference.</summary>
public sealed class PlacementSizeResolver : IPlacementSizeResolver
{
    public static PlacementSizeResolver Instance { get; } = new();

    public PlacementSizeResolution Resolve(
        int? resolvedStudentCount,
        int? expectedStudentCount,
        int? subjectExpectedCapacity)
    {
        if (resolvedStudentCount is int resolved)
            return PlacementSizeResolution.From(PlacementSizeSource.ResolvedStudentCount, resolved);

        if (expectedStudentCount is int expected && expected > 0)
            return PlacementSizeResolution.From(PlacementSizeSource.ExpectedStudentCount, expected);

        if (subjectExpectedCapacity is int subjectCap && subjectCap > 0)
            return PlacementSizeResolution.From(PlacementSizeSource.SubjectExpectedCapacity, subjectCap);

        return PlacementSizeResolution.Unset;
    }
}
