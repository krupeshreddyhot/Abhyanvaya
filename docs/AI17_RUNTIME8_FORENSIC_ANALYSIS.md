# AI17.RUNTIME.8 — Production Recognition Forensics Analysis

**Status: ANALYSIS FRAMEWORK — NO PRODUCTION EVIDENCE CAPTURED YET.**

This milestone asked for a read-only forensic analysis of a completed production run on the Render
Starter (512 MB) instance, correlated by `ExecutionTraceId` across Render logs, the AI17.RUNTIME.1–.6
log blocks, `/health`, `/health/ready`, and the Render OOM event.

**No such evidence exists in this workspace.** A full search was performed before writing this
document:

- No `*.log` files anywhere in the repository.
- No files containing `AI17 STAGE CHECKPOINT`, `MEMORY SPIKE DETECTED`, `Queue Received`, or any other
  AI17 log-block marker outside of the instrumentation source code itself and the AI17.RUNTIME.7
  document (which is explicitly marked as pre-production, estimate-only).
- No Render MCP/API access is configured in this environment to pull logs, `/health` output, or OOM
  events directly from the deployed service.
- No pasted terminal output or attached files containing production log text.

Per this milestone's own strict constraint — **"Do not speculate. Every conclusion must cite
supporting evidence from the forensic logs"** — none of Tasks 1–11 can be completed with real numbers
right now. Fabricating timeline durations, GC counts, or an OOM root cause without a real log to point
to would produce a report that *looks* like forensics but is actually invented, which is precisely the
outcome this AI17 phase exists to prevent.

Per your instruction, this document is instead the **complete analysis framework**: for every task,
it specifies exactly which AI17/AI15/AI16 log line to extract, the exact field names to read (matching
the literal log format already shipped in `RecognitionForensicsAudit.cs` /
`RecognitionPipelineDiagnostics.cs`), and the computation to perform. Once a real production log is
captured, filling this document in becomes a mechanical extraction exercise, not a fresh investigation.

---

## Executive Summary

| | |
|---|---|
| **Objective** | Determine root cause of Render Starter (512 MB) memory exhaustion during classroom recognition, from production forensic evidence |
| **Evidence available** | None — no production run has been captured against the AI17-instrumented build in this workspace |
| **Conclusion reached** | **None.** Root cause cannot be responsibly determined without measured data (see Task 9) |
| **What exists instead** | A structural, code-derived *hypothesis* from AI17.RUNTIME.7 (§11 there) — the full-resolution `image.Clone()` in `BuildDetectionInput` — explicitly labeled there as unconfirmed and estimate-based, not a forensic finding |
| **Action required before this document can be completed** | Capture one real job's logs per the "Evidence Capture Protocol" below and re-run this analysis |
| **Code/behavior changed this milestone** | None (see Verification, end of document) |

---

## Evidence Capture Protocol (must happen before Tasks 1–11 can be filled in)

1. Deploy the AI17-instrumented build (already merged/available on this branch) to the Render Starter
   instance with `RecognitionDiagnostics:Enabled = true` (default).
2. Trigger one classroom photo recognition job — ideally one that is known to have previously
   approached or triggered the OOM, so the capture is representative.
3. From Render's log stream, export **every** line for that job's window. The AI17 blocks all share
   one `ExecutionTraceId` (format `TRACE-yyyyMMdd-HHmmss-XXXXXXXX`, from `ExecutionTraceLog.FormatTraceId`)
   and are labeled with these exact markers — grep for these to isolate one job's evidence from a
   mixed-traffic log stream:

   | Marker | Source | Cardinality per job |
   |---|---|---|
   | `AI17 STAGE CHECKPOINT` | `RecognitionForensicsAudit.Checkpoint` | 16 fixed + 4×(detected faces) |
   | `MEMORY SPIKE DETECTED` | same, conditional | 0+ |
   | `AI17 Object Created` / `AI17 Disposable Audit` | `ObjectCreated`/`ObjectDisposed` | variable |
   | `LONG LIVED DISPOSABLE` / `UNDISPOSED ONNX OUTPUT` | `FinalizeAudit` sweep, conditional | 0+ (expected 0) |
   | `WARNING: Multiple classroom images resident.` / `WARNING: Face crop retained.` | `ObjectCreated`/`CheckFaceCropRetainedAfterEmbedding` | `Face crop retained` expected once per face; `Multiple classroom images` expected 0 |
   | `AI17 STUDENT EMBEDDING LOAD AUDIT` | `RecordStudentEmbeddingLoad` | 1 |
   | `DUPLICATE LOAD DETECTED` | same, conditional | 0+ (expected 0) |
   | `AI17 ONNX RUNTIME INFERENCE AUDIT` | `RecordOnnxInference` | 1 (detection) + N (recognition, one per face) |
   | `NATIVE MEMORY GROWTH DETECTED` | same, conditional | 0+ |
   | `AI17 CANDIDATE MATCHING MEMORY AUDIT` | `RecordMatching` | 1 |
   | `MATCHING MEMORY SPIKE` | same, conditional | 0+ |
   | `AI17 FORENSICS AUDIT SUMMARY` | `FinalizeAudit` | 1 (terminal) |
   | `PIPELINE ENTRY`, `QUEUE TRACE`, `Recognition Started/Completed`, `Recognition Memory Summary` | AI15/AI16 (`RecognitionPipelineDiagnostics`) | 1 each |
4. Also capture, for the same time window: the Render service's OOM/restart event (if any) from the
   Render dashboard's Events tab, and one `/health` + `/health/ready` response (these expose
   `peakNativeEstimateMB`/`peakWorkingSetDeltaMB` from AI16.RUNTIME.4 plus the standard AI15 summary
   fields — see `Program.cs`'s health endpoint projection).
5. Save the raw text as-is (no reformatting) into `docs/evidence/` (or wherever this repo's convention
   for non-code artifacts is) and re-run this analysis against that file.

---

## Task 1 — Complete execution timeline

**Template** (one row per checkpoint, in chronological order, all from `AI17 STAGE CHECKPOINT` blocks
sharing one `ExecutionTraceId`):

| Stage | Start Time | Finish Time | Duration | Working Set MB | Private MB | Managed Heap MB | Native Estimate MB | Thread Count | Gen0 | Gen1 | Gen2 | Peak So Far MB | ExecutionTraceId |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Queue Received | *pending* | — | — | | | | | | | | | | |
| Image Download Started/Finished | | | | | | | | | | | | | |
| Image Decode Started/Finished | | | | | | | | | | | | | |
| Face Detection Started/Finished | | | | | | | | | | | | | |
| Face Crop Loop Begin | | | | | | | | | | | | | |
| Before/After Face Crop (×N faces) | | | | | | | | | | | | | |
| Before/After Embedding Generation (×N faces) | | | | | | | | | | | | | |
| Before/After Student Embedding Load | | | | | | | | | | | | | |
| Before/After Matching | | | | | | | | | | | | | |
| Before/After Database Save | | | | | | | | | | | | | |
| Completed (or Completed (Failed) if OOM/exception interrupted the job) | | | | | | | | | | | | | |

**Note:** if the job actually OOM'd, the timeline will be *truncated* — the last logged checkpoint
before the process was killed is itself evidence (it brackets exactly which stage was executing when
the OOM killer fired, since Render kills the process, it doesn't let `catch`/`finally` run cleanup).
Cross-reference the last checkpoint's timestamp against the Render OOM event's timestamp to confirm
they align (a large gap would suggest the OOM happened somewhere AI17 has no checkpoint, e.g. mid-decode
of an unusually large image, which is itself informative).

## Task 2 — Largest single-checkpoint increase

**Method:** compute `WorkingSetBytes[i] - WorkingSetBytes[i-1]` for every consecutive pair of
checkpoints in Task 1's table; the AI17 instrumentation itself already flags any pair exceeding 25 MB
with `MEMORY SPIKE DETECTED` at capture time — so this task is really "list every `MEMORY SPIKE
DETECTED` line from the raw log, in order, plus the one non-flagged largest delta if none exceed
25 MB."

| Rank | Stage transition | Increase MB | Running Working Set MB | Running Private MB | Running Native Estimate MB | Running Managed Heap MB |
|---|---|---|---|---|---|---|
| *pending* | | | | | | |

## Task 3 — GC behavior analysis

**Available fields per checkpoint:** `Gen0`/`Gen1`/`Gen2` collection *counts* (cumulative, from
`GC.CollectionCount(n)`) and `ManagedHeapMB` (from `GC.GetTotalMemory(false)`) are already captured at
every checkpoint. **Not currently captured** by any AI15/16/17 instrumentation shipped so far:
`GC.GetGCMemoryInfo()` fragmentation bytes, high-memory-load threshold, or `MemoryLoadBytes` — these
specific sub-fields this task asks for do not exist in any log line today. If they are required, that
is itself a finding: **AI17.RUNTIME.1–.6 do not currently log GC fragmentation/memory-load detail**,
only heap size and collection counts. This gap should be flagged to whoever requested Task 3, not
silently filled with estimated numbers.

| Checkpoint | GC.GetTotalMemory (Managed Heap MB) | Gen0 Δ | Gen1 Δ | Gen2 Δ | Fragmented Bytes | Memory Load |
|---|---|---|---|---|---|---|
| *pending — Fragmented Bytes/Memory Load columns cannot be filled without new instrumentation* | | | | | *not logged* | *not logged* |

**Determinations requiring real data:**
- *Was GC able to recover memory?* — compare Managed Heap MB immediately after a Gen2 collection count
  increments vs. immediately before; cannot answer without real Gen-count deltas aligned to heap size.
- *Is memory continuously increasing?* — read directly off Task 1's Working Set/Private/Native columns.
- *Is fragmentation increasing?* — cannot answer at all without capturing `GC.GetGCMemoryInfo()`, which
  is not currently logged anywhere in this codebase.

## Task 4 — ImageSharp lifetime analysis

**Source:** `AI17 Object Created`/`AI17 Disposable Audit` lines for `ImageSharp Image` and
`ImageSharp Clone` object types (added in AI17.RUNTIME.2/.4).

| Field | Where to read it | Value |
|---|---|---|
| Original Image Size | `AI17 Object Created: ImageSharp Image (source image) ... [WxH]` | *pending* |
| Decoded Size | `[WxH]` × 3 bytes (Rgb24) from the same line | *pending* |
| Clone Size | `AI17 Object Created: ImageSharp Clone (detection-resize clone) ... [WxH]` — **note:** per the code (`InsightFaceImageMath.BuildDetectionInput`), this dimension is logged at *creation* time, i.e. before `Resize()` runs, so it should equal the *original* image's dimensions, not 640×640 | *pending* |
| Resize Size | Not separately logged as a distinct object — the clone is resized in place; the *tensor* it feeds is fixed at 640×640 (`DenseTensor<float>` "detection input") | 640×640×3 by construction |
| Maximum Concurrent Images | `AI17 FORENSICS AUDIT SUMMARY` → `Peak Concurrent ImageSharp Images` | *pending* |
| Longest Living Image | Largest `Elapsed Lifetime` value across all `AI17 Disposable Audit` lines with `ObjectType=ImageSharp Image` or `ImageSharp Clone` | *pending* |
| Largest Image | Cross-reference dimensions from `AI17 Object Created` lines | *pending — structurally expected to be "source image", see AI17.RUNTIME.7 §5* |
| Dispose Timeline | Ordered list of all matching `AI17 Disposable Audit` lines by `Disposal Stage` | *pending* |

**Determination — did any image remain alive longer than expected?** The instrumentation already
answers this automatically: any `ImageSharp Image`/`Clone` disposal logged with `Elapsed Lifetime`
over 2000 ms is auto-flagged `LONG LIVED DISPOSABLE` by `RecognitionForensicsAudit`
(`LongLivedDisposableThresholdMs`). **Also check** for `WARNING: Face crop retained.` lines — these are
*expected* once per face (by design, see AI17.RUNTIME.7 §7) and are not evidence of a problem on their
own; only `LONG LIVED DISPOSABLE` or a nonzero "Objects Never Disposed" count in the
`AI17 FORENSICS AUDIT SUMMARY` block would indicate a genuine issue.

## Task 5 — ONNX Runtime analysis

**Source:** `AI17 ONNX RUNTIME INFERENCE AUDIT` blocks (one for detection, one per face for
recognition), each already carrying every field this task asks for.

| Inference | Model | Native Before MB | Native After MB | Δ | Duration ms | Disposed Outputs | Session Reused | Tensor Reused |
|---|---|---|---|---|---|---|---|---|
| Detection | `det_10g.onnx` | *pending* | *pending* | *pending* | *pending* | (always `True` under current code) | `True` | `False` |
| Recognition (×N faces) | `w600k_r50.onnx` | *pending* | *pending* | *pending* | *pending* | (always `True` under current code) | `True` | `True` |

**Peak Native (per inference):** not directly logged as a third sample — only before/after are
captured (a synchronous `session.Run()` call has no safe intermediate sampling point without a
concurrent polling thread, same limitation documented for matching in AI17.RUNTIME.7 §10). Peak-across-
the-whole-job Native Estimate is available from Task 1's checkpoint table instead.

**Determination — did native memory continuously grow, or stay stable?** Read directly from whether
any `NATIVE MEMORY GROWTH DETECTED` lines appear, and whether the Native Before value of inference
*k+1* is higher than the Native After value of inference *k* (would indicate growth is not being
reclaimed between calls even when each individual call looks flat).

## Task 6 — Student Embedding loading analysis

**Source:** the single `AI17 STUDENT EMBEDDING LOAD AUDIT` block per job.

| Field | Value |
|---|---|
| Student Count | *pending* |
| Embedding Count | *pending* |
| Embedding Bytes | *pending* |
| Materialized Objects | *pending* |
| EF Tracking | `False` (AsNoTracking) — fixed by code, confirmed in AI17.RUNTIME.7 §9, not log-dependent |
| Navigation Properties | `None` — fixed by code | 
| Duplicate Loads | Check for `DUPLICATE LOAD DETECTED` — expected absent |

**Determination — can student embedding loading be eliminated as a memory source?** Structurally yes
for typical class sizes (AI17.RUNTIME.7 §9 computed ~80 KB total for 40 students), but this should be
confirmed against the *actual* class size in the production job being analyzed — a much larger
cohort/section could change this conclusion, which is exactly why it must be read from the real
`Student Count`/`Embedding Bytes` fields above, not assumed.

## Task 7 — Matching analysis

**Source:** the single `AI17 CANDIDATE MATCHING MEMORY AUDIT` block per job.

| Field | Value |
|---|---|
| Detected Faces | *pending* |
| Candidate Students | *pending* |
| Comparisons | *pending* (= Detected Faces × Candidate Students) |
| Temporary Allocations | *pending* (logged as an estimate in the audit line itself) |
| Working Set Before Matching | *pending* |
| Working Set After Matching | *pending* |
| Peak During Matching (approx.) | *pending* |

**Determination — did matching contribute meaningfully?** Check for `MATCHING MEMORY SPIKE`
(threshold 30 MB); absent that, compare the Before/After delta against the deltas recorded for
Image Decode/Face Detection in Task 1 — AI17.RUNTIME.7 §10's structural expectation is that matching
is negligible by comparison, but this must be confirmed with the real deltas, not assumed.

## Task 8 — Disposable Audit analysis

**Source:** every `AI17 Disposable Audit` line, plus the `LONG LIVED DISPOSABLE`/`UNDISPOSED ONNX
OUTPUT` warnings and the final `AI17 FORENSICS AUDIT SUMMARY`'s "Objects Never Disposed" count.

| Object | Creation Stage | Disposal Stage | Lifetime ms | Classification |
|---|---|---|---|---|
| *pending — populate one row per disposable logged* | | | | Correct / Suspicious / Leak Candidate |

**Classification rule (apply once real data exists):**
- **Correct** — disposed, lifetime consistent with its documented expected span (e.g. an "aligned face
  N" living from crop through webp-encode is *correct*, not suspicious — see AI17.RUNTIME.7 §7).
- **Suspicious** — disposed, but lifetime exceeds `LongLivedDisposableThresholdMs` (2000 ms) without an
  obvious reason tied to a slow stage in Task 1's timeline.
- **Leak Candidate** — appears in the `AI17 FORENSICS AUDIT SUMMARY`'s "Objects Never Disposed" set, or
  logged as `LONG LIVED DISPOSABLE`/`UNDISPOSED ONNX OUTPUT`.

**Ranking of leak candidates:** cannot be produced — zero candidates exist without real log data.

## Task 9 — Root Cause Analysis

**This task cannot be responsibly completed without production evidence**, per this milestone's own
"do not speculate" constraint. What can be stated honestly:

| # | Candidate cause | Confidence | Evidence |
|---|---|---|---|
| 1 | Image Clone (`BuildDetectionInput` clones at full resolution before resizing) | **N/A — no production log evidence yet.** A *structural* hypothesis exists from static code/size analysis in AI17.RUNTIME.7 §5/§11 (a 4000×3000 photo → two concurrent ~34 MB buffers), but that document explicitly labels every number **[estimate]** and is not a substitute for a measured `AI17 Object Created`/checkpoint delta from a real job. | AI17.RUNTIME.7 §5, §11 (code-derived, not log-derived) |
| 2 | Native ONNX Runtime growth | **N/A — no production log evidence yet.** `EnableCpuMemArena=false`/`EnableMemoryPattern=false` (AI16.RUNTIME.1) predicts stability, but this is a configuration expectation, not a measurement — only a real `NATIVE MEMORY GROWTH DETECTED` count (or absence) from Task 5 can confirm or refute it. | None captured |
| 3 | Matching | **N/A — no production log evidence yet.** Structurally expected to be cheap (Task 7 discussion, AI17.RUNTIME.7 §10) but unconfirmed. | None captured |
| 4 | EF Core / student embedding loading | **N/A — no production log evidence yet.** Structurally small for typical class sizes (Task 6 discussion), but depends on the actual cohort size in the analyzed job. | None captured |
| 5 | Managed memory leak (disposables never released) | **N/A — no production log evidence yet.** The AI17.RUNTIME.2 audit mechanism exists specifically to detect this via `LONG LIVED DISPOSABLE`/"Objects Never Disposed", but zero real audit output exists to rank against. | None captured |

**No ranking is defensible today.** Re-run this task once the Evidence Capture Protocol above has
produced a real log; at that point every row above should be replaced with a genuine confidence
derived from Tasks 1–8's actual numbers, not this framework's placeholders.

## Task 10 — Is Render Starter (512 MB) sufficient?

**Cannot be determined without a measured peak memory figure from a real job.** The AI15/AI16
`/health` endpoint already exposes exactly the numbers this task needs
(`peakWorkingSetMB`/`peakNativeEstimateMB`/`peakManagedHeapMB` and the AI16.RUNTIME.4 additions
`peakNativeEstimateMB`/`peakWorkingSetDeltaMB`) — but no `/health` response was captured for this
analysis. Once one is:

- **Safe** would require peak Working Set to sit comfortably below 512 MB with meaningful headroom
  under the *largest* photo/class-size combination the platform actually receives in production, not
  just the job that happened to be captured.
- **Marginal**/**Insufficient** and the Minimum/Recommended RAM/Expected Headroom figures all follow
  directly from that same peak-Working-Set measurement plus a safety margin — none of which can be
  responsibly estimated without it.

## Task 11 — Architecture Decision (AI18 focus)

**Cannot be responsibly selected among (A) ImageSharp, (B) ONNX Runtime, (C) recognition pipeline, (D)
Render memory upgrade, (E) combination — without Task 9's root-cause ranking being evidence-backed
first.** Selecting an architecture direction from an unconfirmed hypothesis risks optimizing the wrong
subsystem while the Render Starter instance continues to OOM in production. AI17.RUNTIME.7 §12 already
lists candidate optimizations *if* the Image Clone hypothesis is confirmed — but that document was
explicit that those are AI18 candidates pending validation, not a decision.

## AI18 Readiness Assessment

| Gate | Status |
|---|---|
| Instrumentation shipped (AI17.RUNTIME.1–.6) | ✅ Complete |
| Structural hypothesis documented (AI17.RUNTIME.7) | ✅ Complete, explicitly marked as estimate-only |
| Production evidence captured | ❌ **Not done — blocking gate for AI18** |
| Root cause confirmed from evidence (this document, Task 9) | ❌ **Blocked on the gate above** |
| Architecture decision (this document, Task 11) | ❌ **Blocked on the gate above** |
| **Recommendation** | Do not start AI18 implementation work until the Evidence Capture Protocol above has been executed and this document has been re-run with real data substituted for every *pending* cell. |

---

## Verification

- **No code modified.** This milestone made zero changes to any `.cs`, `.json`, or configuration file —
  it is a single new documentation file (`docs/AI17_RUNTIME8_FORENSIC_ANALYSIS.md`).
- **No behavior modified.** N/A — no code touched.
- **No recognition logic modified.** N/A — no code touched.
- **No AI thresholds modified.** N/A — no code touched.
- **No matching algorithm modified.** N/A — no code touched.
- **Build verification:** not performed, per this milestone's explicit instruction that it is not
  required for a documentation-only deliverable.
