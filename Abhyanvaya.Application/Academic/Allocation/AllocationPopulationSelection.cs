namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>
/// AI29.1D Prompt 10A — Immutable population selection criteria for the Allocation Engine.
/// Represents selection intent only; never a database query. Resolution is against
/// <see cref="SectionAllocationContext"/> exclusively.
/// </summary>
public sealed class AllocationPopulationSelection
{
    public string Mode { get; init; } = AllocationPopulationModes.AllEligible;

    /// <summary>Inclusive From for <see cref="AllocationPopulationModes.StudentNumberRange"/>.</summary>
    public string? FromStudentNumber { get; init; }

    /// <summary>Inclusive To for <see cref="AllocationPopulationModes.StudentNumberRange"/>.</summary>
    public string? ToStudentNumber { get; init; }

    /// <summary>Explicit student ids for <see cref="AllocationPopulationModes.StudentIds"/>.</summary>
    public IReadOnlyList<int>? StudentIds { get; init; }

    /// <summary>Facet value for Gender / Language / Scholarship / … modes.</summary>
    public string? FacetValue { get; init; }

    public static AllocationPopulationSelection AllEligible { get; } = new()
    {
        Mode = AllocationPopulationModes.AllEligible,
    };

    /// <summary>Deterministic copy with sorted student ids; LastThreeDigitsRange bounds normalized to D3.</summary>
    public AllocationPopulationSelection Normalize()
    {
        IReadOnlyList<int>? ids = null;
        if (StudentIds is { Count: > 0 })
            ids = StudentIds.Distinct().OrderBy(x => x).ToList();

        var mode = string.IsNullOrWhiteSpace(Mode) ? AllocationPopulationModes.AllEligible : Mode.Trim();
        string? from = string.IsNullOrWhiteSpace(FromStudentNumber) ? null : FromStudentNumber.Trim();
        string? to = string.IsNullOrWhiteSpace(ToStudentNumber) ? null : ToStudentNumber.Trim();

        if (string.Equals(mode, AllocationPopulationModes.LastThreeDigitsRange, StringComparison.OrdinalIgnoreCase))
        {
            if (LastThreeDigitsSemantics.TryParseBound(from, out _, out var fromNorm, out _))
                from = fromNorm;
            if (LastThreeDigitsSemantics.TryParseBound(to, out _, out var toNorm, out _))
                to = toNorm;
        }

        return new AllocationPopulationSelection
        {
            Mode = mode,
            FromStudentNumber = from,
            ToStudentNumber = to,
            StudentIds = ids,
            FacetValue = string.IsNullOrWhiteSpace(FacetValue) ? null : FacetValue.Trim(),
        };
    }
}

public static class AllocationPopulationModes
{
    public const string AllEligible = "AllEligible";
    public const string StudentNumberRange = "StudentNumberRange";
    /// <summary>AI29.1D.24B.4 — Inclusive last-three-digit numeric range (000–999), not full StudentNumber ordinal.</summary>
    public const string LastThreeDigitsRange = "LastThreeDigitsRange";
    public const string StudentIds = "StudentIds";
    public const string Gender = "Gender";
    public const string ScholarshipCategory = "ScholarshipCategory";
    public const string MinorSubject = "MinorSubject";
    public const string Language = "Language";
    public const string TransportRoute = "TransportRoute";
    public const string Hostel = "Hostel";
    public const string ElectiveCombination = "ElectiveCombination";
    public const string Merit = "Merit";

    public static IReadOnlyList<string> All { get; } =
    [
        AllEligible, StudentNumberRange, LastThreeDigitsRange, StudentIds, Gender, ScholarshipCategory, MinorSubject,
        Language, TransportRoute, Hostel, ElectiveCombination, Merit,
    ];

    public static bool IsFacetMode(string mode) => mode is
        Gender or ScholarshipCategory or MinorSubject or Language
        or TransportRoute or Hostel or ElectiveCombination or Merit;
}

/// <summary>Result of validating / resolving population + target sections against Allocation Context.</summary>
public sealed class AllocationScopeSelectionValidation
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<int> ResolvedStudentIds { get; init; } = [];
    public IReadOnlyList<int> ResolvedSectionIds { get; init; } = [];
    public string PopulationSummary { get; init; } = "All eligible students";
    public string TargetSectionsSummary { get; init; } = "All eligible sections";
}
