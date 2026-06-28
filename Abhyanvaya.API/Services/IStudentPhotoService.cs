using Abhyanvaya.Application.DTOs.Student;

namespace Abhyanvaya.API.Services;

/// <summary>Commands and queries for student photo WebP variants (original 800px, thumbnail 200px).</summary>
public interface IStudentPhotoService
{
    Task<(bool Ok, string? Error, StudentPhotoUploadResult? Result)> UploadPhotoAsync(
        int tenantId,
        int studentId,
        IFormFile file,
        CancellationToken cancellationToken);

    Task<(bool Ok, string? Error)> DeletePhotoAsync(
        int tenantId,
        int studentId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns photo metadata for a student. <paramref name="tenantId"/> is null for SuperAdmin (cross-tenant).
    /// </summary>
    /// <returns>Null when the student does not exist.</returns>
    Task<StudentPhotoDto?> GetPhotoAsync(
        int studentId,
        int? tenantId,
        CancellationToken cancellationToken);
}
