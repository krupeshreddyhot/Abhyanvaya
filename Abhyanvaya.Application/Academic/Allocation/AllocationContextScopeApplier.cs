namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>
/// AI29.1D Prompt 10A — Applies validated population / target-section selection to produce
/// a filtered Allocation Context view. Uses only <see cref="SectionAllocationContext"/> data.
/// </summary>
public static class AllocationContextScopeApplier
{
    public static SectionAllocationContext Apply(
        SectionAllocationContext source,
        AllocationScopeSelectionValidation selection)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selection);
        if (!selection.IsValid)
            throw new ArgumentException("Cannot apply invalid allocation scope selection.");

        var studentSet = selection.ResolvedStudentIds.ToHashSet();
        var sectionSet = selection.ResolvedSectionIds.ToHashSet();

        var students = source.Students.Where(s => studentSet.Contains(s.StudentId)).ToList();
        var sections = source.Sections.Where(s => sectionSet.Contains(s.SectionId)).ToList();
        var capacities = source.Capacities.Where(c => sectionSet.Contains(c.SectionId)).ToList();
        var faculty = source.FacultyAssignments.Where(f => sectionSet.Contains(f.SectionId)).ToList();

        var metadata = new Dictionary<string, string>(source.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
        {
            ["PopulationSummary"] = selection.PopulationSummary,
            ["TargetSectionsSummary"] = selection.TargetSectionsSummary,
            ["ScopedStudentCount"] = students.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ScopedSectionCount"] = sections.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        return new SectionAllocationContext
        {
            ContextId = source.ContextId,
            ContextVersion = source.ContextVersion,
            SchemaVersion = source.SchemaVersion,
            GeneratedAt = source.GeneratedAt,
            Checksum = source.Checksum,
            Hierarchy = source.Hierarchy,
            Sections = sections,
            Capacities = capacities,
            Students = students,
            FacultyAssignments = faculty,
            SubjectAssignments = source.SubjectAssignments,
            RoomAvailability = source.RoomAvailability,
            Policies = source.Policies,
            Recommendations = source.Recommendations,
            Metadata = metadata,
            OverallHealth = source.OverallHealth,
            OverallReadiness = source.OverallReadiness,
            TimetableStatus = source.TimetableStatus,
        };
    }
}
