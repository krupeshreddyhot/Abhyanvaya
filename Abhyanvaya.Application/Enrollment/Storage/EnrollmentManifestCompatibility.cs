namespace Abhyanvaya.Application.Enrollment.Storage;

/// <summary>Manifest version compatibility checks for downstream services (AI20.PHASE2.1.5B).</summary>
public static class EnrollmentManifestCompatibility
{
    public const int SupportedManifestVersion = EnrollmentStorageVersions.CurrentManifestVersion;
    public const int SupportedSchemaVersion = EnrollmentStorageVersions.ManifestSchemaVersion;
    public const int SupportedStorageVersion = EnrollmentStorageVersions.StorageSchemaVersion;
    public const int SupportedValidationVersion = EnrollmentStorageVersions.ValidationSchemaVersion;

    public static bool IsCompatible(EnrollmentStorageManifest manifest) =>
        manifest.ManifestVersion <= SupportedManifestVersion
        && manifest.SchemaVersion <= SupportedSchemaVersion
        && manifest.StorageVersion <= SupportedStorageVersion
        && manifest.ValidationVersion <= SupportedValidationVersion;

    public static string? GetIncompatibilityReason(EnrollmentStorageManifest manifest)
    {
        if (manifest.ManifestVersion > SupportedManifestVersion)
        {
            return $"Manifest version {manifest.ManifestVersion} exceeds supported {SupportedManifestVersion}.";
        }

        if (manifest.SchemaVersion > SupportedSchemaVersion)
        {
            return $"Schema version {manifest.SchemaVersion} exceeds supported {SupportedSchemaVersion}.";
        }

        if (manifest.StorageVersion > SupportedStorageVersion)
        {
            return $"Storage version {manifest.StorageVersion} exceeds supported {SupportedStorageVersion}.";
        }

        if (manifest.ValidationVersion > SupportedValidationVersion)
        {
            return $"Validation version {manifest.ValidationVersion} exceeds supported {SupportedValidationVersion}.";
        }

        return null;
    }

    /// <summary>Compatibility matrix for documentation and runtime checks.</summary>
    public static IReadOnlyList<EnrollmentManifestCompatibilityEntry> CompatibilityMatrix { get; } =
    [
        new(SupportedManifestVersion, SupportedSchemaVersion, SupportedStorageVersion, SupportedValidationVersion, "Current resolver and embedding contract"),
        new(1, 1, 1, 1, "AI20.PHASE2.1.5 baseline"),
    ];
}

public sealed record EnrollmentManifestCompatibilityEntry(
    int ManifestVersion,
    int SchemaVersion,
    int StorageVersion,
    int ValidationVersion,
    string Description);
