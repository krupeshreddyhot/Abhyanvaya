using Abhyanvaya.Application.Academic.Allocation;

namespace Abhyanvaya.Application.Academic.Architecture;

/// <summary>AI29.1D Prompt 21A — machine-readable compliance status for CI gates.</summary>
public enum Ai291DArchitectureComplianceStatus
{
    /// <summary>UI source scanned and all checks passed.</summary>
    FullyVerified = 0,

    /// <summary>UI source unavailable; backend/assembly checks passed.</summary>
    PartiallyVerified = 1,

    /// <summary>One or more architectural violations.</summary>
    Failed = 2,
}

/// <summary>AI29.1D Prompt 21 / 21A — architecture compliance report (layering + UI non-authority).</summary>
public sealed class Ai291DArchitectureComplianceReport
{
    public DateTime GeneratedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Required layering contract.</summary>
    public string RequiredLayering { get; init; } = "UI → API / Application Contracts → Domain Services";

    /// <summary>
    /// Backward-compatible pass flag: true when <see cref="Status"/> is not <see cref="Ai291DArchitectureComplianceStatus.Failed"/>.
    /// PARTIALLY_VERIFIED is Passed=true but FullyVerified=false.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>CI-facing status: FULLY_VERIFIED | PARTIALLY_VERIFIED | FAILED.</summary>
    public string Status { get; init; } = Ai291DArchitectureComplianceStatuses.Failed;

    /// <summary>Typed status (same semantics as <see cref="Status"/>).</summary>
    public Ai291DArchitectureComplianceStatus ComplianceStatus { get; init; } =
        Ai291DArchitectureComplianceStatus.Failed;

    /// <summary>True only when Status is FULLY_VERIFIED (never when PARTIALLY_VERIFIED).</summary>
    public bool FullyVerified { get; init; }

    /// <summary>Whether the UI source tree was scanned.</summary>
    public bool UiScanExecuted { get; init; }

    /// <summary>Backend authority types present and no backend-authority violations.</summary>
    public bool BackendChecksPassed { get; init; }

    /// <summary>Platform allocation boundary guard passed.</summary>
    public bool PlatformBoundaryPassed { get; init; }

    public int ViolationCount { get; init; }

    public IReadOnlyList<string> Checks { get; init; } = [];

    public IReadOnlyList<string> Violations { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];

    public Ai291DUiScanSummary? UiScan { get; init; }

    public Ai291DBackendAuthoritySummary BackendAuthority { get; init; } = new();
}

/// <summary>String constants for CI / snapshot consumers.</summary>
public static class Ai291DArchitectureComplianceStatuses
{
    public const string FullyVerified = "FULLY_VERIFIED";
    public const string PartiallyVerified = "PARTIALLY_VERIFIED";
    public const string Failed = "FAILED";

    public static string ToCiString(Ai291DArchitectureComplianceStatus status) => status switch
    {
        Ai291DArchitectureComplianceStatus.FullyVerified => FullyVerified,
        Ai291DArchitectureComplianceStatus.PartiallyVerified => PartiallyVerified,
        _ => Failed,
    };
}

public sealed class Ai291DUiScanSummary
{
    public bool Executed { get; init; }
    public string? UiRoot { get; init; }
    public int FilesScanned { get; init; }
    public int ForbiddenDataAccessHits { get; init; }
    public int ForbiddenAuthorityHits { get; init; }
}

public sealed record Ai291DBackendAuthoritySummary
{
    public bool AllocationEnginePresent { get; init; }
    public bool AttendanceSessionResolverPresent { get; init; }
    public bool AllocationLifecycleServicePresent { get; init; }
    public bool AllocationGovernanceServicePresent { get; init; }
    public bool SectionCapacityEnginePresent { get; init; }
    public bool ExistingPlatformGuardPassed { get; init; }
}

/// <summary>Optional validation context for tests / specialized hosts (not a second architecture framework).</summary>
public sealed class Ai291DArchitectureGuardOptions
{
    public string? RepositoryRoot { get; init; }

    /// <summary>When true, skips UI file + package.json scan (forces PARTIALLY_VERIFIED if otherwise clean).</summary>
    public bool SkipUiScan { get; init; }

    /// <summary>Override UI <c>src</c> root for scanning.</summary>
    public string? UiSourceRootOverride { get; init; }

    /// <summary>Override package.json path (defaults next to UI root).</summary>
    public string? PackageJsonPathOverride { get; init; }

    /// <summary>Override platform allocation boundary evaluation (tests).</summary>
    public Func<AllocationArchitectureReport>? PlatformBoundaryEvaluator { get; init; }

    /// <summary>Override Domain assembly under inspection (tests).</summary>
    public System.Reflection.Assembly? DomainAssemblyOverride { get; init; }

    /// <summary>Override Domain .csproj path (tests).</summary>
    public string? DomainCsprojPathOverride { get; init; }

    /// <summary>Additional types inspected for Application → UI leakage (tests / probes).</summary>
    public IReadOnlyList<Type>? AdditionalApplicationTypesToInspect { get; init; }
}
