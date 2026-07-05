# AI12.OBS.10 — Portable Model Path Resolution

## Purpose

Stop merely *warning* about a relative `InsightFace:ModelDirectory` and
instead **resolve it correctly**, so the diagnostic checks (startup summary,
`/health`, `/health/ready`, and `ConfigurationValidator`) always look in the
same, deterministic location regardless of the process's current working
directory.

## The Problem

```json
"InsightFace": {
  "ModelDirectory": "models/insightface"
}
```

A relative path like this is resolved against the process's **current
working directory**, which is not the same thing as the application's
content root, and varies significantly across hosting models:

| Host | Typical working directory |
|---|---|
| `dotnet run` | Project directory (usually matches content root) |
| Visual Studio (F5) | Project directory (usually matches content root) |
| IIS / IIS Express | Can be the IIS worker process directory, not the app folder |
| Windows Service | Often `C:\Windows\System32` unless explicitly set |
| Docker / Linux container | Whatever `WORKDIR` the image/entrypoint sets — may or may not equal the app's install path |

Previously, `AI12.OBS.7`'s validator could only detect this risk and warn
about it ("this is a relative path, it might break elsewhere"). It could not
fix it, and the actual `Directory.Exists` / `File.Exists` check in
`ModelAvailabilityChecker` still used the raw, unresolved value — so the
diagnostic itself was subject to the same ambiguity it was warning about.

## The Fix

A new shared helper, `ModelPathResolver.Resolve`, anchors relative model
directories to `IHostEnvironment.ContentRootPath` — the one working-directory
concept ASP.NET Core computes consistently and correctly across every hosting
model above:

```csharp
public static class ModelPathResolver
{
    public static string Resolve(string configuredModelDirectory, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(configuredModelDirectory))
            return configuredModelDirectory;

        return Path.IsPathRooted(configuredModelDirectory)
            ? configuredModelDirectory
            : Path.Combine(environment.ContentRootPath, configuredModelDirectory);
    }
}
```

- **Absolute paths are used unchanged** — no behavior change for deployments
  that already configure an absolute `InsightFace:ModelDirectory`.
- **Relative paths are fully supported** (not discouraged) — they are simply
  resolved deterministically instead of depending on an ambient working
  directory.

### Example

| Configured | Content Root | Resolved |
|---|---|---|
| `models/insightface` | `C:\Program Files\Abhyanvaya` | `C:\Program Files\Abhyanvaya\models\insightface` |
| `C:\Models\InsightFace` | *(any)* | `C:\Models\InsightFace` (unchanged — already absolute) |

## Where It's Used

`ModelAvailabilityChecker.Check(InsightFaceOptions options, IHostEnvironment environment)`
now calls `ModelPathResolver.Resolve` once, and performs every subsequent
`Directory.Exists`/`File.Exists`/`FileInfo.Length` check against the
**resolved** path. `ModelAvailabilityReport` now exposes both:

```csharp
public sealed record ModelAvailabilityReport(
    string ConfiguredModelDirectory,   // raw appsettings value, e.g. "models/insightface"
    string ResolvedModelDirectory,     // absolute path actually checked on disk
    bool ModelDirectoryExists,
    ModelFileStatus Detection,
    ModelFileStatus Embedding,
    string PipelineVersion);
```

This single helper is used by every consumer that previously called
`ModelAvailabilityChecker.Check(...)`, so there is exactly one place the
resolution logic lives:

- The startup configuration summary (`Program.cs` →
  `LogStartupConfigurationSummary`)
- `ConfigurationValidator.ValidateModelDirectory` (via the shared
  `ModelAvailabilityReport` passed in)
- `GET /health`
- `GET /health/ready`

### Startup Logging

```
Configured Model Directory           : models/insightface
Resolved Model Directory             : C:\Program Files\Abhyanvaya\models\insightface (MISSING)
```

(The second line only gets a `(MISSING)` suffix and `LogWarning` level when
the resolved directory doesn't exist — matching the existing pattern used
elsewhere in the startup summary.)

### Configuration Validator Behavior Change

Because relative paths are now handled correctly, the old "relative path is a
portability risk" `ConfigurationIssue` (from `AI12.OBS.7`) has been removed —
it's no longer an issue, since the path is always resolved deterministically.
`ConfigurationValidator.ValidateModelDirectory` now only reports a
**Critical** issue when the *resolved* directory genuinely doesn't exist on
disk, citing both the configured and resolved values in the message:

```
[Critical]
  Category           : AI
  Configuration Key  : InsightFace:ModelDirectory
  Message            : Configured model directory 'models/insightface' (resolved to
                        'C:\Program Files\Abhyanvaya\models\insightface') does not exist on disk.
  Suggested Fix      : Create the directory and place det_10g.onnx and w600k_r50.onnx
                        inside it, or correct InsightFace:ModelDirectory to point at the
                        correct location.
```

## Why `ContentRootPath` (Not `BaseDirectory` Or The Working Directory)

- `IHostEnvironment.ContentRootPath` is exactly the directory ASP.NET Core
  itself considers "where this application lives" — it's set explicitly by
  the host builder (defaulting to the directory containing the entry
  assembly, but overridable via `UseContentRoot`/`WEBROOT`/`--contentRoot`),
  and is stable regardless of how the process was launched or what its
  current working directory happens to be.
- `AppContext.BaseDirectory` (the DLL's own folder) is a reasonable
  alternative and would behave almost identically for this app's deployment
  shape, but `ContentRootPath` is the officially-recommended, environment-
  aware anchor for "where are this app's own files" in ASP.NET Core, and is
  already used elsewhere in this codebase (`ResolveLocalMediaPhysicalRoot` in
  `Program.cs` uses the analogous `WebRootPath`/`ContentRootPath` pattern for
  media storage), so this keeps path-resolution conventions consistent
  across the app.

## Deployment Portability

With this change, the exact same `appsettings.json` value
(`"ModelDirectory": "models/insightface"`) now resolves correctly and
identically whether the application is:

- Run via `dotnet run` or debugged in Visual Studio,
- Published and hosted behind IIS,
- Installed and run as a Windows Service,
- Or containerized and run under Docker/Linux —

as long as the ONNX model files are physically deployed under
`<content root>/models/insightface`, which is exactly where a normal
`dotnet publish` output (or a Docker image `COPY`) would place them relative
to the application's own files.

## Scope Note: Actual Model Loading Is Unchanged

`InsightFaceOnnxModelHost.EnsureLoaded` (the code that actually loads the
ONNX models for inference) still does its own
`Path.Combine(_options.ModelDirectory, modelFile)` using the raw configured
value, **unchanged** — this milestone explicitly excludes AI recognition
changes. In practice this is a non-issue for this application's current
deployment shape (its working directory already matches its content root in
every environment it currently runs in), but it does mean the *diagnostic*
layer (this change) and the *real* model loader are not yet using the exact
same resolution helper. This is called out here as a known, intentionally
out-of-scope follow-up should model loading ever need the same portability
guarantee — a future milestone could have `InsightFaceOnnxModelHost` resolve
its path via `ModelPathResolver` too.

## Files Modified / Created

| File | Change |
|---|---|
| `Abhyanvaya.API/Diagnostics/ModelPathResolver.cs` | **New.** Single shared `Resolve(string, IHostEnvironment)` helper. |
| `Abhyanvaya.API/Diagnostics/ModelAvailabilityChecker.cs` | `Check` now takes `IHostEnvironment` and resolves via `ModelPathResolver`; `ModelAvailabilityReport` now exposes `ConfiguredModelDirectory` + `ResolvedModelDirectory` (previously a single `ModelDirectory` field). |
| `Abhyanvaya.API/Diagnostics/ConfigurationValidator.cs` | `ValidateModelDirectory` updated for the renamed fields; removed the now-obsolete "relative path" issue; "directory does not exist" message now cites both configured and resolved paths. |
| `Abhyanvaya.API/Program.cs` | All three call sites of `ModelAvailabilityChecker.Check(...)` (startup summary, `/health`, `/health/ready`) now pass `app.Environment`; startup log and `/health` snapshot updated to show both configured and resolved directories. |
| `docs/AI12_OBS10_MODEL_PATH_RESOLUTION.md` | **New.** This document. |

## Build Status

`dotnet build Abhyanvaya.sln` — **Build succeeded, 0 errors** (pre-existing
nullable-reference/NuGet-advisory warnings only).

Runtime-verified: ran the compiled binary standalone from a different working
directory than its own output folder; confirmed `Resolved Model Directory`
in the startup log, and `modelDirectory.configured` / `modelDirectory.resolved`
in the `/health` JSON response, both point at the correct, content-root-anchored
absolute path.

## Architecture Impact

None on runtime AI recognition behavior (explicitly out of scope and
untouched). Purely a diagnostics-accuracy and deployment-portability
improvement:

- No new services registered in DI.
- No controller, DTO, or public API surface changed beyond the additive
  `modelDirectory` field already covered under `AI12.OBS.8`'s "expose new
  metadata" allowance.
- `ModelAvailabilityChecker.Check` signature changed (added an
  `IHostEnvironment` parameter) — all three call sites in this codebase were
  updated; this is an internal diagnostics API, not a public contract.
