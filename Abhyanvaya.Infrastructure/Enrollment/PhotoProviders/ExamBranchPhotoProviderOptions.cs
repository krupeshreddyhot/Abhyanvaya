namespace Abhyanvaya.Infrastructure.Enrollment.PhotoProviders;

/// <summary>
/// Configuration for <see cref="ExamBranchPhotoProvider"/>. Bound from <c>StudentPhotoProvider:ExamBranch</c>.
/// Nothing about the source host, path shape, or file extension is hardcoded — see
/// docs/AI20_PHOTO_IMPORT.md §1 for why the whole URL shape is a single configurable template.
/// </summary>
public sealed class ExamBranchPhotoProviderOptions
{
    public const string SectionName = "StudentPhotoProvider:ExamBranch";

    /// <summary>
    /// URL template with <c>{collegeCode}</c>, <c>{academicYear}</c>, and <c>{studentNumber}</c> placeholders.
    /// Example: <c>https://exambranch.com/PHOTOS/{collegeCode}/{academicYear}/{studentNumber}.jpg</c>.
    /// </summary>
    public string BaseUrlTemplate { get; set; } = string.Empty;

    /// <summary>Per-request HTTP timeout. Bounds a single download attempt, not the whole retry sequence.</summary>
    public int TimeoutSeconds { get; set; } = 15;
}
