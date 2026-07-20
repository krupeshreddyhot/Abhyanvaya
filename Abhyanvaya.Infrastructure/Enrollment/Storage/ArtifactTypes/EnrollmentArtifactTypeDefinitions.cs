using System.Text.Json;
using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Application.Enrollment.Validation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage.ArtifactTypes;

internal sealed class AlignedFaceArtifactTypeDefinition : IEnrollmentArtifactTypeDefinition
{
    public string ArtifactType => EnrollmentArtifactTypeNames.AlignedFace;

    public bool EnabledByDefault => true;

    public string FileExtension => ".webp";

    public string ContentType => "image/webp";

    public bool IsPrimary => true;

    public async Task<EnrollmentArtifactPayload?> TryCreatePayloadAsync(
        EnrollmentValidationArtifact artifact,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (artifact.AlignedFaceImage is { Length: > 0 })
        {
            return new EnrollmentArtifactPayload
            {
                Bytes = artifact.AlignedFaceImage,
                ImageWidth = artifact.Report.FaceWidth ?? 112,
                ImageHeight = artifact.Report.FaceHeight ?? 112,
            };
        }

        var sourceBytes = artifact.SourcePhotoImage ?? artifact.DiagnosticImages?.OriginalImage;
        if (sourceBytes is not { Length: > 0 })
        {
            return null;
        }

        var webpBytes = await ConvertToWebpAsync(sourceBytes, cancellationToken);
        return new EnrollmentArtifactPayload
        {
            Bytes = webpBytes,
            ImageWidth = artifact.Report.SourceWidth ?? 0,
            ImageHeight = artifact.Report.SourceHeight ?? 0,
        };
    }

    private static async Task<byte[]> ConvertToWebpAsync(byte[] sourceBytes, CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync(new MemoryStream(sourceBytes, writable: false), cancellationToken);
        await using var output = new MemoryStream();
        await image.SaveAsWebpAsync(output, cancellationToken);
        return output.ToArray();
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
