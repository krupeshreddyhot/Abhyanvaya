namespace Abhyanvaya.Application.DTOs.Recognition;

/// <summary>Input for face detection on a raster image.</summary>
public sealed record FaceDetectionRequest(
    byte[] ImageBytes,
    int? MaxFaces = null);
