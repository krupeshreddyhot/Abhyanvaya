# AI15.DIAGNOSTICS.1 — Recognition Pipeline Memory Forensics

**Status: IMPLEMENTED (diagnostics only — no behavior change)**
**Date:** 2026-07-11
**Reviewer:** Chief Software Architect

---

## 1. Objective

Render's Starter plan (512 MB) still OOM-restarts mid-job during **classroom recognition** (embedding
works fine; the failure is isolated to the classroom pipeline). This milestone does **not** change any
recognition algorithm, threshold, matching logic, model, database schema, or queue architecture — it
adds a lightweight, always-on forensics layer so the *next* investigation can answer, from logs alone:

- Which stage was executing when memory peaked?
- Which face (of how many) was being processed?
- How much did each stage add to Working Set/Managed Heap/Private Bytes?
- Did the process die before or after a specific `IDisposable` was created/disposed?

---

## 2. Instrumentation architecture

```
ClassroomRecognitionBackgroundService
  └─ creates one DI scope per dequeued message
       ├─ IRecognitionPipelineDiagnostics  (Scoped — one instance per job)
       ├─ ClassroomRecognitionPipeline     (Scoped)
       └─ InsightFaceEngine                (Scoped)
```

- **`IRecognitionPipelineDiagnostics`** (`Abhyanvaya.Infrastructure/Diagnostics/`) is registered
  **Scoped**, so exactly one instance backs one classroom-recognition job — the same DI scope the
  background worker already creates per dequeued message. No locking is needed: a job is always
  processed end-to-end on a single logical call chain.
- **Inert until `Begin(...)` is called.** `ClassroomRecognitionPipeline.ProcessAsync` is the only
  caller of `Begin`. Every other method on the interface is a true no-op (no `Process`/`GC` reads, no
  allocation, no log line) until `Begin` has run on that instance. This is what makes it safe to also
  instrument `InsightFaceEngine`'s **private** helpers (`DetectFaces`, `ExtractEmbedding` — shared by
  both classroom recognition and student face embedding) for Task 6's tensor/ORT-value lifecycle
  logging: the student embedding pipeline (`InsightFaceEmbeddingGenerator` → `GenerateSingleFaceEmbedding`)
  runs in its **own** DI scope that never calls `Begin`, so those same log calls silently no-op there —
  zero observable effect on the one pipeline that is explicitly known to work correctly.
- **`IRecognitionDiagnosticsStore`** (Singleton) holds only the *last completed or failed* job's
  summary, so `/health`/`/health/ready` can read it after the per-job scope (and its
  `IRecognitionPipelineDiagnostics` instance) has already been disposed.
- **Fail-safe by construction.** Every public method on `RecognitionPipelineDiagnostics` is wrapped in
  a try/catch that swallows and logs a single internal warning — a bug in diagnostics code can never
  throw into, or change the timing of, the actual recognition pipeline.
- **`RecognitionDiagnostics:Enabled`** (`appsettings.json`) is a master kill-switch if the added log
  volume ever needs to be turned off without a redeploy. Recognition behavior is identical either way.

### Stage diagram

```
ClassroomRecognitionPipeline.ProcessAsync
 ├─ Recognition Started                         (Begin)
 ├─ Load Image            [Started/Finished]     ← fetch bytes from storage
 ├─ InsightFaceEngine.DetectAsync
 │    ├─ Decode Image      [Started/Finished]    ← Image.Load<Rgb24>
 │    ├─ Face Detection    [Started/Finished]    ← SCRFD (det_10g.onnx) + NMS
 │    └─ for each detected face (1..N):
 │         ├─ Face Cropping     [Started/Finished]   ("Cropping Face i")   ← 5-pt alignment
 │         ├─ Embedding Generation [Started/Finished] ("Embedding Face i") ← ArcFace (w600k_r50.onnx)
 │         └─ Dispose Complete (Face i)              ← aligned Image<Rgb24> about to be disposed
 ├─ Matching               [Started/Finished]    ← batched cosine-distance match, all faces at once
 │    └─ Matching (Face i)  for i in 1..N        ← per-face reporting of the batch result above
 ├─ Database Save          [Started/Finished]    ← persist AttendanceRecognition rows + status
 └─ Recognition Completed  (Complete)             → Memory Summary + Timing Summary printed
```

`Load Image`, `Recognition Started/Completed`, `Matching`, and `Database Save` are logged from
`ClassroomRecognitionPipeline`. `Decode Image`, `Face Detection`, `Face Cropping`, and `Embedding
Generation` are logged from `InsightFaceEngine.DetectAsync` — both classes share the one scoped
`IRecognitionPipelineDiagnostics` instance for the job, so all stages land in a single, correctly
ordered timeline per `AttendanceSessionId`.

### Design choices worth calling out

- **Per-face "Matching" is reported, not re-computed.** `IFaceMatcher.Match(...)` is called exactly
  once, unmodified, for the whole batch (matching logic/thresholds are completely untouched). The
  pipeline then loops over the *already-computed* results purely to log one entry per face — this
  satisfies Task 3's "per-face Matching" requirement without adding a second matching pass or touching
  `FaceMatcher` at all.
- **"Dispose Complete" is logged immediately before, not inside, the implicit dispose.** The codebase
  uses `using var aligned = ...;` inside the per-face loop (disposing at the end of each loop
  iteration) and `using var image = ...;` at the top of `DetectAsync` (disposing at method exit). To
  satisfy "do not change lifetime, only log," the diagnostics calls are inserted as plain, additional
  statements immediately before each implicit dispose fires — never by restructuring the `using`
  itself into a different disposal mechanism.
- **Object lifecycle logs (Task 6) intentionally skip the full memory snapshot.** `ObjectCreated`/
  `ObjectDisposed` fire far more often than stage boundaries (once per tensor/ORT value per face), so
  they are single, cheap log lines with no `Process`/`GC` read — this is what keeps total overhead
  within the < 2% CPU / < 1 MB budget (Task 12) regardless of face count.

---

## 3. `RecognitionPipelineDiagnostics` (Task 1)

`RecognitionMemorySnapshot.Capture()` — the only place any memory/GC state is actually read — uses
exactly the four APIs the task specifies, nothing else, no third-party packages:

```csharp
public static RecognitionMemorySnapshot Capture()
{
    var managedHeapBytes = GC.GetTotalMemory(false);   // no forced collection
    var workingSetBytes = Environment.WorkingSet;

    long privateBytes;
    using (var process = Process.GetCurrentProcess())
    {
        privateBytes = process.PrivateMemorySize64;
    }

    return new RecognitionMemorySnapshot(
        TimestampUtc: DateTime.UtcNow,
        ManagedHeapBytes: managedHeapBytes,
        WorkingSetBytes: workingSetBytes,
        PrivateBytes: privateBytes,
        Gen0Collections: GC.CollectionCount(0),
        Gen1Collections: GC.CollectionCount(1),
        Gen2Collections: GC.CollectionCount(2),
        ThreadId: Environment.CurrentManagedThreadId);
}
```

`Process.GetCurrentProcess()` is disposed immediately after reading `PrivateMemorySize64` to avoid
leaking a native handle per snapshot.

---

## 4. Logging format (Tasks 2, 3, 4, 10)

Every stage boundary and per-face event goes through one structured, boxed log entry that follows the
existing startup-diagnostics "Label : Value" convention:

```
====================================================
Recognition Pipeline Diagnostics
  Stage                              : Embedding Face 2 Finished
  Face                               : 2 of 5
  Managed Heap                       : 188 MB
  Working Set                        : 411 MB
  Private Memory                     : 462 MB
  Delta                              : +73 MB
  Elapsed                            : 214 ms
  Thread Id                          : 9
  GC Gen0/Gen1/Gen2                  : 41/6/1
====================================================
```

- `Stage` always contains the literal boundary text checked off in Task 2/3 (e.g. `"Face Detection
  Started"`, `"Face Detection Finished"`, `"Cropping Face 3 Started"`, `"Matching Face 3"`, `"Dispose
  Complete Face 3"`, `"Recognition Started"`, `"Recognition Completed"`).
- `Delta` is the Working Set change versus the immediately preceding log entry (any stage/face), not a
  running total — this is what makes it possible to attribute a memory jump to one specific stage.
- `Elapsed` is milliseconds since `Begin()` (job start), from a single `Stopwatch`, not `DateTime`
  subtraction — avoids clock-resolution noise.
- Object lifecycle events (Task 6) use a lighter one-line form with no memory snapshot:
  `"ImageSharp Image Created (aligned face 3)"` / `"ImageSharp Image Disposed (aligned face 3)"`.

### OOM prediction (Task 8)

Every boxed log entry also checks the Working Set against `RecognitionDiagnostics:WorkingSetWarningThresholdMB`
(default `450`, leaving ~62 MB below Render Starter's hard 512 MB limit):

```
WARNING: Memory approaching Render Starter limit. Current Working Set: 463 MB. Stage: Embedding Face 3 Finished
```

This is advisory only — logged every time the threshold is exceeded, with no throttling, retry, or
behavior change of any kind.

---

## 5. Peak tracker and summaries (Tasks 5, 7)

Every snapshot updates a single peak record (keyed off Working Set, the metric that actually drives the
Render OOM kill) with the matching Managed Heap/Private Bytes/Stage/Face/Timestamp at that same instant:

```
----------------------------------------------------------
Recognition Memory Summary
----------------------------------------------------------
  Peak Managed Heap                   : 210.3 MB
  Peak Working Set                    : 463.1 MB
  Peak Private Memory                 : 471.8 MB
  Highest Memory Stage                : Embedding Face 4 Finished
  Highest Memory Face                 : 4
  Recognition Duration                : 3542 ms
----------------------------------------------------------
```

Stage durations are accumulated into named buckets as each `StageEnd` fires (summed across faces for
per-face stages) and printed at completion:

```
----------------------------------------------------------
Recognition Timing Summary
----------------------------------------------------------
  Load Image                         : 62 ms
  Detection                          : 340 ms
  Cropping                           : 58 ms
  Embedding                          : 2210 ms
  Matching                           : 4 ms
  Saving                             : 71 ms
  Entire Recognition                 : 3542 ms
----------------------------------------------------------
```

---

## 6. `IDisposable` inventory (Task 6)

| Object | Created in | Logged as |
|---|---|---|
| Source `Image<Rgb24>` | `InsightFaceEngine.DetectAsync` (`Image.Load<Rgb24>`) | `ImageSharp Image Created/Disposed (source image)` |
| Aligned `Image<Rgb24>` (per face) | `InsightFaceEngine.DetectAsync` (`InsightFaceImageMath.AlignFace`) | `ImageSharp Image Created/Disposed (aligned face N)` |
| `MemoryStream` (per face, WebP buffer) | `InsightFaceEngine.DetectAsync` | `MemoryStream Created/Disposed (face N webp buffer)` |
| `DenseTensor<float>` (detection input) | `InsightFaceEngine.DetectFaces` (`InsightFaceImageMath.BuildDetectionInput`) | `DenseTensor<float> Created (detection input)` |
| `DenseTensor<float>` (recognition input, per face) | `InsightFaceEngine.ExtractEmbedding` (`InsightFaceImageMath.BuildRecognitionInput`) | `DenseTensor<float> Created (recognition input)` |
| `NamedOnnxValue` (per session run) | `InsightFaceEngine.DetectFaces` / `ExtractEmbedding` | `NamedOnnxValue Created (detection/recognition input)` |
| `IDisposableReadOnlyCollection<DisposableNamedOnnxValue>` (ORT outputs) | `session.Run(inputs)` in `DetectFaces` / `ExtractEmbedding` | `DisposableNamedOnnxValue collection Created/Disposed (detection/recognition outputs)` |

`DenseTensor<float>` doesn't implement `IDisposable` in the ONNX Runtime managed API used here, so only
`Created` is logged for it (no lifetime to track); everything else logs both boundaries. None of these
objects' actual lifetimes were changed — every `using`/`using var` in the pipeline is untouched.

---

## 7. Health endpoint exposure (Task 9)

Both `/health` and `/health/ready` gain a `recognitionDiagnostics` field, built by the shared
`BuildRecognitionDiagnosticsSnapshot` helper from `IRecognitionDiagnosticsStore.GetLast()` — metadata
only, never affects `isReady`/`overallStatus`, and is `null`-safe before the first job completes:

```json
"recognitionDiagnostics": {
  "status": "Healthy",
  "lastRecognition": {
    "attendanceSessionId": "5f2c...":,
    "startedUtc": "2026-07-11T09:10:00Z",
    "completedUtc": "2026-07-11T09:10:03Z",
    "peakWorkingSetMB": 411.2,
    "peakManagedHeapMB": 188.4,
    "peakPrivateMemoryMB": 462.9,
    "peakStage": "Embedding Face 4 Finished",
    "peakFace": 4,
    "lastStage": "Recognition Completed",
    "lastFace": null,
    "recognitionDurationMs": 3542,
    "completed": true,
    "failed": false
  }
}
```

Before the first classroom recognition job completes since process start:
`{"status": "NoDataYet", "lastRecognition": null}`. On a failed run,
`status` is `"LastRunFailed"` and `lastRecognition.failed` is `true`, `completed` is `false`.

---

## 8. Failure diagnostics (Task 11)

`ClassroomRecognitionPipeline.ProcessAsync`'s `catch (Exception ex)` block calls
`_diagnostics.Fail(ex)` **before** any of its existing recovery logic (`ex.Message`, `MoveToFailed()`,
save, `throw;`) — none of which changed:

```
Recognition Pipeline Failure
  Exception                          : OutOfMemoryException: Insufficient memory to continue...
  Current Stage                      : Embedding Face 4 Started
  Current Face                       : 4
  Peak Managed Heap                  : 188.4 MB
  Peak Working Set                   : 463.1 MB
  Peak Private Bytes                 : 471.8 MB
  Elapsed                            : 2980 ms
  Stack Trace                        : at Abhyanvaya.Infrastructure.InsightFace.InsightFaceEngine...
```

The exception object is also passed to `ILogger.LogError(exception, ...)` directly, so structured log
sinks retain the full `Exception`/stack trace as first-class fields in addition to the boxed text
above. The original `throw;` in the pipeline is completely unchanged — `Fail` never swallows, wraps, or
alters the exception.

---

## 9. Production overhead estimate (Task 12)

| Source | Frequency per job (N faces) | Cost |
|---|---|---|
| Full memory snapshot (`StageStart`/`StageEnd`/`FaceEvent`) | `2×6 + 2×N` (fixed stages) + N (matching) + N (dispose) ≈ `12 + 4N` | One `Process.GetCurrentProcess()` (short-lived handle) + `GC.GetTotalMemory(false)` + `Environment.WorkingSet` + 3×`GC.CollectionCount` per call — all O(1), no image/tensor traversal. |
| Object lifecycle log (`ObjectCreated`/`ObjectDisposed`) | ≈ `2 + 8N` | Single `ILogger` call, no snapshot, no allocation proportional to any buffer size. |
| Peak/stage-timing bookkeeping | O(1) per snapshot | A handful of field comparisons/assignments and one `Dictionary<string,long>` update — no per-pixel or per-tensor-element work anywhere in the diagnostics code. |

For a typical classroom photo (5–15 faces), this is on the order of 70–170 log lines and the same
number of lightweight `Process`/`GC` reads per job — negligible next to one ArcFace inference call
(the dominant cost per face). No diagnostics code touches image pixel buffers, copies a `Tensor`, or
buffers ONNX output arrays; it only reads process/GC counters and forwards already-computed values
(face index, stage name) into `ILogger`. This satisfies the < 2% CPU / < 1 MB / "no allocations
proportional to image size" budget.

The one intentional trade-off: log **volume** is high by design — this is a forensics milestone, and
Task 10's acceptance criterion is "production logging enabled." `RecognitionDiagnostics:Enabled` in
`appsettings.json` is the safety valve to turn it off instantly (no redeploy) if log ingestion cost
becomes a concern before the underlying memory issue is fixed.

---

## 10. Investigation checklist

Once deployed, use this sequence against production logs to localize the actual OOM:

1. Filter logs for `Recognition Pipeline Diagnostics` boxes for the failing `AttendanceSessionId`
   (correlate via the `Recognition Started`/`Recognition Completed`/`Recognition Pipeline Failure`
   entries, which is the only place the session ID currently appears in this log stream).
2. Read the `Recognition Memory Summary` (or, on a crash, the `Recognition Pipeline Failure` block) —
   note `Peak Working Set` and `Highest Memory Stage`/`Current Stage`.
3. Compare `Peak Working Set` against `RecognitionDiagnostics:WorkingSetWarningThresholdMB` (450 MB
   default) and count how many `WARNING: Memory approaching Render Starter limit` lines preceded the
   crash, and at which face numbers.
4. Check the `Recognition Timing Summary` — `Embedding` total time vs. face count tells you whether
   memory grows roughly linearly per face (consistent with N sequential ArcFace inferences never being
   released) or spikes at one specific face (consistent with an unusually large/complex detection).
5. Cross-reference `ImageSharp Image Created`/`Disposed` and `DisposableNamedOnnxValue collection
   Created`/`Disposed` pairs around the peak face — confirm whether the peak coincides with an object
   that *should* have already been disposed (a leak) or is expected to be alive (a legitimately large
   working set with correct, but insufficient, cleanup).
6. If the process restarts before any `Recognition Pipeline Failure` log appears at all (a hard
   OS-level OOM-kill, not a catchable `OutOfMemoryException`), the last `Recognition Pipeline
   Diagnostics` box emitted before the gap in the log stream is the closest available evidence of where
   the process died — this is precisely why every stage/face logs unconditionally rather than only on
   completion.

---

## 11. Sample end-to-end output (2-face photo, abbreviated)

```
====================================================
Recognition Pipeline Diagnostics
  Stage                              : Recognition Started
  Managed Heap                       : 96.1 MB
  Working Set                        : 210.4 MB
  Private Memory                     : 215.0 MB
  Delta                              : +0 MB
  Elapsed                            : 0 ms
====================================================
====================================================
Recognition Pipeline Diagnostics
  Stage                              : Load Image Started
  ...
====================================================
====================================================
Recognition Pipeline Diagnostics
  Stage                              : Load Image Finished
  Delta                              : +8 MB
  Elapsed                            : 41 ms
====================================================
ImageSharp Image Created (source image)
====================================================
Recognition Pipeline Diagnostics
  Stage                              : Decode Image Finished
  Delta                              : +34 MB
  Elapsed                            : 118 ms
====================================================
====================================================
Recognition Pipeline Diagnostics
  Stage                              : Face Detection Finished
  Delta                              : +52 MB
  Elapsed                            : 402 ms
====================================================
ImageSharp Image Created (aligned face 1)
====================================================
Recognition Pipeline Diagnostics
  Stage                              : Cropping Face 1 Finished
  Face                               : 1 of 2
  Delta                              : +1 MB
  Elapsed                            : 409 ms
====================================================
DenseTensor<float> Created (recognition input)
NamedOnnxValue Created (recognition input)
DisposableNamedOnnxValue collection Created (recognition outputs)
====================================================
Recognition Pipeline Diagnostics
  Stage                              : Embedding Face 1 Finished
  Face                               : 1 of 2
  Managed Heap                       : 168.9 MB
  Working Set                        : 372.6 MB
  Private Memory                     : 380.1 MB
  Delta                              : +71 MB
  Elapsed                            : 611 ms
====================================================
DisposableNamedOnnxValue collection Disposed (recognition outputs)
MemoryStream Created (face 1 webp buffer)
MemoryStream Disposed (face 1 webp buffer)
ImageSharp Image Disposed (aligned face 1)
====================================================
Recognition Pipeline Diagnostics
  Stage                              : Dispose Complete Face 1
  Face                               : 1 of 2
  Elapsed                            : 618 ms
====================================================
... (face 2 repeats the same sequence) ...
====================================================
Recognition Pipeline Diagnostics
  Stage                              : Matching Finished
  Elapsed                            : 1230 ms
====================================================
Recognition Pipeline Diagnostics: Stage = Matching Face 1 ... Matching Face 2 ...
====================================================
Recognition Pipeline Diagnostics
  Stage                              : Database Save Finished
  Elapsed                            : 1301 ms
====================================================
====================================================
Recognition Pipeline Diagnostics
  Stage                              : Recognition Completed
  Elapsed                            : 1302 ms
====================================================
----------------------------------------------------------
Recognition Memory Summary
----------------------------------------------------------
  Peak Managed Heap                   : 172.3 MB
  Peak Working Set                    : 378.9 MB
  Peak Private Memory                 : 386.2 MB
  Highest Memory Stage                : Embedding Face 2 Finished
  Highest Memory Face                 : 2
  Recognition Duration                : 1302 ms
----------------------------------------------------------
----------------------------------------------------------
Recognition Timing Summary
----------------------------------------------------------
  Load Image                         : 41 ms
  Detection                          : 284 ms
  Cropping                           : 14 ms
  Embedding                          : 812 ms
  Matching                           : 6 ms
  Saving                             : 71 ms
  Entire Recognition                 : 1302 ms
----------------------------------------------------------
```

---

## 12. Future optimization recommendations (not implemented — investigation only)

These follow directly from where the instrumentation above is expected to show the growth, but are
explicitly **out of scope** for this milestone:

1. **Bound ONNX Runtime allocator arenas per session** (`SessionOptions.EnableCpuMemArena = false`, or
   a shared `OrtEnv`/`SessionOptions` allocator across the two sessions) — SCRFD and ArcFace inference
   each hold their own arena; on a 0.5 vCPU/512 MB instance these may never fully release back to the
   OS between faces.
2. **Serialize/limit concurrent recognition jobs** so at most one classroom photo (with potentially
   many faces) is being processed at a time per instance — already effectively true today via the
   single-consumer `InMemoryClassroomPhotoQueue`, but worth confirming once real timing data is in.
3. **Stream WebP encoding instead of buffering `ms.ToArray()`** per face, if the timing summary shows
   this step contributing non-trivially to Working Set growth.
4. **Force a `GC.Collect()`/`LOH` compaction between faces** only if the timing/memory summary shows
   Managed Heap growing faster than Working Set (indicating GC pressure rather than native/unmanaged
   growth from ONNX Runtime) — otherwise this would just add CPU cost without addressing the real
   growth source.
5. **Right-size the Render plan or move to a larger instance for classroom recognition specifically**
   if the peak Working Set consistently exceeds what any code-level change can plausibly claw back
   (e.g., if two ~180 MB ONNX models plus per-inference arenas structurally require >450 MB regardless
   of face count).

None of these should be implemented until the logs from this milestone confirm which one(s) actually
apply.

---

## 13. Constraints verification

- **No recognition/matching/AI/database/business-logic/queue changes.** Every insertion in
  `ClassroomRecognitionPipeline.ProcessAsync` and `InsightFaceEngine.DetectAsync` is a call to
  `IRecognitionPipelineDiagnostics` inserted around existing statements — no existing statement, value,
  order, or control-flow branch was altered. `FaceMatcher`, `InsightFaceImageMath`, and
  `InsightFaceOnnxModelHost` are completely untouched.
- **No image resizing, caching, retries, or "optimization".** Confirmed — nothing in this milestone
  changes what bytes are read, resized, encoded, or compared.
- **Diagnostics can never throw.** Every public method on `RecognitionPipelineDiagnostics` is wrapped
  in try/catch that swallows internally.
- **No third-party packages.** Only `System.Diagnostics.Process`/`Stopwatch`, `System.GC`,
  `System.Environment`, and `Microsoft.Extensions.Logging` (already a dependency) are used.

---

## 14. Files created/modified

| File | Change |
|---|---|
| `Abhyanvaya.Infrastructure/Diagnostics/RecognitionMemorySnapshot.cs` | New — Task 1 snapshot struct (`GC.GetTotalMemory`, `Environment.WorkingSet`, `Process.GetCurrentProcess().PrivateMemorySize64`, `GC.CollectionCount(0/1/2)`, `Environment.CurrentManagedThreadId`). |
| `Abhyanvaya.Infrastructure/Diagnostics/RecognitionDiagnosticsModels.cs` | New — `RecognitionStageHandle`, `RecognitionPeakMemory`, `RecognitionDiagnosticsSummary`. |
| `Abhyanvaya.Infrastructure/Diagnostics/RecognitionDiagnosticsOptions.cs` | New — `Enabled`, `WorkingSetWarningThresholdMB` (config-driven, never hardcoded in the check). |
| `Abhyanvaya.Infrastructure/Diagnostics/IRecognitionPipelineDiagnostics.cs` | New — per-job scoped interface. |
| `Abhyanvaya.Infrastructure/Diagnostics/RecognitionPipelineDiagnostics.cs` | New — scoped implementation: stage boundaries, per-face events, peak tracker, timing buckets, OOM warning, memory/timing summaries, failure diagnostics. |
| `Abhyanvaya.Infrastructure/Diagnostics/IRecognitionDiagnosticsStore.cs` / `RecognitionDiagnosticsStore.cs` | New — singleton holder of the last completed/failed job's summary, for health endpoints. |
| `Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs` | Instrumented `DetectAsync` (Decode/Detection/Cropping/Embedding stages, per-face dispose events) and the private `DetectFaces`/`ExtractEmbedding` helpers (tensor/ORT value lifecycle) — algorithm/values unchanged. `GenerateSingleFaceEmbedding` (student embedding) is unaffected because it never calls `Begin`. |
| `Abhyanvaya.Infrastructure/Recognition/ClassroomRecognitionPipeline.cs` | Instrumented `ProcessAsync` (`Begin`/Load Image/Matching/per-face Matching/Database Save/`Complete`/`Fail`) — persistence/status-transition logic unchanged. |
| `Abhyanvaya.Infrastructure/DependencyInjection.cs` | Registers `RecognitionDiagnosticsOptions`, `IRecognitionDiagnosticsStore` (singleton), `IRecognitionPipelineDiagnostics` (scoped). |
| `Abhyanvaya.API/Program.cs` | Adds `BuildRecognitionDiagnosticsSnapshot`; `/health/ready` adds `checks.recognitionDiagnostics`; `/health` adds `health.recognitionDiagnostics`. |
| `Abhyanvaya.API/appsettings.json` | New `RecognitionDiagnostics` section (`Enabled: true`, `WorkingSetWarningThresholdMB: 450`). |
| `docs/AI15_DIAGNOSTICS1_MEMORY_FORENSICS.md` | This document. |

---

## 15. Build status

`dotnet build Abhyanvaya.sln` — **Build succeeded**, 0 errors.

## 16. Acceptance criteria

- ✅ Build succeeds.
- ✅ No behavior changes — every insertion is an additional, independent statement around existing
  code; no existing statement, value, or branch was modified.
- ✅ Recognition algorithm, matching, AI, and database logic unchanged.
- ✅ Every recognition stage (and every per-face sub-stage) is measurable via structured logs.
- ✅ Peak memory (Working Set/Managed Heap/Private Bytes) and the stage/face it occurred at are
  identifiable from a single `Recognition Memory Summary` log block per job.
- ✅ Every relevant `IDisposable`'s create/dispose lifetime is logged without changing that lifetime.
- ✅ Root-cause investigation is possible from logs alone (see §10 checklist), without attaching a
  profiler or reproducing locally.
