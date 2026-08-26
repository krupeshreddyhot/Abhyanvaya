using Abhyanvaya.Application.Academic.ReadModels;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1D Prompt 16A — operational breadcrumb compose result (validation + trail).</summary>
public sealed record AcademicOperationalBreadcrumbOutcome(
    AcademicBreadcrumb Breadcrumb,
    bool IsValid,
    string? Error)
{
    public static AcademicOperationalBreadcrumbOutcome Valid(AcademicBreadcrumb breadcrumb)
        => new(breadcrumb, true, null);

    public static AcademicOperationalBreadcrumbOutcome Invalid(string error)
        => new(new AcademicBreadcrumb([]), false, error);
}
