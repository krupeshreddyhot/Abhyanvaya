using Abhyanvaya.API.Media;
using Abhyanvaya.Domain.Constants;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.API.Diagnostics;

/// <summary>
/// Read-only, advisory startup configuration checks. Every check in this class only ever produces
/// a <see cref="ConfigurationIssue"/> to be <em>logged</em> — none of them throw, block, or
/// otherwise affect startup, regardless of <see cref="ConfigurationSeverity"/>. This is a softer,
/// broader complement to the handful of existing hard (fail-fast) checks already present elsewhere
/// (e.g. the <c>Jwt:Issuer</c>/<c>Jwt:Audience</c>/<c>Jwt:Key</c> required-value checks and
/// <c>MediaOptionsValidator</c>'s S3-bucket-required check in <c>Program.cs</c>/<c>AddMediaStorage</c>),
/// which intentionally continue to throw for configuration that makes the application entirely
/// non-functional. This validator instead surfaces "this will probably misbehave, but the app can
/// still start" signals, ranked by <see cref="ConfigurationSeverity"/> so operators can triage.
/// </summary>
public static class ConfigurationValidator
{
    private static readonly string[] KnownEmbeddingProviders =
    {
        EmbeddingProviders.InsightFace,
        EmbeddingProviders.FaceNet,
        EmbeddingProviders.AzureFace,
        EmbeddingProviders.OpenCv,
    };

    public static IReadOnlyList<ConfigurationIssue> Validate(WebApplication app, ModelAvailabilityReport modelReport)
    {
        var issues = new List<ConfigurationIssue>();
        var configuration = app.Configuration;

        ValidateJwtConfiguration(configuration, issues);
        ValidateDatabaseConnectionString(configuration, issues);
        ValidateStorageProvider(app, issues);
        ValidateRecognitionProvider(configuration, issues);
        ValidateModelDirectory(configuration, modelReport, issues);
        ValidatePipelineVersion(configuration, issues);
        ValidateRedisConfiguration(configuration, issues);
        ValidateTenantMode(configuration, issues);
        ValidateMediaStorage(app, issues);

        return issues;
    }

    private static void ValidateJwtConfiguration(IConfiguration configuration, List<ConfigurationIssue> issues)
    {
        // Jwt:Issuer / Jwt:Audience / Jwt:Key presence is already a hard (fail-fast) requirement
        // enforced earlier in Program.cs via `?? throw new InvalidOperationException(...)`, so by
        // the time this runs they are guaranteed non-null. This check adds softer quality signals
        // on top of that hard guarantee.
        var jwtKey = configuration["Jwt:Key"] ?? string.Empty;
        if (jwtKey.Length < 32)
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Warning,
                ConfigurationCategory.Security,
                "Jwt:Key",
                $"JWT signing key is only {jwtKey.Length} characters; HMAC-SHA256 recommends at least 32 characters (256 bits) of entropy.",
                "Generate a longer, cryptographically random Jwt:Key (>= 32 characters) and store it via user-secrets or an environment variable, not directly in appsettings.json."));
        }

        // JwtService.GenerateToken() computes `Convert.ToInt32(configuration["Jwt:ExpiryMinutes"])`
        // directly; Convert.ToInt32(null) returns 0, so a missing/invalid value silently issues
        // tokens that expire immediately (AddMinutes(0)) — a functional authentication failure,
        // not just a quality nit, hence Critical rather than Warning.
        var expiryMinutesRaw = configuration["Jwt:ExpiryMinutes"];
        if (string.IsNullOrWhiteSpace(expiryMinutesRaw) || !int.TryParse(expiryMinutesRaw, out var expiryMinutes) || expiryMinutes <= 0)
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Critical,
                ConfigurationCategory.Security,
                "Jwt:ExpiryMinutes",
                "JWT token expiry is not configured (or is not a positive number); issued tokens will expire immediately, breaking authentication.",
                "Set Jwt:ExpiryMinutes to an explicit positive value (e.g. 60)."));
        }
    }

    private static void ValidateDatabaseConnectionString(IConfiguration configuration, List<ConfigurationIssue> issues)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Critical,
                ConfigurationCategory.Database,
                "ConnectionStrings:DefaultConnection",
                "Database connection string is missing; the application will fail on first database access.",
                "Configure a valid PostgreSQL connection string (Host, Port, Database, Username, Password) via appsettings, user-secrets, or an environment variable."));
            return;
        }

        if (!connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Warning,
                ConfigurationCategory.Database,
                "ConnectionStrings:DefaultConnection",
                "Database connection string does not appear to specify a database name.",
                "Verify the connection string includes 'Database=<name>'."));
        }

        if (connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Warning,
                ConfigurationCategory.Security,
                "ConnectionStrings:DefaultConnection",
                "Database connection string contains a plaintext password in configuration.",
                "Move the password out of appsettings.json into user-secrets (Development) or environment variables / a secrets manager (Production)."));
        }
    }

    private static void ValidateStorageProvider(WebApplication app, List<ConfigurationIssue> issues)
    {
        var mediaOptions = app.Services.GetRequiredService<IOptions<MediaOptions>>().Value;
        var rawProvider = mediaOptions.Provider?.Trim();
        var normalizedProvider = mediaOptions.GetActiveProviderName();

        if (!string.IsNullOrWhiteSpace(rawProvider)
            && !string.Equals(rawProvider, normalizedProvider, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Critical,
                ConfigurationCategory.Storage,
                "Media:Provider",
                $"Configured provider '{rawProvider}' is not recognized; the application silently defaulted to '{normalizedProvider}'.",
                "Set Media:Provider to one of the supported values: 'local' or 's3'."));
        }

        if (string.Equals(normalizedProvider, S3StorageProvider.Id, StringComparison.OrdinalIgnoreCase))
        {
            // Media:S3:Bucket being empty is already a hard (fail-fast) check via MediaOptionsValidator
            // (IValidateOptions<MediaOptions>.ValidateOnStart()); this covers a secondary, non-fatal case.
            if (string.IsNullOrWhiteSpace(mediaOptions.S3.Region) && string.IsNullOrWhiteSpace(mediaOptions.S3.Endpoint))
            {
                issues.Add(new ConfigurationIssue(
                    ConfigurationSeverity.Warning,
                    ConfigurationCategory.Storage,
                    "Media:S3:Region / Media:S3:Endpoint",
                    "S3 storage is selected but neither Region nor Endpoint is configured; the AWS SDK will fall back to its own defaults, which is unlikely to be correct for S3-compatible providers (e.g. Cloudflare R2, MinIO).",
                    "Set Media:S3:Region (for AWS) or Media:S3:Endpoint (for S3-compatible providers)."));
            }
        }
    }

    private static void ValidateRecognitionProvider(IConfiguration configuration, List<ConfigurationIssue> issues)
    {
        var configuredProvider = configuration["Embedding:DefaultProvider"];
        if (string.IsNullOrWhiteSpace(configuredProvider))
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Warning,
                ConfigurationCategory.AI,
                "Embedding:DefaultProvider",
                "Recognition/embedding provider is not explicitly configured.",
                $"Set Embedding:DefaultProvider to one of: {string.Join(", ", KnownEmbeddingProviders)}."));
            return;
        }

        if (!KnownEmbeddingProviders.Any(known => string.Equals(known, configuredProvider, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Warning,
                ConfigurationCategory.AI,
                "Embedding:DefaultProvider",
                $"Configured provider '{configuredProvider}' does not match any known embedding provider.",
                $"Set Embedding:DefaultProvider to one of: {string.Join(", ", KnownEmbeddingProviders)}."));
        }
    }

    private static void ValidateModelDirectory(
        IConfiguration configuration,
        ModelAvailabilityReport modelReport,
        List<ConfigurationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(modelReport.ConfiguredModelDirectory))
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Critical,
                ConfigurationCategory.AI,
                "InsightFace:ModelDirectory",
                "InsightFace model directory is not configured.",
                "Set InsightFace:ModelDirectory to the folder containing the detection and embedding ONNX models."));
            return;
        }

        // AI12.OBS.10: relative paths are now resolved against ContentRootPath (see
        // ModelPathResolver) instead of merely being flagged as risky, so a relative value here is
        // no longer, by itself, a configuration issue. Only an actually-missing resolved directory
        // is reported.
        if (!modelReport.ModelDirectoryExists)
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Critical,
                ConfigurationCategory.AI,
                "InsightFace:ModelDirectory",
                $"Configured model directory '{modelReport.ConfiguredModelDirectory}' (resolved to '{modelReport.ResolvedModelDirectory}') does not exist on disk.",
                "Create the directory and place det_10g.onnx and w600k_r50.onnx inside it, or correct InsightFace:ModelDirectory to point at the correct location."));
        }

        var configuredDetectionFile = configuration["InsightFace:DetectionModelFile"];
        if (string.IsNullOrWhiteSpace(configuredDetectionFile))
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Information,
                ConfigurationCategory.AI,
                "InsightFace:DetectionModelFile",
                "Detection model file name is not explicitly configured; relying on the code default.",
                "Set InsightFace:DetectionModelFile explicitly (e.g. det_10g.onnx) for deployment traceability."));
        }

        var configuredRecognitionFile = configuration["InsightFace:RecognitionModelFile"];
        if (string.IsNullOrWhiteSpace(configuredRecognitionFile))
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Information,
                ConfigurationCategory.AI,
                "InsightFace:RecognitionModelFile",
                "Embedding model file name is not explicitly configured; relying on the code default.",
                "Set InsightFace:RecognitionModelFile explicitly (e.g. w600k_r50.onnx) for deployment traceability."));
        }
    }

    private static void ValidatePipelineVersion(IConfiguration configuration, List<ConfigurationIssue> issues)
    {
        var configuredPipelineVersion = configuration["InsightFace:PipelineVersion"];
        if (string.IsNullOrWhiteSpace(configuredPipelineVersion))
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Information,
                ConfigurationCategory.AI,
                "InsightFace:PipelineVersion",
                "Recognition pipeline version is not explicitly configured in appsettings; the application is relying on the compiled-in code default.",
                "Set InsightFace:PipelineVersion explicitly per environment so recognition results can be traced back to a known pipeline version during audits/incident review."));
        }
    }

    private static void ValidateRedisConfiguration(IConfiguration configuration, List<ConfigurationIssue> issues)
    {
        // UseRedis=true with no connection string is already a hard (fail-fast) check in Program.cs
        // (`throw new InvalidOperationException(...)`). This covers the inverse, softer case.
        var useRedis = configuration.GetValue<bool>("UseRedis");
        var redisConnection = configuration["Redis:Connection"] ?? configuration.GetConnectionString("Redis");

        if (!useRedis && !string.IsNullOrWhiteSpace(redisConnection))
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Warning,
                ConfigurationCategory.Performance,
                "UseRedis",
                "A Redis connection is configured (Redis:Connection / ConnectionStrings:Redis) but UseRedis is false, so the in-memory distributed cache is being used instead and the Redis configuration is currently ignored.",
                "Set UseRedis to true to actually use the configured Redis instance, or remove the unused Redis connection configuration to avoid confusion."));
        }
    }

    private static void ValidateTenantMode(IConfiguration configuration, List<ConfigurationIssue> issues)
    {
        var tenancyModeRaw = configuration["Tenancy:Mode"];
        if (string.IsNullOrWhiteSpace(tenancyModeRaw))
        {
            return;
        }

        var isRecognized = string.Equals(tenancyModeRaw, "SingleTenant", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tenancyModeRaw, "MultiTenant", StringComparison.OrdinalIgnoreCase);

        if (!isRecognized)
        {
            issues.Add(new ConfigurationIssue(
                ConfigurationSeverity.Warning,
                ConfigurationCategory.MultiTenancy,
                "Tenancy:Mode",
                $"Configured value '{tenancyModeRaw}' is not a recognized tenancy mode; the application silently defaulted to Multi Tenant.",
                "Set Tenancy:Mode to exactly 'SingleTenant' or 'MultiTenant'."));
        }
    }

    private static void ValidateMediaStorage(WebApplication app, List<ConfigurationIssue> issues)
    {
        var mediaOptions = app.Services.GetRequiredService<IOptions<MediaOptions>>().Value;
        var activeProvider = mediaOptions.GetActiveProviderName();

        if (string.Equals(activeProvider, LocalStorageProvider.Id, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var root = string.IsNullOrWhiteSpace(mediaOptions.PhysicalRoot)
                    ? Path.Combine(app.Environment.WebRootPath ?? app.Environment.ContentRootPath, "branding")
                    : mediaOptions.PhysicalRoot;

                Directory.CreateDirectory(root);
            }
            catch (Exception ex)
            {
                issues.Add(new ConfigurationIssue(
                    ConfigurationSeverity.Critical,
                    ConfigurationCategory.Storage,
                    "Media:PhysicalRoot",
                    $"Local media storage root could not be created/accessed: {ex.Message}",
                    "Verify the configured Media:PhysicalRoot path exists and the application's process account has write permission to it."));
            }
        }
        else if (string.Equals(activeProvider, S3StorageProvider.Id, StringComparison.OrdinalIgnoreCase))
        {
            var hasAccessKey = !string.IsNullOrWhiteSpace(mediaOptions.S3.AccessKeyId);
            var hasSecretKey = !string.IsNullOrWhiteSpace(mediaOptions.S3.SecretAccessKey);
            if (hasAccessKey != hasSecretKey)
            {
                issues.Add(new ConfigurationIssue(
                    ConfigurationSeverity.Warning,
                    ConfigurationCategory.Storage,
                    "Media:S3:AccessKeyId / Media:S3:SecretAccessKey",
                    "Only one of Media:S3:AccessKeyId / Media:S3:SecretAccessKey is configured; partial credentials will cause the AWS SDK to fall back to its default credential chain, which may not be intended.",
                    "Configure both Media:S3:AccessKeyId and Media:S3:SecretAccessKey together, or remove both to intentionally rely on an IAM role / the default AWS credential chain."));
            }
        }
    }
}
