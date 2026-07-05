# AI11.AI.1 — InsightFace ONNX Model Loading Review

**Type:** Read-only investigation. No code, thresholds, or behaviour changed.
**Verification method:** static code trace + live inspection of the actual `.onnx` files with the `onnx` Python library (v1.22.0). No application code was modified.

---

## 1. Startup / load sequence (as coded)

```
IFaceDetectionService  ─►  InsightFaceDetectionService
        │                         (DI: AddScoped<IFaceDetectionService, InsightFaceDetectionService>)
        ▼
InsightFaceEngine.DetectAsync()
        ▼
InsightFaceEngine.DetectFaces(image)
        ▼
InsightFaceOnnxModelHost.GetDetectionSession()   ── EnsureLoaded(det_10g.onnx)
        ▼
new Microsoft.ML.OnnxRuntime.InferenceSession(path)     ← detection model
        …
InsightFaceEngine.ExtractEmbedding(aligned)
        ▼
InsightFaceOnnxModelHost.GetRecognitionSession()  ── EnsureLoaded(w600k_r50.onnx)
        ▼
new InferenceSession(path)                              ← recognition model
```

Loading is **lazy** and thread-safe (double-checked lock). Sessions are created on first
detection/embedding call, not at startup:

```34:60:Abhyanvaya.Infrastructure/InsightFace/InsightFaceOnnxModelHost.cs
    private void EnsureLoaded(ref InferenceSession? session, string modelFile, string label)
    {
        if (session != null)
        {
            return;
        }

        lock (_gate)
        {
            if (session != null)
            {
                return;
            }

            var path = Path.Combine(_options.ModelDirectory, modelFile);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"InsightFace {label} model not found at '{path}'. " +
                    "Place ONNX models under InsightFace:ModelDirectory and configure appsettings.",
                    path);
            }

            session = new InferenceSession(path);
            _logger.LogInformation("InsightFace {Label} ONNX model loaded from {Path}", label, path);
        }
    }
```

---

## 2. Which physical files are loaded

Resolved from configuration (`appsettings.json` → `InsightFace` section, bound to `InsightFaceOptions`):

| Setting | Value |
|---------|-------|
| `ModelDirectory` | `models/insightface` |
| `DetectionModelFile` | `det_10g.onnx` |
| `RecognitionModelFile` | `w600k_r50.onnx` |

The host builds the path as `Path.Combine(ModelDirectory, file)` — i.e. **relative to the process
current working directory** (`Environment.CurrentDirectory`).

> ⚠️ **Path-resolution divergence (advisory, not the current failure):** the *diagnostics* layer
> (`Program.cs` / `ModelPathResolver`) resolves the same directory against `ContentRootPath`, but the
> *actual model host* uses the raw relative path against the CWD. When launched via `dotnet run` or
> Visual Studio the CWD equals the project directory, so both agree and loading succeeds. If the
> compiled `.exe` is launched from another working directory (e.g. `bin/Debug/net8.0`), the health
> check could still pass while the model host fails to find the file. Flagged for hardening; **not**
> the cause of the detection problem.

### Full physical paths & sizes (verified on disk)

| Model | Full path | Size (bytes) | Size |
|-------|-----------|--------------|------|
| Detection | `D:\Resheta\AttendenceProject\Abhyanvaya\Abhyanvaya.API\models\insightface\det_10g.onnx` | **16,923,827** | ~16.1 MiB |
| Recognition | `D:\Resheta\AttendenceProject\Abhyanvaya\Abhyanvaya.API\models\insightface\w600k_r50.onnx` | **174,383,860** | ~166 MiB |

Both sizes match the official InsightFace **buffalo_l** pack.

---

## 3–7. Model metadata (verified from the actual files)

### det_10g.onnx (SCRFD detector)

- IR version: 6 · Producer: pytorch 1.6 · Opset: ai.onnx v11

**Input**

| Idx | Name | Type / shape |
|-----|------|--------------|
| 0 | `input.1` | `float32[1, 3, ?, ?]` (dynamic H/W; app feeds `1×3×640×640`) |

**Outputs (graph order)** — 9 tensors:

| Idx | Name | Shape | Meaning (official SCRFD) | Stride |
|-----|------|-------|--------------------------|--------|
| 0 | `448` | `float32[12800, 1]`  | scores | 8 |
| 1 | `471` | `float32[3200, 1]`   | scores | 16 |
| 2 | `494` | `float32[800, 1]`    | scores | 32 |
| 3 | `451` | `float32[12800, 4]`  | bbox distances | 8 |
| 4 | `474` | `float32[3200, 4]`   | bbox distances | 16 |
| 5 | `497` | `float32[800, 4]`    | bbox distances | 32 |
| 6 | `454` | `float32[12800, 10]` | 5 landmarks (x,y) | 8 |
| 7 | `477` | `float32[3200, 10]`  | 5 landmarks (x,y) | 16 |
| 8 | `500` | `float32[800, 10]`   | 5 landmarks (x,y) | 32 |

**Anchor math confirmation (640×640 input):**

| Stride | Grid | Cells | × anchors | Rows |
|--------|------|-------|-----------|------|
| 8 | 80×80 | 6,400 | ×2 | **12,800** |
| 16 | 40×40 | 1,600 | ×2 | **3,200** |
| 32 | 20×20 | 400 | ×2 | **800** |

⇒ **2 anchors per spatial cell**, and outputs are **grouped by type** (all scores, then all bboxes,
then all landmarks) — *not* interleaved per stride. This is the crux analysed in AI11.AI.2.

### w600k_r50.onnx (ArcFace recognizer)

- IR version: 6 · Producer: pytorch 1.9 · Opset: ai.onnx v11

**Input**

| Idx | Name | Type / shape |
|-----|------|--------------|
| 0 | `input.1` | `float32[None, 3, 112, 112]` (dynamic batch; app feeds `1×3×112×112`) |

**Output**

| Idx | Name | Shape | Meaning |
|-----|------|-------|---------|
| 0 | `683` | `float32[1, 512]` | 512-D face embedding (L2-normalized by the app) |

Matches `RecognitionInputSize = 112` and `ExpectedEmbeddingDimension = 512`.

---

## 8. Is `det_10g.onnx` actually loaded?

**Yes.** The file exists at the resolved path, matches the official size (16,923,827 bytes), and is a
valid SCRFD ONNX graph (9 outputs, 2 anchors/stride). It is opened via `new InferenceSession(path)` on
the first detection call. *(Note: correctly loaded ≠ correctly parsed — see AI11.AI.2.)*

## 9. Is `w600k_r50.onnx` actually loaded?

**Yes.** File present (174,383,860 bytes), valid ArcFace graph, input `1×3×112×112`, output `[1,512]`.

## 10. Fallback model?

**None.** There is no fallback, no secondary path, no bundled default. If either file is missing,
`EnsureLoaded` throws `FileNotFoundException` and detection/recognition aborts — no silent substitute
model is used.

---

## Summary

| Question | Finding |
|----------|---------|
| Files loaded | `det_10g.onnx`, `w600k_r50.onnx` from `…\Abhyanvaya.API\models\insightface\` |
| Sizes | 16,923,827 / 174,383,860 bytes (official buffalo_l) |
| Detection I/O | in `input.1 [1,3,?,?]`; out 9 tensors grouped by type (scores/bbox/kps × strides 8,16,32) |
| Recognition I/O | in `input.1 [None,3,112,112]`; out `[1,512]` |
| det_10g loaded | ✅ Yes |
| w600k_r50 loaded | ✅ Yes |
| Fallback | ❌ None (throws if missing) |

**Both models load correctly. The detection *output parsing* does not match this model layout — see
`AI11_AI2_OUTPUT_PARSING.md`.**
