namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>
/// AI29.1D Prompt 10A — Validates population + target section selection against
/// <see cref="SectionAllocationContext"/> only. No repository access.
/// </summary>
public static class AllocationScopeSelectionValidator
{
    public static AllocationScopeSelectionValidation Validate(
        SectionAllocationContext context,
        AllocationPipelineConfig config)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<string>();
        var population = (config.PopulationSelection ?? AllocationPopulationSelection.AllEligible).Normalize();
        var students = context.Students ?? [];
        var sections = context.Sections ?? [];
        var contextStudentIds = students.Select(s => s.StudentId).ToHashSet();
        var contextSectionIds = sections.Select(s => s.SectionId).ToHashSet();

        IReadOnlyList<int> resolvedStudents;
        string populationSummary;

        var mode = population.Mode;
        if (!AllocationPopulationModes.All.Contains(mode, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(mode, AllocationPopulationModes.AllEligible, StringComparison.OrdinalIgnoreCase))
        {
            // Allow exact known modes only.
            if (!AllocationPopulationModes.All.Any(m => string.Equals(m, mode, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"Unknown population selection mode '{mode}'.");
        }

        if (string.Equals(mode, AllocationPopulationModes.AllEligible, StringComparison.OrdinalIgnoreCase))
        {
            resolvedStudents = students.Select(s => s.StudentId).OrderBy(x => x).ToList();
            populationSummary = "All eligible students";
        }
        else if (string.Equals(mode, AllocationPopulationModes.StudentNumberRange, StringComparison.OrdinalIgnoreCase))
        {
            var from = population.FromStudentNumber ?? "";
            var to = population.ToStudentNumber ?? "";
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                errors.Add("Student number range requires both FromStudentNumber and ToStudentNumber.");
            else if (CompareStudentNumbers(from, to) > 0)
                errors.Add("Invalid student number range: From must be less than or equal to To (ordinal ignore-case).");

            resolvedStudents = students
                .Where(s => IsInRange(s.StudentNumber, from, to))
                .Select(s => s.StudentId)
                .OrderBy(x => x)
                .ToList();
            populationSummary = $"Student number range {from}–{to}";
        }
        else if (string.Equals(mode, AllocationPopulationModes.LastThreeDigitsRange, StringComparison.OrdinalIgnoreCase))
        {
            var fromOk = LastThreeDigitsSemantics.TryParseBound(population.FromStudentNumber, out var fromVal, out var fromNorm, out var fromErr);
            var toOk = LastThreeDigitsSemantics.TryParseBound(population.ToStudentNumber, out var toVal, out var toNorm, out var toErr);
            if (!fromOk)
                errors.Add(fromErr ?? "Invalid Last 3 Digits From value.");
            if (!toOk)
                errors.Add(toErr ?? "Invalid Last 3 Digits To value.");
            if (fromOk && toOk && fromVal > toVal)
                errors.Add("Invalid Last 3 Digits range: From must be less than or equal to To (000–999).");

            if (fromOk && toOk && fromVal <= toVal)
            {
                resolvedStudents = students
                    .Where(s =>
                        LastThreeDigitsSemantics.TryExtractLastThree(s.StudentNumber, out var last3)
                        && last3 >= fromVal
                        && last3 <= toVal)
                    .Select(s => s.StudentId)
                    .OrderBy(x => x)
                    .ToList();
                populationSummary = $"Last 3 Digits range {fromNorm}–{toNorm}";
            }
            else
            {
                resolvedStudents = [];
                populationSummary = "Last 3 Digits range (invalid)";
            }
        }
        else if (string.Equals(mode, AllocationPopulationModes.StudentIds, StringComparison.OrdinalIgnoreCase))
        {
            var ids = population.StudentIds ?? [];
            if (ids.Count == 0)
                errors.Add("StudentIds mode requires at least one student id.");
            foreach (var id in ids)
            {
                if (!contextStudentIds.Contains(id))
                    errors.Add($"Student id {id} is not present in the Allocation Context and cannot be injected.");
            }

            resolvedStudents = ids.Where(contextStudentIds.Contains).Distinct().OrderBy(x => x).ToList();
            populationSummary = $"Explicit student ids ({resolvedStudents.Count})";
        }
        else if (AllocationPopulationModes.IsFacetMode(mode))
        {
            var facet = population.FacetValue;
            if (string.IsNullOrWhiteSpace(facet))
                errors.Add($"Population mode '{mode}' requires FacetValue.");

            var available = DistinctFacetValues(students, mode);
            if (available.Count == 0)
            {
                errors.Add(
                    $"Population mode '{mode}' is unavailable: Allocation Context does not contain authoritative facet values for this criterion.");
            }
            else if (!string.IsNullOrWhiteSpace(facet)
                     && !available.Any(v => string.Equals(v, facet, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"Facet value '{facet}' for '{mode}' does not exist in the Allocation Context.");
            }

            resolvedStudents = students
                .Where(s => FacetMatches(s, mode, facet))
                .Select(s => s.StudentId)
                .OrderBy(x => x)
                .ToList();
            populationSummary = $"{mode} = {facet}";
        }
        else
        {
            resolvedStudents = students.Select(s => s.StudentId).OrderBy(x => x).ToList();
            populationSummary = "All eligible students";
        }

        // Target sections
        IReadOnlyList<int> resolvedSections;
        string targetSummary;
        var targets = config.TargetSectionIds;
        if (targets is null || targets.Count == 0)
        {
            resolvedSections = sections.Select(s => s.SectionId).OrderBy(x => x).ToList();
            targetSummary = "All eligible sections";
        }
        else
        {
            var normalizedTargets = targets.Distinct().OrderBy(x => x).ToList();
            foreach (var id in normalizedTargets)
            {
                if (!contextSectionIds.Contains(id))
                    errors.Add($"Target section id {id} is not present in the Allocation Context.");
            }

            resolvedSections = normalizedTargets.Where(contextSectionIds.Contains).ToList();
            if (resolvedSections.Count == 0 && errors.Count == 0)
                errors.Add("No valid target sections remain after filtering against Allocation Context.");

            var codes = sections
                .Where(s => resolvedSections.Contains(s.SectionId))
                .OrderBy(s => s.SectionCode, StringComparer.OrdinalIgnoreCase)
                .Select(s => s.SectionCode);
            targetSummary = "Selected sections: " + string.Join(", ", codes);
        }

        return new AllocationScopeSelectionValidation
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            ResolvedStudentIds = resolvedStudents,
            ResolvedSectionIds = resolvedSections,
            PopulationSummary = populationSummary,
            TargetSectionsSummary = targetSummary,
        };
    }

    public static void ValidateOrThrow(SectionAllocationContext context, AllocationPipelineConfig config)
    {
        var result = Validate(context, config);
        if (!result.IsValid)
            throw new ArgumentException(string.Join(" ", result.Errors));
    }

    public static int CompareStudentNumbers(string? a, string? b)
    {
        var left = (a ?? "").Trim().ToUpperInvariant();
        var right = (b ?? "").Trim().ToUpperInvariant();
        return string.CompareOrdinal(left, right);
    }

    private static bool IsInRange(string? studentNumber, string from, string to)
    {
        var n = (studentNumber ?? "").Trim();
        if (n.Length == 0) return false;
        return CompareStudentNumbers(from, n) <= 0 && CompareStudentNumbers(n, to) <= 0;
    }

    private static string? ReadFacet(AllocationStudentProjection s, string mode) => mode switch
    {
        AllocationPopulationModes.Gender => s.Gender,
        AllocationPopulationModes.ScholarshipCategory => s.ScholarshipCategory,
        AllocationPopulationModes.MinorSubject => s.MinorSubject,
        AllocationPopulationModes.Language => s.Language,
        AllocationPopulationModes.TransportRoute => s.TransportRoute,
        AllocationPopulationModes.Hostel => s.Hostel,
        AllocationPopulationModes.ElectiveCombination => s.ElectiveCombination,
        AllocationPopulationModes.Merit => s.Merit,
        _ => null,
    };

    private static bool FacetMatches(AllocationStudentProjection s, string mode, string? facet)
    {
        if (string.IsNullOrWhiteSpace(facet)) return false;
        var value = ReadFacet(s, mode);
        return !string.IsNullOrWhiteSpace(value)
               && string.Equals(value.Trim(), facet.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> DistinctFacetValues(
        IReadOnlyList<AllocationStudentProjection> students,
        string mode)
    {
        return students
            .Select(s => ReadFacet(s, mode)?.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Facet readiness for UI: Available / Unavailable / PartiallyAvailable.</summary>
    public static string FacetReadiness(
        IReadOnlyList<AllocationStudentProjection> students,
        string mode)
    {
        if (!AllocationPopulationModes.IsFacetMode(mode))
            return "Available";
        var withValue = 0;
        foreach (var s in students)
        {
            if (!string.IsNullOrWhiteSpace(ReadFacet(s, mode)))
                withValue++;
        }

        if (withValue == 0) return "Unavailable";
        if (withValue < students.Count) return "PartiallyAvailable";
        return "Available";
    }
}
