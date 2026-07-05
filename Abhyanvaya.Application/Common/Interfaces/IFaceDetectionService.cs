using Abhyanvaya.Application.DTOs.Recognition;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Detects faces in an image, aligns them, and extracts embedding vectors.
/// Does not write attendance or recognition records.
/// </summary>
public interface IFaceDetectionService
{
    string ProviderName { get; }

    string ModelName { get; }

    string Version { get; }

    Task<FaceDetectionResponse> DetectAsync(
        FaceDetectionRequest request,
        CancellationToken cancellationToken = default);
}
