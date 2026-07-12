# AI16.RUNTIME.4 — Native Memory Profiling

**Status: IMPLEMENTED (diagnostics only — no pipeline behavior change)**
**Date:** 2026-07-12
**Reviewer:** Chief Software Architect
**Scope:** `RecognitionMemorySnapshot`, `RecognitionPipelineDiagnostics`, `InsightFaceEngine` (new
inference-boundary checkpoints), `RecognitionDiagnosticsSummary`, `/health` endpoint projection.

---

## 1. Objective

The AI15.DIAGNOSTICS.1 snapshot already reported Managed Heap, Working Set, and Private Memory at
every stage boundary, but could not distinguish *how much of that memory is native/unmanaged* versus
*collectible managed garbage*, nor isolate the ONNX Runtime `Run()` call's own footprint from the
broader stage that contains it. This milestone adds that finer visibility, purely as additional
read-only measurements layered onto the existing AI15.DIAGNOSTICS.1 snapshot mechanism.

## 2. New measurements added

### 2.1 Peak Native Estimate

```csharp
// RecognitionMemorySnapshot
public long NativeEstimateBytes => Math.Max(0, PrivateBytes - ManagedHeapBytes);
```

Private Bytes (`Process.PrivateMemorySize64`) already includes the managed heap — the CLR's GC
segments are committed private pages of the process. Subtracting `ManagedHeapBytes`
(`GC.GetTotalMemory(false)`) leaves an estimate of everything Private Bytes counts that the GC doesn't:
the ONNX Runtime native allocator/arena, native interop the image/encoding libraries may use, thread
stacks, the JIT, and loaded native libraries. This is explicitly documented as an **estimate** — the two
source values come from different subsystems (OS process accounting vs. the CLR's GC) sampled a few
instructions apart — clamped to `0` so any momentary sampling skew never logs as a misleading negative
number.

`RecognitionPipelineDiagnostics` tracks the **peak** of this value across the whole job
(`_peakNativeEstimateBytes`), independently of the existing Working-Set-driven peak — the native
estimate does not necessarily peak at the same stage the overall Working Set does.

### 2.2 Working Set Delta / Peak Working Set Delta

The existing per-stage snapshot already computed `deltaWorkingSetBytes` (this stage's Working Set
minus the previous stage's) for the `"Delta"` line already printed in every diagnostics box. This
milestone adds tracking of the **largest single such delta observed across the whole job**, and which
stage produced it:

```csharp
if (deltaWorkingSetBytes > _peakWorkingSetDeltaBytes)
{
    _peakWorkingSetDeltaBytes = deltaWorkingSetBytes;
    _peakWorkingSetDeltaStage = stageLabel;
}
```

This answers "which single step of the pipeline caused the biggest jump in Working Set?" — a different
question from "what was the highest absolute Working Set reached?" (the pre-existing Peak Working Set
metric), and one that the existing per-stage delta log lines didn't previously surface as a summary.

### 2.3 Peak Managed Heap / Peak Working Set / Peak Private Memory

Already existed from AI15.DIAGNOSTICS.1 (`_peakManagedHeapBytes`/`_peakWorkingSetBytes`/`_peakPrivateBytes`,
recorded together whenever a new overall Working Set peak is observed) — unchanged by this milestone,
listed here only because the task explicitly named them as required diagnostics that must be present in
the final report (see §4).

### 2.4 Memory before inference / after inference

Added two new, narrower stage checkpoints inside `InsightFaceEngine`, nested *inside* the existing
"Face Detection" and "Embedding Generation" stages (which also include tensor building and output
parsing) so the native ONNX Runtime `Run()` call's own memory footprint can be isolated from the rest
of the stage:

```csharp
// DetectFaces
var inferenceStage = _diagnostics.StageStart("ONNX Inference (Detection)");
using var outputs = session.Run(inputs);
_diagnostics.StageEnd(inferenceStage);

// ExtractEmbedding
var inferenceStage = _diagnostics.StageStart("ONNX Inference (Embedding)");
using var outputs = session.Run(inputs);
_diagnostics.StageEnd(inferenceStage);
```

Each `StageStart`/`StageEnd` pair captures a full `RecognitionMemorySnapshot` immediately before and
immediately after the bracketed call — for these two new stages, that is memory immediately before
`session.Run(...)` is invoked and immediately after it returns, giving a direct "before inference" /
"after inference" pair per face (embedding) and per photo (detection).

### 2.5 Memory after disposal

`InsightFaceEngine`/`RecognitionPipelineDiagnostics` already log an `ObjectDisposed` event immediately
after every `using`-scoped object (aligned face image, per-face `MemoryStream`, `DisposableNamedOnnxValue`
collection) goes out of scope — see the existing calls immediately following each `using var`/
`await using` block. Per Task 12 (AI15.DIAGNOSTICS.1), `ObjectDisposed`/`ObjectCreated` are deliberately
lightweight (log line only, no `RecognitionMemorySnapshot` capture) because they fire far more often
than stage boundaries — capturing a full snapshot on every single object create/dispose event would
scale diagnostics overhead with face count, exactly the kind of cost this milestone is meant to reduce,
not add. "Memory after disposal" is therefore observable as the **next stage-boundary snapshot**
immediately following a dispose (e.g., the "Cropping Finished" snapshot is taken right after `aligned`'s
`using` scope ends) rather than as a dedicated snapshot fired from `ObjectDisposed` itself — this
preserves the existing, deliberate cost/frequency tradeoff from AI15.DIAGNOSTICS.1 while still making
post-disposal memory visible in the log stream.

### 2.6 Memory after GC

Delivered as AI16.RUNTIME.5 (`RecognitionDiagnosticsOptions.ForceGcValidation`, default `false`) —
see `docs/AI16_RUNTIME5_FORCED_GC.md`. Cross-referenced here because the task groups it under
"finer visibility"; it is documented separately because it is diagnostics-only *and* gated behind its
own configuration flag with its own CPU-cost tradeoff (a full blocking GC pass), distinct from the
always-on measurements in §2.1–2.5.

## 3. Health endpoint — final memory report

`RecognitionDiagnosticsSummary` (produced once per job, at `Complete()`/`Fail()`) gained:

```csharp
long PeakNativeEstimateBytes,
long PeakWorkingSetDeltaBytes
```

`Program.cs`'s existing `recognitionDiagnostics.lastRecognition` projection on `/health` and
`/health/ready` (already exposing `peakWorkingSetMB`/`peakManagedHeapMB`/`peakPrivateMemoryMB` since
AI15.DIAGNOSTICS.1) now additionally exposes:

```json
"recognitionDiagnostics": {
  "lastRecognition": {
    "...": "... unchanged existing fields ...",
    "peakNativeEstimateMB": 41.3,
    "peakWorkingSetDeltaMB": 18.7
  }
}
```

No existing field was renamed or removed — these are additive, matching the same pattern used for the
AI15.DIAGNOSTICS.2B/2C fields added previously.

## 4. Final memory report — where every requested field now lives

| Requested field | Where it is now surfaced |
|---|---|
| Working Set Delta | Existing per-stage "Delta" log line (unchanged) + new `PeakWorkingSetDeltaBytes` summary field. |
| Peak Working Set | `RecognitionDiagnosticsSummary.PeakWorkingSetBytes` (pre-existing) → "Recognition Memory Summary" log + `/health`. |
| Peak Private Memory | `RecognitionDiagnosticsSummary.PeakPrivateBytes` (pre-existing) → same log + `/health`. |
| Peak Managed Heap | `RecognitionDiagnosticsSummary.PeakManagedHeapBytes` (pre-existing) → same log + `/health`. |
| Peak Native Estimate | **New** `RecognitionDiagnosticsSummary.PeakNativeEstimateBytes` → "Recognition Memory Summary" log + `/health`. |
| Memory before inference | **New** — snapshot captured by `StageStart("ONNX Inference (Detection|Embedding)")`. |
| Memory after inference | **New** — snapshot captured by the matching `StageEnd(...)`. |
| Memory after disposal | Existing next-stage-boundary snapshot immediately following each `using` scope's exit (see §2.5). |
| Memory after GC | AI16.RUNTIME.5's `LogForcedGcValidation()`, gated behind `RecognitionDiagnostics:ForceGcValidation` (default `false`). |

The "Recognition Memory Summary" log block (`RecognitionPipelineDiagnostics.LogMemorySummary`), printed
once per job at `Complete()`, is the single consolidated "final memory report" — it now reads:

```
----------------------------------------------------------
Recognition Memory Summary
----------------------------------------------------------
  Peak Managed Heap                   : 38.2 MB
  Peak Working Set                    : 187.4 MB
  Peak Private Memory                 : 201.6 MB
  Peak Native Estimate                : 41.3 MB
  Peak Working Set Delta              : 18.7 MB (at Embedding Face 3 Finished)
  Highest Memory Stage                : Face Detection Finished
  Highest Memory Face                 : (none)
  Recognition Duration                : 842 ms
----------------------------------------------------------
```

(Illustrative values — actual numbers depend on the photo/host and were not captured from a live run
in this pass; see §5 for how to obtain real numbers.)

## 5. How to obtain real measurements

None of the new fields were benchmarked against a live Render deployment in this documentation pass —
doing so requires either a running Postgres-backed environment with real classroom photos or the
Testcontainers-based integration harness (currently blocked by an unrelated pre-existing Docker/DB
issue — see the AI16.RUNTIME cover note). To capture real numbers: enable
`RecognitionDiagnostics:Enabled` (already the default), process a representative classroom photo, and
read the "Recognition Memory Summary" block from the logs, or `GET /health` /`GET /health/ready`
immediately afterwards for the same fields in JSON.

## 6. Requirements verified

- ✅ No pipeline behavior change — every new measurement point wraps an *existing* call
  (`session.Run(...)`) with `StageStart`/`StageEnd`, which only reads process/GC state and logs; it does
  not alter control flow, retries, or timing behavior of the wrapped call itself (the same guarantee
  `RecognitionPipelineDiagnostics`'s class-level `<remarks>` already documents for every public method).
- ✅ No AI/model/threshold change.
- ✅ `dotnet build` — `Abhyanvaya.Infrastructure` builds with 0 errors.
