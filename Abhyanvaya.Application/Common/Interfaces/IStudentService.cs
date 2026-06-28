using Abhyanvaya.Application.DTOs.Student;

namespace Abhyanvaya.Application.Common.Interfaces
{
    public interface IStudentService
    {
        Task<UploadStudentsResultDto> UploadStudentsAsync(
            Stream fileStream,
            int tenantId,
            CancellationToken cancellationToken = default);

        string BuildPhotoStoragePath(int tenantId, int studentId);

        string? BuildPhotoVariantPath(
            string? photoKey,
            DateTime? photoUploadedUtc,
            string variant,
            string? publicBaseUrl = null);
    }
}
