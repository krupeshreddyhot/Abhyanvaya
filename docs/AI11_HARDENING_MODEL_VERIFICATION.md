# AI11.HARDENING.3 — Startup AI Model Availability Verification

**Status: IMPLEMENTED**
**Date:** 2026-07-04
**Reviewer:** Chief Software Architect

---

## 1. Purpose

Detect a missing or misconfigured InsightFace ONNX model deployment **immediately at application startup**, instead of letting the first classroom photo upload silently fail deep inside the recognition pipeline.

This is a **read-only, diagnostics-only** enhancement to the existing AI11.HARDENING.2 startup configuration summary. It does not:

- Load any ONNX model.
- Modify recognition logic, the recognition pipeline, controllers, or hosted background workers.
- Change application startup behavior — a missing model file/folder is logged as a warning and startup continues exactly as it does today.

The intent is purely operational visibility: a deployment or configuration mistake (wrong `InsightFace:ModelDirectory`, models not copied to the target environment, wrong file names) becomes visible in the very first lines of the application log, rather than surfacing later as a confusing `FileNotFoundException` thrown from `InsightFaceOnnxModelHost` the first time a teacher uploads a photo.

---

## 2. Files verified

Two files, resolved from the existing `InsightFace` configuration section (`Abhyanvaya.Infrastructure.InsightFace.InsightFaceOptions`, bound from `appsettings.json`'s `InsightFace` section) — no paths are hardcoded:

| Model | Configuration key | Default file name |
|-------|--------------------|--------------------|
| Detection Model | `InsightFace:DetectionModelFile`, combined with `InsightFace:ModelDirectory` | `det_10g.onnx` |
| Embedding Model | `InsightFace:RecognitionModelFile`, combined with `InsightFace:ModelDirectory` | `w600k_r50.onnx` |

The resolved path for each model is computed the same way `InsightFaceOnnxModelHost.EnsureLoaded` already does it:

```csharp
var path = Path.Combine(insightFaceOptions.ModelDirectory, modelFile);
```

This guarantees the diagnostic check always looks at the exact same location the real ONNX inference session will look at when a photo is actually processed — there is no separate/duplicated path-resolution logic.

---

## 3. Implementation

Added to the existing `LogStartupConfigurationSummary(WebApplication app)` local function in `Abhyanvaya.API/Program.cs` (the same method introduced for AI11.HARDENING.2) — no new method, service, hosted service, or middleware was created:

```csharp
var insightFaceOptions = app.Services.GetRequiredService<IOptions<InsightFaceOptions>>().Value;

var modelDirectoryExists = Directory.Exists(insightFaceOptions.ModelDirectory);
var detectionModelPath = Path.Combine(insightFaceOptions.ModelDirectory, insightFaceOptions.DetectionModelFile);
var embeddingModelPath = Path.Combine(insightFaceOptions.ModelDirectory, insightFaceOptions.RecognitionModelFile);
var detectionModelFound = modelDirectoryExists && File.Exists(detectionModelPath);
var embeddingModelFound = modelDirectoryExists && File.Exists(embeddingModelPath);
```

Only `Directory.Exists` and `File.Exists` are used — **no `InferenceSession` is ever constructed** and no model bytes are ever read. This makes the check effectively instantaneous and incapable of throwing an ONNX/runtime-level exception.

`IOptions<InsightFaceOptions>` was already resolved by the existing summary code (for `Recognition Pipeline Version`); this change reuses that same resolved instance rather than looking it up a second time.

---

## 4. Logging behavior

Extends the existing startup summary block, between `Recognition Pipeline Version` and `Face Matching Engine`:

**Both models found (healthy deployment):**

```
Detection Model (det_10g.onnx)      : Found
Embedding Model (w600k_r50.onnx)    : Found
```
Logged with `LogInformation`.

**Model directory exists but one or both files are missing:**

```
Detection Model (det_10g.onnx)      : MISSING (models/insightface/det_10g.onnx)
Embedding Model (w600k_r50.onnx)    : MISSING (models/insightface/w600k_r50.onnx)
```
Logged with `LogWarning`. The full resolved path is included as a structured argument to speed up diagnosis (so an operator doesn't have to separately reconstruct `ModelDirectory + file name`).

**Model directory itself is missing (the more severe misconfiguration):**

```
Model Directory                     : MISSING (models/insightface)
Detection Model                     : MISSING
Embedding Model                     : MISSING
```
All three lines logged with `LogWarning`. In this case the individual model lines omit the file name suffix, matching the required format, since the whole directory — not just an individual file — is absent.

In every case: **no exception is thrown, `LogInformation`/`LogWarning` are the only log levels used for these lines (never `LogError`/`LogCritical`), and `app.Run()` executes immediately afterward exactly as before.**

---

## 5. Why startup verification is preferred over runtime failures

- **Fail visibly, fail early:** Today, a missing model is only discovered when `InsightFaceOnnxModelHost.EnsureLoaded` first runs — which happens lazily, inside the background recognition worker, the first time a classroom photo is processed. That failure is logged as `Classroom recognition job failed` deep in `ClassroomRecognitionBackgroundService`, several layers away from the actual root cause (a deployment/config mistake), and only after a teacher has already uploaded a photo and is waiting for results.
- **Faster incident triage:** With this change, the very first thing an operator sees in the startup log (right where `BackgroundServiceExceptionBehavior`, worker status, and storage provider are already reported) tells them whether the AI models are even present, before any user-facing symptom occurs.
- **Zero risk to startup:** Because this is a pure `File.Exists`/`Directory.Exists` check with no model loading, it cannot slow down or destabilize startup, and a missing model can never prevent the API from starting — which is important because other API functionality (e.g. attendance review of already-recognized sessions, timetable management, student management) does not depend on the ONNX models at all and should remain available even if the recognition models are temporarily missing/misconfigured.
- **No duplicated failure handling:** The pipeline's own `FileNotFoundException` (thrown by `InsightFaceOnnxModelHost.EnsureLoaded` when a model is actually needed) is left completely untouched — this diagnostic is purely additive, early warning, not a replacement for that runtime guard.

---

## 6. Architecture impact

None — this is a strictly additive, read-only diagnostic:

- No new services, hosted services, or middleware were introduced (per requirement — all logic lives inside the existing `LogStartupConfigurationSummary` local function).
- No changes to `InsightFaceOnnxModelHost`, `InsightFaceEngine`, `InsightFaceDetectionService`, `ClassroomRecognitionPipeline`, controllers, or any hosted `BackgroundService`.
- No new configuration keys were introduced — the existing `InsightFace:ModelDirectory`, `InsightFace:DetectionModelFile`, and `InsightFace:RecognitionModelFile` settings are reused as-is.
- `IOptions<InsightFaceOptions>` was already registered and resolved elsewhere in the app (by `InsightFaceOnnxModelHost` and by the AI11.HARDENING.2 summary itself); no new DI registration was added.
- Application startup sequence, request pipeline, and `app.Run()` timing are unaffected; the two additional filesystem checks are effectively instant (single-directory, single-file `Exists` calls).

---

## 7. Verification steps

1. **Build:** `dotnet build Abhyanvaya.sln` (or per-project build if the solution build is blocked by a locked output DLL from an active Visual Studio debug session — see §9) — 0 compilation errors.
2. **Healthy case:** With both `det_10g.onnx` and `w600k_r50.onnx` present under the configured `InsightFace:ModelDirectory`, start the API and confirm both lines log as `Found` via `LogInformation`.
3. **Missing single file:** Temporarily rename/move `w600k_r50.onnx` out of the model directory, restart the API, and confirm:
   - `Detection Model (det_10g.onnx)      : Found` (still `LogInformation`)
   - `Embedding Model (w600k_r50.onnx)    : MISSING (...)` (as `LogWarning`)
   - The API still starts successfully and responds to non-recognition endpoints.
4. **Missing directory:** Temporarily rename the entire `InsightFace:ModelDirectory` folder, restart the API, and confirm the three `MISSING` lines (`Model Directory`, `Detection Model`, `Embedding Model`) are logged as `LogWarning`, with no exception and no startup failure.
5. **No behavior change when healthy:** Confirm application startup time, HTTP endpoint availability, and existing functionality (upload, recognition, review, finalization) are unchanged when models are present — this feature only adds log lines.
6. **Restore state:** Restore any renamed files/folders used for steps 3–4 back to their original names/locations after verification.

---

## 8. Files created/modified

| File | Change |
|------|--------|
| `Abhyanvaya.API/Program.cs` | Extended the existing `LogStartupConfigurationSummary` local function (introduced in AI11.HARDENING.2) with a read-only InsightFace model presence check (`Directory.Exists` / `File.Exists` only) and two-to-three new structured log lines (`LogInformation` when found, `LogWarning` when missing). No new usings were required — `InsightFaceOptions` was already imported for AI11.HARDENING.2. |
| `docs/AI11_HARDENING_MODEL_VERIFICATION.md` | New — this document. |

No other files were created or modified for this milestone.

---

## 9. Build status

Code compiles with **0 errors**. `dotnet build Abhyanvaya.API\Abhyanvaya.API.csproj` targeted at a fresh output directory (bypassing a stale file lock held on `bin\Debug\net8.0\*.dll` by an active Visual Studio debug session on this machine, an environment-only issue unrelated to this change) completed with `Build succeeded. 0 Error(s).` A subsequent `dotnet build Abhyanvaya.sln` against the normal `bin\Debug` output failed only with `MSB3027`/`MSB3021` copy-lock errors (no `CS` compiler errors), confirming the source change itself is correct; the copy-lock will clear once the active Visual Studio debug session holding those DLLs is stopped.
