namespace Abhyanvaya.Application.Enrollment.Storage;

/// <summary>Known enrollment storage pipeline step categories (AI20.PHASE2.1.5G).</summary>
public static class StorageStepCategory
{
    public const string Validation = "Validation";
    public const string Preparation = "Preparation";
    public const string Checksum = "Checksum";
    public const string Compression = "Compression";
    public const string Encryption = "Encryption";
    public const string Upload = "Upload";
    public const string Metadata = "Metadata";
    public const string Manifest = "Manifest";
    public const string Rollback = "Rollback";

    // Reserved for future pipeline extensions.
    public const string Audit = "Audit";
    public const string Replication = "Replication";
    public const string Archive = "Archive";

    private static readonly HashSet<string> KnownCategories = new(StringComparer.Ordinal)
    {
        Validation,
        Preparation,
        Checksum,
        Compression,
        Encryption,
        Upload,
        Metadata,
        Manifest,
        Rollback,
        Audit,
        Replication,
        Archive,
    };

    public static IReadOnlyCollection<string> All => KnownCategories;

    public static bool IsKnown(string category) =>
        !string.IsNullOrWhiteSpace(category) && KnownCategories.Contains(category);
}
