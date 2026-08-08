using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Domain.Academic;

namespace Abhyanvaya.Application.Academic.Allocation;

public sealed class SectionAllocationContextValidator : ISectionAllocationContextValidator
{
    private readonly IAcademicTelemetryService _telemetry;

    public SectionAllocationContextValidator(IAcademicTelemetryService telemetry) => _telemetry = telemetry;

    public Task<AllocationValidationReport> ValidateAsync(
        SectionAllocationContext context,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.AllocationValidation,
            "AllocationContext.ValidateOnly",
            _ => Task.FromResult(ValidateCore(context)),
            cancellationToken);

    private static AllocationValidationReport ValidateCore(SectionAllocationContext context)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var checks = new List<string>();

        checks.Add("Academic year present");
        if (context.Hierarchy.AcademicYearId <= 0) errors.Add("Missing academic year.");

        checks.Add("Sections present");
        if (context.Sections.Count == 0) errors.Add("Missing sections.");

        checks.Add("Inactive / terminal sections");
        foreach (var s in context.Sections.Where(s =>
                     s.Lifecycle is SectionLifecycleStates.Merged
                         or SectionLifecycleStates.Split
                         or SectionLifecycleStates.Archived
                         or SectionLifecycleStates.Closed))
        {
            warnings.Add($"Section {s.SectionCode} lifecycle is {s.Lifecycle}.");
        }

        checks.Add("Capacity");
        foreach (var c in context.Capacities.Where(c => c.CurrentStrength > c.MaximumCapacity && c.MaximumCapacity > 0))
            errors.Add($"Section {c.SectionId} over capacity ({c.CurrentStrength}/{c.MaximumCapacity}).");

        checks.Add("Policies");
        if (context.Sections.Count > 0 && context.Policies.Count == 0)
            warnings.Add("No policy lines resolved.");

        checks.Add("Faculty");
        if (context.Sections.Count > 0 && context.FacultyAssignments.Count == 0)
            warnings.Add("No faculty assignments in context.");

        checks.Add("Subjects");
        if (context.SubjectAssignments.Count == 0)
            warnings.Add("No subjects in scope.");

        checks.Add("Rooms / timetable");
        if (context.TimetableStatus != "Mapped")
            warnings.Add($"Timetable status: {context.TimetableStatus}.");

        checks.Add("Students");
        if (context.Students.Count == 0)
            warnings.Add("No students in scope.");

        checks.Add("Checksum");
        if (string.IsNullOrWhiteSpace(context.Checksum))
            errors.Add("Context checksum missing.");

        return new AllocationValidationReport
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Checks = checks,
        };
    }
}
