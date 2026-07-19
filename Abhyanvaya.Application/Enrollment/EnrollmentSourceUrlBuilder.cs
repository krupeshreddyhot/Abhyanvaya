namespace Abhyanvaya.Application.Enrollment;

/// <summary>
/// Builds deterministic source photo URLs from a template without invoking photo providers.
/// </summary>
public static class EnrollmentSourceUrlBuilder
{
    public static string Build(
        string baseUrlTemplate,
        string collegeCode,
        int academicYear,
        string studentNumber)
    {
        if (string.IsNullOrWhiteSpace(baseUrlTemplate))
        {
            return string.Empty;
        }

        return baseUrlTemplate
            .Replace("{collegeCode}", Uri.EscapeDataString(collegeCode), StringComparison.OrdinalIgnoreCase)
            .Replace("{academicYear}", academicYear.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{studentNumber}", Uri.EscapeDataString(studentNumber), StringComparison.OrdinalIgnoreCase);
    }
}
