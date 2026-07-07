# AI12.OBS.8 — Configuration Severity Framework

## Purpose

Replace the flat `ConfigurationWarning` model (introduced in `AI12.OBS.7`) with
a richer `ConfigurationIssue` model that carries a **severity** so operators
can triage startup configuration findings at a glance instead of treating
every finding as equally important.

This is a pure metadata/architecture enhancement:

- No validation *rule* changed — the same 9 configuration areas are checked,
  with the same conditions.
- No startup behavior changed — every issue is still only ever **logged**;
  nothing throws and nothing blocks startup.
- No AI recognition, business logic, or controller behavior changed.
- `/health` and `/health/ready` now additionally expose this severity
  metadata (they did not previously expose configuration issues at all).

## The New Model

```csharp
public enum ConfigurationSeverity
{
    Information,
    Warning,
    Critical,
}

public sealed record ConfigurationIssue(
    ConfigurationSeverity Severity,
    ConfigurationCategory Category,   // see AI12.OBS.9
    string ConfigurationKey,
    string Message,
    string SuggestedFix);
```

`ConfigurationValidator.Validate(...)` now returns
`IReadOnlyList<ConfigurationIssue>` instead of
`IReadOnlyList<ConfigurationWarning>`. `ConfigurationWarning` no longer exists.

## Severity Definitions

| Severity | Meaning | Examples in this codebase |
|---|---|---|
| **Information** | Configuration is valid but could be improved. | `InsightFace:PipelineVersion` not explicitly set (relying on code default); `InsightFace:DetectionModelFile`/`RecognitionModelFile` not explicitly set |
| **Warning** | Configuration works but is not recommended. | Plaintext password in `ConnectionStrings:DefaultConnection`; Redis connection configured but `UseRedis=false`; unrecognized `Tenancy:Mode` value (silently defaulting); weak/short `Jwt:Key`; partially-configured S3 credentials |
| **Critical** | Configuration is very likely incorrect; a feature will probably fail at runtime. | Missing `ConnectionStrings:DefaultConnection`; missing/non-existent `InsightFace:ModelDirectory`; unrecognized `Media:Provider` value (silently defaulting); missing/invalid `Jwt:ExpiryMinutes` (issued tokens expire immediately) |

Severity **only** changes the *log level* used to report a finding
(`Information` → `LogInformation`, `Warning` → `LogWarning`, `Critical` →
`LogError`). It never throws, and it never affects whether the process starts,
or the pass/fail computation of `/health/ready`.

> Note: some conceptually "Critical" cases in the original spec — e.g. a
> missing JWT signing key — are already prevented by a separate, pre-existing
> **hard** (fail-fast) check earlier in `Program.cs` (`?? throw new
> InvalidOperationException(...)`), so they can never actually reach this
> soft validator. Those hard checks are intentionally left untouched.

## Startup Logging

Each issue is now logged as an aligned, multi-line block instead of a flat
3-line warning:

```
----------------------------------------------------------
Startup Configuration Validation
----------------------------------------------------------
warn: [Warning]
        Category           : Security
        Configuration Key  : ConnectionStrings:DefaultConnection
        Message            : Database connection string contains a plaintext password in configuration.
        Suggested Fix      : Move the password out of appsettings.json into user-secrets (Development) or environment variables / a secrets manager (Production).
fail: [Critical]
        Category           : AI
        Configuration Key  : InsightFace:ModelDirectory
        Message            : Configured model directory 'models/insightface' (resolved to 'C:\...\models/insightface') does not exist on disk.
        Suggested Fix      : Create the directory and place det_10g.onnx and w600k_r50.onnx inside it, or correct InsightFace:ModelDirectory to point at the correct location.
info: 2 configuration issue(s) detected (1 Critical, 1 Warning, 0 Information). Startup is continuing normally - these are advisory only.
----------------------------------------------------------
```

(`Category` is introduced by `AI12.OBS.9`, delivered together with this
change since both were requested in the same milestone batch.)

This was verified by running the compiled binary standalone: both a
`Warning` and a `Critical` issue were correctly logged at their respective
levels (`warn:` / `fail:`), and the application continued starting normally,
background workers included.

## Health Endpoint Changes

`/health` and `/health/ready` now include a `configurationIssues` object
(counts by severity + the full list) as an **additive** field — no existing
field, and no pass/fail semantics, changed:

```json
"configurationIssues": {
  "critical": 1,
  "warning": 1,
  "information": 0,
  "items": [
    {
      "severity": "Critical",
      "category": "AI",
      "configurationKey": "InsightFace:ModelDirectory",
      "message": "...",
      "suggestedFix": "..."
    }
  ]
}
```

- On `/health`, this appears under the `health` extension object.
- On `/health/ready`, this appears under the `checks` extension object.
- In both cases, `overallStatus` / `isReady` computation is **unchanged**
  from `AI12.OBS.6` — configuration issues are visibility only, not a
  readiness/health gate, per this milestone's explicit scope ("expose the
  new metadata" only).

## Future Extensibility

The severity framework is designed so future validators (e.g. CORS origin
sanity, connection pool sizing, log retention settings) can be added to
`ConfigurationValidator` without any changes to the logging or health-endpoint
plumbing — they simply add another `ConfigurationIssue` with an appropriate
`Severity`/`Category`. Because severity is a first-class enum (not a string),
future consumers (dashboards, alerting rules) can safely filter/sort on it
without string matching.

## Files Modified / Created

| File | Change |
|---|---|
| `Abhyanvaya.API/Diagnostics/ConfigurationIssue.cs` | **New.** `ConfigurationSeverity` enum, `ConfigurationCategory` enum (see AI12.OBS.9), and the `ConfigurationIssue` record. |
| `Abhyanvaya.API/Diagnostics/ConfigurationValidator.cs` | All 9 checks now build `ConfigurationIssue` (with severity + category) instead of `ConfigurationWarning`; `ConfigurationWarning` removed. No validation conditions changed, except severities were assigned per the table above (e.g. unrecognized storage provider and missing model directory promoted to `Critical`; JWT expiry misconfiguration promoted to `Critical` since it breaks authentication). |
| `Abhyanvaya.API/Program.cs` | `LogConfigurationValidationWarnings` renamed to `LogConfigurationIssues`, now logs `[Severity]` / `Category` / `Configuration Key` / `Message` / `Suggested Fix` at a level derived from severity; `/health` and `/health/ready` now include `configurationIssues` via a shared `ToConfigurationIssueSummaries` helper. |
| `docs/AI12_OBS8_CONFIGURATION_SEVERITY.md` | **New.** This document. |

## Build Status

`dotnet build Abhyanvaya.sln` — **Build succeeded, 0 errors** (pre-existing
nullable-reference/NuGet-advisory warnings only).

Runtime-verified: ran the compiled binary standalone; confirmed the new
`[Severity]` log format, correct log levels per severity, and both `/health`
and `/health/ready` returning the new `configurationIssues` metadata, with
startup and readiness/health pass-fail behavior unchanged.

## Architecture Impact

None on runtime behavior — purely additive diagnostics metadata:

- No new services registered in DI.
- No controller, DTO, or public API surface changed (health endpoints only
  gained new response fields, not new endpoints or changed status codes).
- No changes to recognition, queue, worker, or finalization business logic.
- No changes to any existing hard-fail configuration checks.
