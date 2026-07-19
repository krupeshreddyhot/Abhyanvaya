namespace Abhyanvaya.Application.Enrollment.Storage;

/// <summary>Self-describing metadata for a single enrollment storage pipeline step.</summary>
public sealed record StorageStepMetadata(
    string Name,
    string Category,
    string Version,
    int Order,
    bool SupportsRollback,
    bool Optional,
    string? FeatureFlag,
    string Description);
