namespace Abhyanvaya.API.Diagnostics;

/// <summary>
/// How likely a configuration issue is to actually break something at runtime.
/// This is metadata only — every severity is still just <em>logged</em>; nothing in this
/// enum changes whether startup continues (see <see cref="ConfigurationValidator"/>).
/// </summary>
public enum ConfigurationSeverity
{
    /// <summary>Configuration is valid but could be improved (e.g. relying on a code default, a relative path).</summary>
    Information,

    /// <summary>Configuration works but is not recommended (e.g. a plaintext secret, an unused setting).</summary>
    Warning,

    /// <summary>Configuration is very likely incorrect and a feature will probably fail at runtime (e.g. a missing connection string).</summary>
    Critical,
}

/// <summary>
/// Functional area a configuration issue belongs to. Used purely for grouping/filtering in logs
/// and the health endpoints — it does not affect validation behavior.
/// </summary>
public enum ConfigurationCategory
{
    /// <summary>Authentication, secrets, credentials (e.g. JWT, plaintext passwords).</summary>
    Security,

    /// <summary>Database connectivity and provider configuration.</summary>
    Database,

    /// <summary>Media/file storage provider configuration (local disk, S3, etc.).</summary>
    Storage,

    /// <summary>Recognition/embedding pipeline, ONNX models.</summary>
    AI,

    /// <summary>CORS, external endpoints, hostnames/URLs.</summary>
    Networking,

    /// <summary>Caching and other performance-affecting configuration (e.g. Redis).</summary>
    Performance,

    /// <summary>Background workers, queues, hosting environment.</summary>
    Infrastructure,

    /// <summary>Tenant mode and multi-tenancy configuration.</summary>
    MultiTenancy,

    /// <summary>General configuration issues that do not fit another category.</summary>
    Configuration,
}

/// <summary>A single, non-fatal startup configuration finding.</summary>
public sealed record ConfigurationIssue(
    ConfigurationSeverity Severity,
    ConfigurationCategory Category,
    string ConfigurationKey,
    string Message,
    string SuggestedFix);
