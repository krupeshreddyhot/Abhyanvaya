using System.Text.Json;
using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Application.Enrollment.Validation;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage.ArtifactTypes;

internal sealed class AlignedFaceArtifactTypeDefinition : IEnrollmentArtifactTypeDefinition
{
    public string ArtifactType => EnrollmentArtifactTypeNames.AlignedFace;

    public bool EnabledByDefault => true;

    public string FileExtension => ".webp";

    public string ContentType => "image/webp";

    public bool IsPrimary => true;

    public Task<EnrollmentArtifactPayload?> TryCreatePayloadAsync(
        EnrollmentValidationArtifact artifact,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (artifact.AlignedFaceImage is null || artifact.AlignedFaceImage.Length == 0)
        {
            return Task.FromResult<EnrollmentArtifactPayload?>(null);
        }

        return Task.FromResult<EnrollmentArtifactPayload?>(new EnrollmentArtifactPayload
        {
            Bytes = artifact.AlignedFaceImage,
            ImageWidth = artifact.Report.FaceWidth ?? 112,
            ImageHeight = artifact.Report.FaceHeight ?? 112,
        });
    }
}

internal sealed class ValidationReportArtifactTypeDefinition : IEnrollmentArtifactTypeDefinition
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public string ArtifactType => EnrollmentArtifactTypeNames.ValidationReport;

    public bool EnabledByDefault => true;

    public string FileExtension => ".json";

    public string ContentType => "application/json";

    public bool IsPrimary => false;

    public Task<EnrollmentArtifactPayload?> TryCreatePayloadAsync(
        EnrollmentValidationArtifact artifact,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(artifact.Report, JsonOptions);
        return Task.FromResult<EnrollmentArtifactPayload?>(new EnrollmentArtifactPayload
        {
            Bytes = bytes,
            ImageWidth = artifact.Report.SourceWidth,
            ImageHeight = artifact.Report.SourceHeight,
        });
    }
}
