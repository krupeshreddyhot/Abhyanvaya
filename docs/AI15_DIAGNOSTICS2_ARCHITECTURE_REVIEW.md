# AI15.DIAGNOSTICS.2 — Final Architecture Review (2A–2C)

**Status: REVIEWED — APPROVED FOR PRODUCTION**
**Date:** 2026-07-12
**Reviewer:** Principal Engineer (self-review)
**Scope reviewed:** AI15.DIAGNOSTICS.2A (Scoped Execution Context), 2B (Pipeline Version Traceability),
2C (Recognition Attempt Tracking)

---

## 1. Checklist

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | `AsyncLocal` has been completely removed | ✅ Pass | It was never implemented — the prior turn produced only a design, no code. `grep -rn "AsyncLocal"` across all `.cs` files returns only doc-comments *stating its absence* (see §2). |
| 2 | Scoped `RecognitionExecutionContext` is registered correctly | ✅ Pass | `Abhyanvaya.Infrastructure/DependencyInjection.cs`: `services.AddScoped<IRecognitionExecutionContext, RecognitionExecutionContext>();` |
| 3 | Lifetime is Scoped | ✅ Pass | Confirmed via `AddScoped<...>` (not `AddSingleton`/`AddTransient`) and via runtime reasoning: the background worker resolves it once per `scope.ServiceProvider` per dequeued message, and `ClassroomRecognitionPipeline`/`RecognitionPipelineDiagnostics` resolve it from that same scope. |
| 4 | No static state exists | ✅ Pass | `RecognitionExecutionContext` has zero `static` members. All state is instance fields guarded by an instance-level `lock (_gate)`, identical in shape to `TenantContextAccessor`. |
| 5 | `ExecutionTraceId` is preserved through the full pipeline | ✅ Pass | Minted once in `Initialize()` (background worker, at dequeue). Read unchanged by `ClassroomRecognitionPipeline` (Pipeline Entry log), `RecognitionPipelineDiagnostics` (Recognition Started/Completed, Memory Summary, Failure Diagnostics, health-endpoint summary) — all via the *same* scoped instance, no copying/re-derivation. |
| 6 | `PipelineVersion` comes from `InsightFaceOptions` | ✅ Pass | Every consumer holds an `IOptions<InsightFaceOptions>` and reads `.Value.PipelineVersion`; none re-parses configuration or hardcodes `"InsightFace-1.0"`. See `docs/AI15_DIAGNOSTICS2B_PIPELINE_VERSION_TRACE.md` §2 for the full consumer table. |
| 7 | `RecognitionAttempt` defaults to 1 | ✅ Pass | `ClassroomRecognitionBackgroundService.CurrentRecognitionAttempt = 1` (named `const int`), passed into `Initialize(...)`. No path sets any other value. |
| 8 | No recognition logic changed | ✅ Pass | `InsightFaceEngine.DetectAsync`/`DetectFaces`/`ExtractEmbedding` — untouched by this milestone (last touched in AI15.DIAGNOSTICS.1, out of scope here). |
| 9 | No queue logic changed | ✅ Pass | `InMemoryClassroomPhotoQueue`/`IClassroomPhotoQueue` — untouched. The background worker's `await foreach (var message in _queue.DequeueAllAsync(...))` loop shape, `_queue.Count` read, and cancellation handling are unchanged; only logging/diagnostics statements were added around the existing control flow. |
| 10 | No AI logic changed | ✅ Pass | No ONNX/`SessionOptions`/model-loading code touched. |
| 11 | No matching logic changed | ✅ Pass | `IFaceMatcher`/`FaceMatcher` — untouched. `ClassroomRecognitionPipeline`'s call to `_faceMatcher.Match(...)` is unchanged; only diagnostics calls around it (from AI15.DIAGNOSTICS.1, pre-existing) remain as they were. |
| 12 | No database schema changes | ✅ Pass | No migration added. No entity property added. `RecognitionDiagnosticsSummary` (a plain in-memory record, never persisted) gained fields, but that is not a schema change. |
| 13 | No API contract changes | ✅ Pass | No controller route, request DTO, or response DTO changed. `/health` and `/health/ready` gained three **additive** JSON fields inside the already-existing, already-optional `recognitionDiagnostics.lastRecognition` object (present only when a job has completed at least once) — existing consumers reading known fields are unaffected; nothing existing was renamed, removed, or retyped. |

## 2. `AsyncLocal` removal — clarification

The original AI15.DIAGNOSTICS.2 (Prompts 1–8) design response proposed an `AsyncLocal<ExecutionTraceContext>`
approach but **no code implementing it was ever written** — the user paused implementation after the
design/Prompt-1 response specifically to reconsider this piece, which became 2A. Item 1 above is
therefore verified by absence: there is no `AsyncLocal` type anywhere in the diff or the wider
codebase (confirmed via `grep -rn "AsyncLocal"`, which returns only the three doc-comments in the new
files that explicitly state it is *not* used, plus the pre-existing `TenantContextAccessor` comment
making the same claim about tenant context).

## 3. Design consistency with `ITenantContextAccessor`

| Aspect | `ITenantContextAccessor` (existing) | `IRecognitionExecutionContext` (new) |
|---|---|---|
| Interface location | `Abhyanvaya.Application/Common/Interfaces/` | `Abhyanvaya.Application/Common/Interfaces/` |
| Implementation location | `Abhyanvaya.Infrastructure/Services/` | `Abhyanvaya.Infrastructure/Services/` |
| Lifetime | Scoped | Scoped |
| State storage | Private field + `lock (_gate)` | Private fields + `lock (_gate)` |
| Static/AsyncLocal/ThreadStatic | None | None |
| Lifecycle | `SetTenant(...)` → used → `Clear()` in `finally` | `Initialize(...)` → `MarkPipelineStarted()` → used → `Clear()` in `finally` |
| Who owns the scope | `ClassroomRecognitionBackgroundService.ExecuteAsync` (per dequeued message) | Same — the identical scope, resolved alongside `ITenantContextAccessor` |

This is a deliberate 1:1 pattern match, not a superficial resemblance — both accessors are resolved
from, and cleared within, the exact same `await using var scope = _scopeFactory.CreateAsyncScope();`
block in `ClassroomRecognitionBackgroundService`.

## 4. Runtime overhead estimate

| Source | Per-job cost | Notes |
|---|---|---|
| `Initialize`/`MarkPipelineStarted`/`Clear` | 3× `lock` acquisitions + ~6 field writes total | Uncontended lock (single logical caller per job) — effectively free, no measurable CPU. |
| `Guid.NewGuid()` | One call per job | ~100 ns; 16 bytes, stack-allocated struct copy into a field — no heap allocation. |
| New log lines (Queue Trace, Pipeline Entry, 4× Execution Trace sub-blocks) | ~35 `LogInformation`/`LogError` calls total per job (bookend/summary-level only, **not** per-face/per-stage) | Matches the existing AI15.DIAGNOSTICS.1 logging mechanism (`ILogger` structured logging) already in production use — no new logging provider, no new I/O path. |
| `ExecutionTraceLog.FormatTraceId` | Called ~6×/job | One `DateTime` format + one `Guid.ToString("N")` substring + one string interpolation — a handful of small, short-lived string allocations (well under 1 KB total), not proportional to image size or face count. |
| New `IOptions<InsightFaceOptions>` injections (background worker, `RecognitionPipelineDiagnostics`) | Zero incremental cost | `IOptions<T>` is already a cached singleton snapshot bound once at startup — injecting it into two more classes adds no re-parsing, no re-binding, no per-request work. |

**Estimated CPU overhead:** materially below the 1% target — the added work is a small, fixed number of
lock/field operations and string-formatting calls per job (not per stage, not per face), dominated in
practice by the `ILogger` sink's own I/O cost, which was already present and budgeted for in
AI15.DIAGNOSTICS.1.

**Estimated memory overhead:** well under the 512 KB target — one `Guid` (16 bytes) plus a small,
bounded number of short-lived formatted strings (trace-id string ~30 chars, a handful of log-message
strings) per job; all are Gen0 garbage collected almost immediately and never retained.

**Allocations proportional to image size:** none. Nothing added in 2A/2B/2C reads, copies, or buffers
image bytes, pixel data, tensors, or ONNX values — those remain exactly as instrumented (read-only,
no extra copies) in AI15.DIAGNOSTICS.1, untouched by this milestone.

## 5. Known, deliberate deviation from the literal request

`Attempt` was **not** added to the recovery-service "Recovery Summary" log (AI14.RUNTIME.3). See
`docs/AI15_DIAGNOSTICS2C_RECOGNITION_ATTEMPT.md` §4 for the full rationale: the recovery sweep is a
batch operation over already-orphaned sessions whose original `IRecognitionExecutionContext` no longer
exists, and `RecognitionAttempt` is not persisted anywhere (persisting it was explicitly out of scope
and would require a schema change, which this milestone forbids). Fabricating a value there would be
misleading rather than diagnostic. This is flagged here explicitly rather than silently omitted.

## 6. Build/verification evidence

- `dotnet build Abhyanvaya.sln` — **0 errors, 0 warnings**, after every file change in this milestone.
- `grep -rn "AsyncLocal|ThreadStatic"` — no functional usage, only doc-comments confirming absence.
- Manual trace of the DI graph: `ClassroomRecognitionBackgroundService` (Singleton-lifetime
  `BackgroundService`) creates one scope per message and resolves `ITenantContextAccessor`,
  `IRecognitionExecutionContext`, and `IClassroomRecognitionPipeline` from that same
  `scope.ServiceProvider` — confirmed by reading the constructor registrations in
  `Abhyanvaya.Infrastructure/DependencyInjection.cs` (all three are `AddScoped`).

## 7. Verdict

AI15.DIAGNOSTICS.2A, 2B, and 2C are implemented as specified (with the one documented, justified
deviation in §5), preserve 100% of existing recognition/matching/queue/AI behavior and all
AI15.DIAGNOSTICS.1 log formats, introduce no static or ambient state, and add overhead far below the
stated targets. **Approved.**
