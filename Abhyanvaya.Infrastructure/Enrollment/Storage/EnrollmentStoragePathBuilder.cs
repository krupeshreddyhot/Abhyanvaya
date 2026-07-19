using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Application;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage;

internal static class EnrollmentStoragePathBuilder
{
    internal static string BuildObjectKey(EnrollmentStoragePathContext context) =>
        $"enrollment/{context.TenantId}/{context.CollegeId}/{context.AcademicYear}/{context.StudentId}/v{context.PipelineVersion}/a{context.ArtifactVersion}/{context.ArtifactType}{context.FileExtension}";

    internal static string BuildCanonicalPhotoKey(int tenantId, int studentId) =>
        StudentMediaPaths.BuildStoragePath(tenantId, studentId);
}
