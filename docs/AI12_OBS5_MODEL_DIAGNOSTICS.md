# AI12.OBS.5 — AI Model Diagnostics

**Status: IMPLEMENTED**
**Date:** 2026-07-04
**Reviewer:** Chief Software Architect

---

## 1. Objective

Extend the AI11.HARDENING.3 model presence check with file size information, so operators can distinguish "model present but truncated/corrupted download" from "model fully present" without loading the model — and expose the model directory path unconditionally (not only when missing).

Models are still **never loaded** — only `File.Exists()` and `FileInfo.Length` are used.

---

## 2. Before / after

**Before (AI11.HARDENING.3):**

```
Detection Model (det_10g.onnx)      : Found
Embedding Model (w600k_r50.onnx)    : Found
```
(`Model Directory` was only logged when the directory itself was missing.)

**After (AI12.OBS.5):**

```
Model Directory                     : C:\Models\InsightFace
Detection Model (det_10g.onnx)      : Found (17.4 MB)
Embedding Model (w600k_r50.onnx)    : Found (123.6 MB)
```

**Missing case:**

```
Detection Model (det_10g.onnx)      : Missing (0 MB)
Embedding Model (w600k_r50.onnx)    : Missing (0 MB)
```

**Missing directory case:**

```
Model Directory                     : C:\Models\InsightFace (MISSING)
Detection Model (det_10g.onnx)      : Missing (0 MB)
Embedding Model (w600k_r50.onnx)    : Missing (0 MB)
```

---

## 3. Implementation

New shared file `Abhyanvaya.API/Diagnostics/ModelAvailabilityChecker.cs`:

```csharp
public sealed record ModelFileStatus(string FileName, string FullPath, bool Found, long SizeBytes)
{
    public double SizeMegabytes => Math.Round(SizeBytes / (1024d * 1024d), 1);
}

public sealed record ModelAvailabilityReport(
    string ModelDirectory, bool ModelDirectoryExists,
    ModelFileStatus Detection, ModelFileStatus Embedding, string PipelineVersion)
{
    public bool AllModelsPresent => ModelDirectoryExists && Detection.Found && Embedding.Found;
}

public static class ModelAvailabilityChecker
{
    public static ModelAvailabilityReport Check(InsightFaceOptions options)
    {
        var directoryExists = Directory.Exists(options.ModelDirectory);
        var detection = BuildStatus(options.ModelDirectory, options.DetectionModelFile, directoryExists);
        var embedding = BuildStatus(options.ModelDirectory, options.RecognitionModelFile, directoryExists);
        return new ModelAvailabilityReport(options.ModelDirectory, directoryExists, detection, embedding, options.PipelineVersion);
    }

    private static ModelFileStatus BuildStatus(string modelDirectory, string fileName, bool directoryExists)
    {
        var fullPath = Path.Combine(modelDirectory, fileName);
        if (!directoryExists || !File.Exists(fullPath))
        {
            return new ModelFileStatus(fileName, fullPath, Found: false, SizeBytes: 0);
        }

        return new ModelFileStatus(fileName, fullPath, Found: true, SizeBytes: new FileInfo(fullPath).Length);
    }
}
```

- **Only `File.Exists` and `FileInfo.Length`** are used — reading a file's length from its directory entry does not open, map, or parse the file contents, so this remains a metadata-only check with no risk of triggering ONNX Runtime initialization or any I/O beyond a filesystem stat.
- Reuses the exact same `ModelDirectory` + `Path.Combine` resolution as `InsightFaceOnnxModelHost.EnsureLoaded` (unchanged from AI11.HARDENING.3), so the diagnostic always reports on the same path the real inference session would use.
- Extracted into its own static class specifically so it is **not duplicated** between the startup log (`Program.cs`) and the new `/health` and `/health/ready` endpoints (AI12.OBS.6) — both now call `ModelAvailabilityChecker.Check(...)` and get identical results.

`Program.cs`'s startup summary now always logs the `Model Directory` line (previously only logged when missing), and each model line includes the formatted size in MB (rounded to 1 decimal place), or `0 MB` when missing — exactly matching the milestone's required format.

---

## 4. Architecture impact

- No model loading was added — `InsightFaceOnnxModelHost` (the only place that actually constructs an `InferenceSession`) is completely untouched.
- `ModelAvailabilityChecker` has no dependency on ONNX Runtime, `InsightFaceEngine`, or the recognition pipeline — it only depends on `InsightFaceOptions` (configuration) and `System.IO`.
- Reused by both the startup summary and the AI12.OBS.6 health endpoints, eliminating the risk of the two surfaces disagreeing about model availability.

---

## 5. Verification steps

1. **Build:** `dotnet build Abhyanvaya.sln` — 0 errors.
2. **Healthy case:** with both ONNX files present, confirm the startup log shows `Found (X.X MB)` for each, with sizes matching the actual files' sizes on disk (spot-check with `Get-Item <path> | Select Length`).
3. **Missing file case:** temporarily rename one model file, restart, and confirm `Missing (0 MB)` is logged for that file (as a warning), while the other still reports `Found (...)`.
4. **Missing directory case:** temporarily rename the whole model directory, restart, and confirm `Model Directory : <path> (MISSING)` plus `Missing (0 MB)` for both models.
5. **No model loading:** confirm via code review that `ModelAvailabilityChecker` never constructs `Microsoft.ML.OnnxRuntime.InferenceSession` — only `Directory.Exists`, `File.Exists`, and `new FileInfo(path).Length` are used.
6. **Consistency:** confirm `/health` (AI12.OBS.6) reports the exact same `Found`/`Missing` and size values as the startup log for the same on-disk state (both call the same `ModelAvailabilityChecker.Check(...)`).

---

## 6. Files created/modified

| File | Change |
|------|--------|
| `Abhyanvaya.API/Diagnostics/ModelAvailabilityChecker.cs` | New — `ModelFileStatus`/`ModelAvailabilityReport` records + `Check(...)`, using `File.Exists`/`FileInfo.Length` only. |
| `Abhyanvaya.API/Program.cs` | Startup summary now always logs `Model Directory`, and each model line includes size in MB (`Found (X.X MB)` / `Missing (0 MB)`). |

---

## 7. Build status

`dotnet build Abhyanvaya.sln` — **Build succeeded**, 0 errors.

## 8. Acceptance criteria

- ✅ Build succeeds.
- ✅ No model loading — `File.Exists()` / `FileInfo.Length` only.
- ✅ Missing files/directory report `Missing` / `0 MB` as specified.
