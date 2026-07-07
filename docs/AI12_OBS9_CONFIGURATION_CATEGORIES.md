# AI12.OBS.9 — Configuration Categories

## Purpose

Classify every startup configuration finding (`ConfigurationIssue`, from
`AI12.OBS.8`) into a functional area via a new `ConfigurationCategory` enum,
so operators/dashboards can filter or group issues (e.g. "show me all
Security issues" or "how many AI-related issues do we have?").

This is classification only:

- No validation rule changed — the same conditions from `AI12.OBS.7` trigger
  the same issues.
- No severities changed from `AI12.OBS.8`.
- No startup or health-endpoint pass/fail behavior changed.

## The Category Enum

```csharp
public enum ConfigurationCategory
{
    Security,
    Database,
    Storage,
    AI,
    Networking,
    Performance,
    Infrastructure,
    MultiTenancy,
    Configuration,
}
```

Every `ConfigurationIssue` now carries a `Category` alongside its `Severity`.

## Category Assignments In This Codebase

| Configuration Area | Category | Rationale |
|---|---|---|
| `Jwt:Key` (weak/short) | Security | Authentication secret strength |
| `Jwt:ExpiryMinutes` (missing/invalid) | Security | Directly breaks token-based authentication |
| `ConnectionStrings:DefaultConnection` (missing / malformed) | Database | Core database connectivity |
| `ConnectionStrings:DefaultConnection` (plaintext password) | Security | Credential exposure, not a connectivity problem |
| `Media:Provider` (unrecognized) | Storage | Media/file storage provider selection |
| `Media:S3:Region` / `Media:S3:Endpoint` (missing) | Storage | S3-compatible storage configuration |
| `Embedding:DefaultProvider` (unknown/missing) | AI | Recognition/embedding pipeline selection |
| `InsightFace:ModelDirectory` (missing/not found) | AI | Recognition model deployment |
| `InsightFace:DetectionModelFile` / `RecognitionModelFile` (unset) | AI | Recognition model deployment |
| `InsightFace:PipelineVersion` (unset) | AI | Recognition pipeline traceability |
| `UseRedis` / `Redis:Connection` (configured but unused) | Performance | Distributed cache configuration |
| `Tenancy:Mode` (unrecognized) | MultiTenancy | Tenant isolation mode |
| `Media:PhysicalRoot` (inaccessible) | Storage | Local media storage writability |
| `Media:S3:AccessKeyId` / `SecretAccessKey` (partial) | Storage | S3 credential configuration |

`Networking`, `Infrastructure`, and the generic `Configuration` fallback are
part of the enum for **future** validators — e.g. CORS origin validation
(`Networking`), background worker/queue tuning (`Infrastructure`), or any
finding that doesn't cleanly map to the other seven categories
(`Configuration`). No current check needed them, but the enum is defined now
so future checks don't require a breaking enum change.

## Startup Logging

`Category` is now printed as its own line in the per-issue block introduced
in `AI12.OBS.8`:

```
warn: [Warning]
        Category           : AI
        Configuration Key  : InsightFace:ModelDirectory
        Message            : Relative model directory detected.
        Suggested Fix      : Resolve against ContentRootPath.
```

(That specific "relative model directory" finding no longer occurs after
`AI12.OBS.10` — relative paths are now resolved automatically instead of
being flagged — but the log shape shown above is exactly what any AI-category
issue looks like today, e.g. the "model directory does not exist" finding.)

## Health Endpoint

`Category` is included in every item of the `configurationIssues.items` array
on both `/health` and `/health/ready` (see `AI12.OBS.8` for the full shape):

```json
{
  "severity": "Critical",
  "category": "AI",
  "configurationKey": "InsightFace:ModelDirectory",
  "message": "...",
  "suggestedFix": "..."
}
```

No other health endpoint behavior changed.

## Files Modified / Created

| File | Change |
|---|---|
| `Abhyanvaya.API/Diagnostics/ConfigurationIssue.cs` | Added the `ConfigurationCategory` enum (delivered together with `AI12.OBS.8`'s `ConfigurationIssue`/`ConfigurationSeverity` since both were requested in the same milestone batch). |
| `Abhyanvaya.API/Diagnostics/ConfigurationValidator.cs` | Every `ConfigurationIssue` construction now supplies a `Category` per the table above. |
| `Abhyanvaya.API/Program.cs` | `LogConfigurationIssues` prints `Category` as part of each issue's log block; `ToConfigurationIssueSummaries` includes `category` in the health endpoint JSON projection. |
| `docs/AI12_OBS9_CONFIGURATION_CATEGORIES.md` | **New.** This document. |

## Build Status

`dotnet build Abhyanvaya.sln` — **Build succeeded, 0 errors** (pre-existing
nullable-reference/NuGet-advisory warnings only). Verified at runtime that
`Category` appears correctly in both the startup log and the `/health` /
`/health/ready` JSON responses.

## Architecture Impact

None on runtime behavior — pure classification metadata added to an already
additive (AI12.OBS.7/8), warn-only diagnostics feature. No validation rules,
severities, controllers, or business logic changed.
