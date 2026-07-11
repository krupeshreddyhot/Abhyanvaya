# AI14.RUNTIME.1 — Startup Diagnostics: ONNX Runtime Configuration

**Status: IMPLEMENTED**
**Date:** 2026-07-11
**Reviewer:** Chief Software Architect

---

## 1. Objective

Extend the existing startup diagnostics summary to report the actual ONNX Runtime execution
configuration (`IntraOpNumThreads`, `InterOpNumThreads`) that `InsightFaceOnnxModelHost` applies to
`SessionOptions` when it loads the InsightFace models.

This is an **observability enhancement only**:

- AI recognition behavior is unchanged.
- Model loading logic (`InsightFaceOnnxModelHost.EnsureLoaded`) is untouched by this task — it was
  already updated (with the same `IntraOpNumThreads`/`InterOpNumThreads` values used here) as part of
  the prior OOM-mitigation work, before this diagnostics task began.
- No `SessionOptions` behavior changes as part of this task.

---

## 2. Before / after

**Before:**

```
Detection Model (det_10g.onnx)      : Found (17.4 MB)
Embedding Model (w600k_r50.onnx)    : Found (123.6 MB)
Tenant Mode                         : Multi Tenant
```

**After:**

```
Detection Model (det_10g.onnx)      : Found (17.4 MB)
Embedding Model (w600k_r50.onnx)    : Found (123.6 MB)
ONNX Runtime
  IntraOp Threads                     : 1
  InterOp Threads                     : 1
Tenant Mode                         : Multi Tenant
```

**When a thread-count key is absent from configuration** (i.e. the C# default on
`InsightFaceOptions` is what's actually in effect), the missing-default is called out explicitly
instead of silently showing a bare number:

```
ONNX Runtime
  IntraOp Threads                     : 1 (default — not set in configuration)
  InterOp Threads                     : 1 (default — not set in configuration)
```

---

## 3. Implementation

`Abhyanvaya.API/Program.cs`:

```623:641:Abhyanvaya.API/Program.cs
static void LogOnnxRuntimeThreadConfiguration(ILogger logger, IConfiguration configuration, InsightFaceOptions insightFaceOptions)
{
    var intraOpConfigured = !string.IsNullOrWhiteSpace(configuration[$"{InsightFaceOptions.SectionName}:IntraOpNumThreads"]);
    var interOpConfigured = !string.IsNullOrWhiteSpace(configuration[$"{InsightFaceOptions.SectionName}:InterOpNumThreads"]);

    logger.LogInformation("ONNX Runtime");
    LogOnnxThreadSetting(logger, "IntraOp Threads", insightFaceOptions.IntraOpNumThreads, intraOpConfigured);
    LogOnnxThreadSetting(logger, "InterOp Threads", insightFaceOptions.InterOpNumThreads, interOpConfigured);
}

static void LogOnnxThreadSetting(ILogger logger, string label, int threadCount, bool explicitlyConfigured)
{
    if (explicitlyConfigured)
    {
        logger.LogInformation("  {Label}                     : {ThreadCount}", label, threadCount);
    }
    else
    {
        logger.LogInformation("  {Label}                     : {ThreadCount} (default — not set in configuration)", label, threadCount);
    }
}
```

Called immediately after the existing model-file diagnostics, inside `LogStartupConfigurationSummary`:

```519:519:Abhyanvaya.API/Program.cs
    LogOnnxRuntimeThreadConfiguration(logger, app.Configuration, insightFaceOptions);
```

### Design notes (mapped to constraints)

- **No duplicate configuration reading.** `insightFaceOptions` is the exact same
  `IOptions<InsightFaceOptions>.Value` instance already resolved earlier in
  `LogStartupConfigurationSummary` (used for `PipelineVersion`, model paths, etc.) — it is passed in,
  not re-bound. Only a single extra raw `IConfiguration[...]` lookup is added, and solely to detect
  whether the key was explicitly present (for the "(default)" annotation); it does not duplicate the
  options binding.
- **Never hardcoded.** The thread counts logged are `insightFaceOptions.IntraOpNumThreads` /
  `InterOpNumThreads` — the same properties `InsightFaceOnnxModelHost.EnsureLoaded` reads to build
  `SessionOptions`. If those properties' defaults or configured values ever change, this log changes
  with them automatically.
- **Missing configuration → defaults are logged.** `InsightFaceOptions.IntraOpNumThreads` /
  `InterOpNumThreads` both default to `1` via C# property initializers, so IOptions binding already
  falls back to `1` when the key is absent. This method additionally detects that absence (via a raw
  `IConfiguration` lookup) and appends `(default — not set in configuration)` so operators can tell a
  deliberately-configured value apart from the built-in default at a glance.
- **Reuses the existing diagnostics framework.** No new diagnostics class was introduced; the new
  helper lives alongside `LogWorkerStatus`/`LogModelFileStatus` in `Program.cs` and follows their exact
  label/indentation/logging conventions (see AI12.OBS.1/AI12.OBS.5).
- **No inference or `SessionOptions` changes.** This method only calls `ILogger.LogInformation` — it
  never touches `InsightFaceOnnxModelHost`, `InferenceSession`, or `SessionOptions`, and cannot affect
  or delay model loading (it runs after model *file* diagnostics, which are themselves read-only
  `File.Exists`/`FileInfo.Length` checks, per AI12.OBS.5).
- **Backward compatible.** Purely additive log lines; no existing log line, config key, or health
  endpoint response shape changed.

---

## 4. Configuration reference

`Abhyanvaya.API/appsettings.json` (`InsightFace` section):

```json
"InsightFace": {
  ...
  "IntraOpNumThreads": 1,
  "InterOpNumThreads": 1
}
```

Both keys were introduced in the prior OOM-mitigation change to `InsightFaceOptions` /
`InsightFaceOnnxModelHost` (capping ONNX Runtime's thread usage on memory-constrained hosts). This
task only adds visibility into the values already in effect; it does not introduce or change these
configuration keys.

---

## 5. Verification steps

1. **Build:** `dotnet build Abhyanvaya.sln` — 0 errors.
2. **Default case:** with no `IntraOpNumThreads`/`InterOpNumThreads` keys in `appsettings.json`,
   confirm the startup log shows `1 (default — not set in configuration)` for both.
3. **Explicit case:** set `InsightFace:IntraOpNumThreads` to e.g. `2` in configuration, restart, and
   confirm the log shows `2` with no `(default...)` suffix, while `InterOp Threads` (still unset)
   continues to show the default annotation.
4. **No behavior change:** confirm via code review that `LogOnnxRuntimeThreadConfiguration` and
   `LogOnnxThreadSetting` only call `ILogger` methods — no `SessionOptions`, `InferenceSession`, or
   `InsightFaceOnnxModelHost` code path is touched.

---

## 6. Files created/modified

| File | Change |
|------|--------|
| `Abhyanvaya.API/Program.cs` | Added `LogOnnxRuntimeThreadConfiguration`/`LogOnnxThreadSetting` helpers; startup summary now logs an `ONNX Runtime` section (`IntraOp Threads`, `InterOp Threads`) after the model diagnostics lines. |

---

## 7. Build status

`dotnet build Abhyanvaya.sln` — **Build succeeded**, 0 errors.

## 8. Acceptance criteria

- ✅ Startup diagnostics extended with an `ONNX Runtime` section after the Recognition Engine/model
  lines, matching the requested `IntraOp Threads` / `InterOp Threads` fields.
- ✅ Values read from the existing `InsightFaceOptions` configuration — never hardcoded.
- ✅ Missing configuration is called out and the default value in effect is logged.
- ✅ No inference logic, `SessionOptions`, or model loading behavior changed.
- ✅ No duplicated configuration reading — reuses the already-resolved `IOptions<InsightFaceOptions>`.
- ✅ Build succeeds; backward compatible (additive log lines only).
