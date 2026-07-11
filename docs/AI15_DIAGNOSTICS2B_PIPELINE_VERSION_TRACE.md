# AI15.DIAGNOSTICS.2B — Pipeline Version Traceability

**Status: IMPLEMENTED (observability only — no behavior change)**
**Date:** 2026-07-12
**Reviewer:** Chief Software Architect

---

## 1. Objective

Every recognition execution should record, in logs and in the health endpoint, exactly which AI
pipeline version processed the image — reusing the existing `InsightFaceOptions.PipelineVersion`
configuration value, never a hardcoded or independently-parsed copy of it.

## 2. Single source of truth

```csharp
// Abhyanvaya.Infrastructure/InsightFace/InsightFaceOptions.cs
public string PipelineVersion { get; set; } = "InsightFace-1.0";
```

This was already bound via the standard `IOptions<InsightFaceOptions>` pattern before this milestone.
No new configuration section, key, or binding was added. Every consumer below reads this **same**
`IOptions<InsightFaceOptions>` snapshot — none of them re-read the `InsightFace` configuration section
independently:

| Consumer | How it obtains `PipelineVersion` |
|---|---|
| `ClassroomRecognitionPipeline` | Already held `_insightFaceOptions` (`IOptions<InsightFaceOptions>`) before this milestone — reused as-is. |
| `ClassroomRecognitionBackgroundService` | Newly constructor-injects `IOptions<InsightFaceOptions>` (previously unused there) for the Queue Trace log. |
| `RecognitionPipelineDiagnostics` | Newly constructor-injects `IOptions<InsightFaceOptions>`, reads `.Value.PipelineVersion` **once** in the constructor into a private `readonly string` field — never per-log-line. |
| `Program.cs` health endpoints | Already resolves `IOptions<InsightFaceOptions>` for the existing "Recognition Engine" startup diagnostics (AI14.RUNTIME.1) — the per-job value is instead read from the `RecognitionDiagnosticsSummary` captured at job completion (see §4), which is itself sourced from the same options binding. |

"Recognition Engine" (`InsightFace`) is likewise never hardcoded as a bare string literal in the new
code — it is sourced from the existing `Abhyanvaya.Domain.Constants.EmbeddingProviders.InsightFace`
constant, the same constant `InsightFaceEngine` already stamps onto detection results.

## 3. The "Execution Trace" log sub-block

A shared helper, `Abhyanvaya.Infrastructure.Diagnostics.ExecutionTraceLog.LogBlock(...)`, prints:

```
  Execution Trace
    Trace Id                          : TRACE-20260712-143205-A1B2C3D4
    Pipeline Version                  : InsightFace-1.0
    Recognition Engine                : InsightFace
    Tenant                             : 1
    Session                            : 3fa85f64-5717-4562-b3fc-2c963f66afa6
    Attempt                            : 1
    Elapsed Since Queue                : 1523 ms
    Elapsed Since Pipeline Start       : 1180 ms
```

This block is appended (never inserted into or reformatting an existing line) after each of the
following **summary/bookend** logs — deliberately not after every high-frequency per-stage/per-face
line from AI15.DIAGNOSTICS.1, which keeps its exact original format:

| Log point | Where |
|---|---|
| **Queue Trace** | `ClassroomRecognitionBackgroundService.LogQueueTrace` — right after the "QUEUE TRACE" box, at dequeue. |
| **Pipeline Entry** | `ClassroomRecognitionPipeline.LogPipelineEntry` — right after the "PIPELINE ENTRY" box, at the top of `ProcessAsync`, before the session is loaded. |
| **Recognition Summary** | `RecognitionPipelineDiagnostics.Begin`/`Complete` — right after the "Recognition Started" and "Recognition Completed" box lines. |
| **Peak Memory Summary** | `RecognitionPipelineDiagnostics.Complete` — right after `LogMemorySummary()` (the existing "Recognition Memory Summary" block). |
| **Failure Diagnostics** | `RecognitionPipelineDiagnostics.Fail` — right after the existing `LogError("Recognition Pipeline Failure...")` call. |

## 4. Health endpoint

`RecognitionDiagnosticsSummary` (`Abhyanvaya.Infrastructure/Diagnostics/RecognitionDiagnosticsModels.cs`)
gained three new fields, populated once by `RecognitionPipelineDiagnostics.BuildSummary()` from data it
already holds (its own `_pipelineVersion` field and the scoped `IRecognitionExecutionContext`):

```csharp
string PipelineVersion,
string ExecutionTraceId,
int RecognitionAttempt
```

`Program.cs`'s existing `recognitionDiagnostics.lastRecognition` projection (already exposed on
`/health` and `/health/ready` since AI15.DIAGNOSTICS.1) now includes them — no new endpoint, no
existing field removed or renamed:

```json
"recognitionDiagnostics": {
  "lastRecognition": {
    "attendanceSessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "...": "... unchanged existing fields ...",
    "pipelineVersion": "InsightFace-1.0",
    "executionTraceId": "TRACE-20260712-143205-A1B2C3D4",
    "recognitionAttempt": 1
  }
}
```

## 5. Constraints verified

- ✅ `PipelineVersion` is read exclusively from the existing `InsightFaceOptions` binding — grepped for
  any new `GetSection("InsightFace")` or `Get<InsightFaceOptions>()` call: none introduced.
- ✅ No hardcoded `"InsightFace-1.0"` string anywhere in the new/changed code.
- ✅ No configuration schema changes — `appsettings.json` untouched by this milestone.
- ✅ No AI/recognition/matching behavior changes — every change is either a constructor injection of an
  already-registered `IOptions<InsightFaceOptions>`, or a new/extended log line.
- ✅ No new endpoint — `/health` and `/health/ready` are extended, not duplicated.
- ✅ `dotnet build` — 0 errors.
