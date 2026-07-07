# AI12.OBS.7 — Startup Configuration Validation

## Purpose

Give operators an early, at-a-glance signal that something in `appsettings.json` /
environment configuration looks *wrong* — a typo, a silently-defaulted value, a
security hygiene issue, a portability risk — **without ever stopping the
application from starting**.

This is deliberately a **soft / advisory** layer:

- It only ever calls `logger.LogWarning(...)`.
- It never throws.
- It never blocks, delays, or retries startup.
- It runs exactly once, immediately after the existing startup configuration
  summary (`AI11.HARDENING.2` / `AI12.OBS.1–6`).

It complements — and does **not** replace — the small number of existing
**hard** (fail-fast) checks already in `Program.cs` and `MediaOptionsValidator`
(e.g. `Jwt:Issuer` / `Jwt:Audience` / `Jwt:Key` being required, `UseRedis=true`
requiring a Redis connection string, S3 storage requiring a bucket name). Those
checks exist because the application is provably non-functional without them,
so failing fast is correct. AI12.OBS.7 checks are for the larger set of
"this will probably misbehave in a way that's easy to miss" cases where the
app can still boot and serve some traffic.

## What Is Validated

| # | Area | Configuration Key(s) | Checks |
|---|------|----------------------|--------|
| 1 | JWT Configuration | `Jwt:Key`, `Jwt:ExpiryMinutes` | Key length ≥ 32 chars (HMAC-SHA256 recommendation); `ExpiryMinutes` explicitly set to a positive number |
| 2 | Database Connection String | `ConnectionStrings:DefaultConnection` | Present; contains a `Database=` segment; **flags plaintext `Password=` in configuration** |
| 3 | Storage Provider | `Media:Provider` / `Branding:Provider` | Configured value resolves to a recognized provider (`local`/`s3`) instead of silently falling back; for S3, warns if both `Region` and `Endpoint` are empty |
| 4 | Recognition Provider | `Embedding:DefaultProvider` | Value matches a known provider (`InsightFace`, `FaceNet`, `AzureFace`, `OpenCV`) |
| 5 | Model Directory | `InsightFace:ModelDirectory`, `InsightFace:DetectionModelFile`, `InsightFace:RecognitionModelFile` | Configured; **flags relative paths** (working-directory risk under IIS/Windows Service/some containers); directory exists; model file names explicitly configured (not relying on code defaults) |
| 6 | Pipeline Version | `InsightFace:PipelineVersion` | Explicitly configured (not relying on the compiled-in default) so results are traceable to a known pipeline version |
| 7 | Redis Configuration | `UseRedis`, `Redis:Connection` / `ConnectionStrings:Redis` | Flags a Redis connection that is configured but **unused** because `UseRedis=false` (likely operator mistake) |
| 8 | Tenant Mode | `Tenancy:Mode` | Value is exactly `SingleTenant` or `MultiTenant`; flags unrecognized values that silently default to Multi Tenant |
| 9 | Media Storage | `Media:PhysicalRoot` / `Branding:PhysicalRoot`, `Media:S3:AccessKeyId` / `Media:S3:SecretAccessKey` | For local storage: the physical root directory can be created/accessed; for S3: flags partially-configured credentials (only one of AccessKeyId/SecretAccessKey set) |

Each finding produces one `ConfigurationWarning(ConfigurationKey, Warning, SuggestedFix)`.

## Sample Startup Output

```
----------------------------------------------------------
Startup Configuration Validation
----------------------------------------------------------
Configuration Warning : Database connection string contains a plaintext password in configuration.
  Configuration Key    : ConnectionStrings:DefaultConnection
  Suggested Fix        : Move the password out of appsettings.json into user-secrets (Development) or environment variables / a secrets manager (Production).
Configuration Warning : InsightFace:ModelDirectory ('models/insightface') is a relative path. When hosted outside 'dotnet run' (IIS, a Windows Service, or some container entrypoints), the process's current working directory can differ from the application's content root, causing model files to appear missing even though they exist on disk.
  Configuration Key    : InsightFace:ModelDirectory
  Suggested Fix        : Use an absolute path, or resolve the configured value against AppContext.BaseDirectory / IWebHostEnvironment.ContentRootPath at startup.
2 configuration warning(s) detected. Startup is continuing normally - these are advisory only.
----------------------------------------------------------
```

When no issues are found:

```
----------------------------------------------------------
Startup Configuration Validation
----------------------------------------------------------
All startup configuration checks passed. No issues detected.
----------------------------------------------------------
```

This was verified against the actual `appsettings.json` in this repository: it
correctly flags the checked-in plaintext database password and the relative
`InsightFace:ModelDirectory` path, and passes cleanly on all other checks.

## Architecture

- **`Abhyanvaya.API/Diagnostics/ConfigurationValidator.cs`** (new) — a single
  static class, `ConfigurationValidator.Validate(WebApplication app,
  ModelAvailabilityReport modelReport)`, returning
  `IReadOnlyList<ConfigurationWarning>`. Each of the 9 areas above is a small,
  independent private method; none of them throw — I/O (e.g.
  `Directory.CreateDirectory` for the local media root check) is wrapped in
  `try/catch` and converted into a warning instead.
- **`Program.cs`** — `LogStartupConfigurationSummary` calls
  `ConfigurationValidator.Validate(app, modelReport)` once, immediately after
  printing the existing startup summary, then a new local function
  `LogConfigurationValidationWarnings` prints each warning as three log lines
  (`Configuration Warning`, `Configuration Key`, `Suggested Fix`) plus a
  trailing count line. It reuses the same `IOptions<MediaOptions>`,
  `IOptions<InsightFaceOptions>`, and `ModelAvailabilityReport` already
  resolved for the main summary — no duplicate DI resolution or filesystem
  scanning.
- No new services, no hosted service, no middleware, no controller — this is
  pure startup diagnostics, consistent with `AI11.HARDENING.3` /
  `AI12.OBS.1–6`.

## Why Warn-Only Is The Right Default Here

- Some of these signals (e.g. a relative model directory, an unused Redis
  connection string, half-configured S3 credentials) are *frequently*
  intentional in development/test environments, so hard-failing on them would
  be disruptive.
- The goal is **operator visibility**, not policy enforcement — a human (or a
  log-scraping alert) reviews these warnings and decides whether they matter
  for that environment.
- The genuinely load-bearing configuration keys (`Jwt:*`, `UseRedis` + Redis
  connection, S3 bucket name) already have separate hard checks elsewhere in
  the codebase and are left untouched by this feature.

## Verification Steps

1. Run the API (`dotnet run` or the compiled `Abhyanvaya.API.dll`) and inspect
   the console/log output for the `Startup Configuration Validation` section
   immediately after the main startup summary.
2. Confirm it appears **exactly once** per process start.
3. Deliberately break a value (e.g. set `InsightFace:PipelineVersion` to an
   empty string, or remove `Tenancy:Mode`) and confirm the corresponding
   warning appears, and that the application still starts and serves
   `/health/live` successfully.
4. Confirm no `LogError`/exception is ever raised by this feature, and the
   process exit code / hosted worker startup is unaffected.

## Files Modified / Created

| File | Change |
|------|--------|
| `Abhyanvaya.API/Diagnostics/ConfigurationValidator.cs` | **New.** `ConfigurationWarning` record + `ConfigurationValidator.Validate(...)` with the 9 checks described above. |
| `Abhyanvaya.API/Program.cs` | `LogStartupConfigurationSummary` now calls `ConfigurationValidator.Validate(...)` and a new `LogConfigurationValidationWarnings` local function once, right after the existing startup summary block. No existing logic changed. |
| `docs/AI12_OBS7_CONFIGURATION_VALIDATION.md` | **New.** This document. |

## Build Status

`dotnet build Abhyanvaya.sln` — **Build succeeded, 0 errors** (43 pre-existing
nullable-reference/NuGet-advisory warnings only, unrelated to this change).

Runtime-verified: the compiled binary was started standalone (isolated port,
Development environment) and the new `Startup Configuration Validation`
section logged correctly, with startup completing successfully and the
background workers still starting normally.

## Architecture Impact

None on runtime behavior. Purely additive, read-only startup diagnostics:

- No controllers, DTOs, or public API surface changed.
- No changes to recognition/queue/worker/finalization business logic.
- No new services registered in DI.
- No behavior change to any existing hard-fail configuration checks.
