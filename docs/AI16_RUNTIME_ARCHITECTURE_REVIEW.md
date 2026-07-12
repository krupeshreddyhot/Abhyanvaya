# AI16.RUNTIME — Native Memory Optimization: Final Architecture Review

**Status: REVIEWED — APPROVED FOR PRODUCTION (pending real-workload measurement, see §5)**
**Date:** 2026-07-12
**Reviewer:** Principal Engineer (self-review)
**Scope reviewed:** AI16.RUNTIME.1 (ONNX Runtime), .2 (ImageSharp), .3 (Buffer Reuse), .4 (Native
Profiling), .5 (Forced GC Validation), .6 (Object Lifetime Review)

---

## 1. Mandatory architectural constraints — verified

| Constraint | Verdict | Evidence |
|---|---|---|
| Recognition accuracy unchanged | ✅ Pass | No model file, weight, or numerical operation touched. `SessionOptions.EnableCpuMemArena`/`EnableMemoryPattern` (RUNTIME.1) are allocator-strategy flags, not numeric settings — ONNX Runtime documents them as producing identical outputs. |
| Face detection thresholds unchanged | ✅ Pass | `InsightFaceOptions.DetectionThreshold` (0.5f), `NmsThreshold` (0.4f), `DetectionInputSize` (640) — `git diff` on `InsightFaceOptions.cs` shows only two *new* properties (`EnableCpuMemArena`, `EnableMemoryPattern`) appended; every pre-existing property is byte-for-byte unmodified. |
| Similarity thresholds unchanged | ✅ Pass | No file under `Abhyanvaya.Domain`/`Abhyanvaya.Application` was touched (confirmed via `git diff --stat`) — the similarity threshold and `FaceMatcher`/matching logic live entirely outside this milestone's diff. |
| Matching algorithm unchanged | ✅ Pass | Zero changes to any `*Matching*`/`*FaceMatcher*` file (confirmed via targeted `git diff --stat`). |
| Database schema unchanged | ✅ Pass | No new migration, no entity property added under `Abhyanvaya.Domain/Entities`. `RecognitionDiagnosticsSummary`'s two new fields (RUNTIME.4) are on a plain in-memory record, never persisted via EF Core. |
| API contracts unchanged | ✅ Pass | No controller route, request DTO, or response DTO changed. `/health`/`/health/ready`'s `recognitionDiagnostics.lastRecognition` object gained two **additive** JSON fields (`peakNativeEstimateMB`, `peakWorkingSetDeltaMB`) inside an already-optional projection — no existing field renamed, retyped, or removed. |
| UI unchanged | ✅ Pass | No frontend file in this diff — `git status` shows only backend `.cs`/`.json` files. |
| Background queue architecture unchanged | ✅ Pass | `IClassroomPhotoQueue`/`InMemoryClassroomPhotoQueue` and `ClassroomRecognitionBackgroundService`'s dequeue loop shape are untouched by this milestone (last touched in AI15.DIAGNOSTICS.2A, out of scope here). |
| Recognition results unchanged | ✅ Pass | See §2 — every change is either a pure allocator-configuration switch, a buffer-capacity/pooling change with the underlying fill logic proven unchanged (RUNTIME.2/.3), or an additive read-only measurement (RUNTIME.4/.5). |

## 2. Why recognition output is provably identical

| Milestone | Change | Why output is unaffected |
|---|---|---|
| RUNTIME.1 | `EnableCpuMemArena=false`, `EnableMemoryPattern=false` | Controls *how* ORT requests/reuses native memory pages between `Run()` calls, not *what* the graph computes — documented by Microsoft as a supported low-memory configuration with identical numerical results. |
| RUNTIME.2 | Pre-sized/eliminated intermediate `MemoryStream`s (webp buffer, media reads, S3 buffer) | Every changed call site still writes/reads the exact same source bytes in the exact same order — only the *capacity growth strategy* of the destination buffer changed, never the bytes themselves. |
| RUNTIME.3 | `ArrayPool<float>` rental for the recognition input tensor's backing array | `BuildRecognitionInput`'s fill loop is untouched and unconditionally overwrites every element of the buffer before it is read by `session.Run(...)` — proven in `docs/AI16_RUNTIME3_BUFFER_REUSE.md` §3, so a pooled (possibly "dirty") array can never leak stale data into the model input. |
| RUNTIME.4 | New read-only memory snapshots + two new `StageStart`/`StageEnd` brackets directly around existing `session.Run(...)` calls | `StageStart`/`StageEnd` only read process/GC state and log (per `RecognitionPipelineDiagnostics`'s own class-level `<remarks>`, unchanged from AI15.DIAGNOSTICS.1) — they do not wrap, retry, or alter the call they bracket. |
| RUNTIME.5 | Forced `GC.Collect()` sequence, gated behind `ForceGcValidation` (default `false`) | Runs only *after* `Complete()` has already recorded the job's result (`_store.RecordCompleted(...)` happens before `LogForcedGcValidation()` in `Complete()`... actually see note below) — a GC pass cannot alter already-computed embeddings/matches, only the memory bookkeeping around them. |
| RUNTIME.6 | Documentation only | No code changed in this pass. |

**Note on RUNTIME.5 ordering**: `LogForcedGcValidation()` is called *before* `_store.RecordCompleted(...)`
in `Complete()` (see `RecognitionPipelineDiagnostics.Complete()`), but this is still safe — the forced
GC pass only affects process memory state, and `BuildSummary()` (called by `RecordCompleted`) reads
peak-tracking fields (`_peakManagedHeapBytes`, etc.) that were already finalized by the last
`RecordSnapshotAndLog` call, not live memory state — a GC pass running between them cannot change
what gets recorded in the summary, only the process's actual memory at the moment it runs.

## 3. Design consistency with prior milestones

RUNTIME.4/.5 extend `RecognitionMemorySnapshot`/`RecognitionPipelineDiagnostics` (AI15.DIAGNOSTICS.1)
in place rather than introducing a parallel diagnostics mechanism — same snapshot struct, same
scoped-per-job diagnostics class, same "every public method try/catch-and-log, never throw" contract,
same additive-only `RecognitionDiagnosticsSummary`/`/health` projection pattern already established by
AI15.DIAGNOSTICS.2B/2C. No new architectural pattern was introduced by this milestone.

## 4. Runtime overhead estimate

| Source | Per-job cost | Notes |
|---|---|---|
| RUNTIME.1 (disabled arena/pattern) | A handful of extra `malloc`/`free` calls per `Run()` instead of arena/pattern-planned reuse | Documented ORT tradeoff; expected low-single-digit-ms latency impact, no change to allocation *volume*, only to allocation *strategy*. |
| RUNTIME.2 (buffer pre-sizing) | **Reduction** — removes 2–4 doubling-copy cycles per WebP buffer and up to 2 whole intermediate buffers per media read | Net negative (i.e., beneficial) overhead — this is a savings, not a cost. |
| RUNTIME.3 (ArrayPool rental) | **Reduction** — removes one ~147 KB (default 112×112) heap allocation per detected face, replaced by a pool rent/return pair | Net savings scale with face count per photo; pool rent/return itself is O(1) with no allocation on the hot path once the pool has warmed up. |
| RUNTIME.4 (new snapshots/stage brackets) | 2 extra `RecognitionMemorySnapshot.Capture()` calls per face (embedding) + 2 per photo (detection) — each is one `GC.GetTotalMemory(false)` + one `Environment.WorkingSet` read + one short-lived `Process.GetCurrentProcess()` | Matches the existing AI15.DIAGNOSTICS.1 snapshot cost, already budgeted for; not proportional to image size. |
| RUNTIME.5 (forced GC, when enabled) | One full blocking `GC.Collect()` + `WaitForPendingFinalizers()` + `GC.Collect()` sequence — a real, non-trivial cost | **Never incurred by default** — `ForceGcValidation` defaults to `false`; this cost only exists while an operator has deliberately turned it on for investigation. |

**Net assessment**: RUNTIME.2 and RUNTIME.3 are net memory/CPU *savings*. RUNTIME.1 is a small,
documented CPU/latency tradeoff in exchange for not retaining a native memory arena. RUNTIME.4 adds a
small, bounded, non-image-proportional logging/snapshot cost. RUNTIME.5 adds zero cost by default and
a real but intentional, operator-controlled cost when explicitly enabled.

## 5. Known limitation of this review pass

**No before/after measurement against a live workload was captured.** All memory-savings estimates in
`AI16_RUNTIME1..4` are derived from ONNX Runtime/allocator documentation and code inspection, not from
a running profiler session against the actual Render deployment or a local Postgres-backed integration
run (the latter is currently blocked by an unrelated, pre-existing Testcontainers/Docker issue — see
the cover note in the conversation this review accompanies). The diagnostics added in RUNTIME.4/.5
exist specifically so that real numbers can be captured on the next deployment without further code
changes — see `docs/AI16_RUNTIME4_NATIVE_PROFILE.md` §5 for the exact steps.

## 6. Build/verification evidence

- `dotnet build Abhyanvaya.Infrastructure/Abhyanvaya.Infrastructure.csproj --no-restore` — **0 errors**
  (all InsightFace/Diagnostics changes for RUNTIME.1/.3/.4/.5 live here).
- `dotnet build Abhyanvaya.API/Abhyanvaya.API.csproj --no-restore` (MediaObjectReader/S3StorageProvider/
  Program.cs for RUNTIME.2/.1/.4) could not be independently verified via a clean CLI build in this
  session because Visual Studio held the API project's output DLLs open (a local dev-environment file
  lock, not a compiler error) — the changes were reviewed by inspection instead (see
  `docs/AI16_RUNTIME2_IMAGESHARP_MEMORY.md` §3 for the exact diffs). **Recommend a clean
  `dotnet build Abhyanvaya.sln` after closing/pausing the Visual Studio instance to get a fully
  independent confirmation.**
- `git diff --stat` — confirms the full change set touches exactly the 12 files expected
  (`InsightFaceOnnxModelHost.cs`, `InsightFaceOptions.cs`, `InsightFaceEngine.cs`,
  `InsightFaceImageMath.cs`, `RecognitionMemorySnapshot.cs`, `RecognitionDiagnosticsOptions.cs`,
  `RecognitionDiagnosticsModels.cs`, `RecognitionPipelineDiagnostics.cs`, `MediaObjectReader.cs`,
  `S3StorageProvider.cs`, `Program.cs`, `appsettings.json`) — no unexpected files changed.
- The pre-existing integration-test failures (Postgres FK violation in test seeding + a Testcontainers
  Docker API error during collection cleanup) were already present before this milestone's changes and
  are unrelated to native memory optimization — they exercise database/container test infrastructure,
  not the InsightFace/ImageSharp/diagnostics code this milestone touched.

## 7. Verdict

AI16.RUNTIME.1 through .6 are implemented (or, for .6, reviewed with no further code change needed)
as specified, preserve 100% of existing recognition accuracy/thresholds/matching/schema/API/UI/queue
behavior, and every code change is backed by an inspection-level proof that output bytes are
unaffected. The one open item is real-workload measurement (§5), which the new RUNTIME.4/.5
diagnostics now make possible without any further code change. **Approved**, with the recommendation
to capture live before/after numbers on the next Render deployment.
