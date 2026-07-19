namespace Abhyanvaya.Domain.Constants;

/// <summary>
/// Known <see cref="Abhyanvaya.Application.Common.Interfaces.IStudentPhotoProvider"/> identifiers.
/// Only <see cref="ExamBranch"/> has a registered implementation today (AI20.IMPLEMENT.4); the
/// remaining constants name providers the framework is designed to accept without further changes
/// to <c>IStudentPhotoProvider</c>, <c>IStudentPhotoProviderFactory</c>, or any caller — see
/// docs/AI20_PHOTO_IMPORT.md and docs/AI20_ENROLLMENT_ARCHITECTURE.md §4.
/// </summary>
public static class StudentPhotoProviders
{
    /// <summary>HTTP photo host keyed by <c>{collegeCode}/{academicYear}/{studentNumber}</c> (e.g. exambranch.com).</summary>
    public const string ExamBranch = "ExamBranch";

    /// <summary>Future: university/OU-hosted roster photo export.</summary>
    public const string Ou = "OU";

    /// <summary>Future: a CSV/spreadsheet mapping student numbers to photo paths or URLs.</summary>
    public const string Csv = "CSV";

    /// <summary>Future: photos stored in a shared Google Drive folder structure.</summary>
    public const string GoogleDrive = "GoogleDrive";

    /// <summary>Future: photos stored in an Azure Blob container (a different account/container than platform media storage).</summary>
    public const string AzureBlob = "AzureBlob";

    /// <summary>Future: photos stored in a shared OneDrive folder structure.</summary>
    public const string OneDrive = "OneDrive";

    /// <summary>Future: SuperAdmin manually uploads a photo per student instead of an automated fetch.</summary>
    public const string ManualUpload = "ManualUpload";
}
