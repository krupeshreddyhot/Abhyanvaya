namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Validates classroom attendance photos before upload and AI processing.
/// </summary>
public interface IClassroomImageValidator
{
    Task<ClassroomImageValidationResult> ValidateAsync(
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);
}

public sealed class ClassroomImageValidationResult
{
    public bool IsValid { get; init; }

    public string? ErrorMessage { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public static ClassroomImageValidationResult Success(int width, int height) =>
        new() { IsValid = true, Width = width, Height = height };

    public static ClassroomImageValidationResult Failure(string message) =>
        new() { IsValid = false, ErrorMessage = message };
}
