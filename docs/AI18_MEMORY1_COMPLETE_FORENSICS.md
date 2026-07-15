# AI18.MEMORY.1 — Complete Memory Snapshot Audit

**Scope:** Forensic memory instrumentation only. **No business logic, thresholds, matching, AI models,
ImageSharp behavior, ONNX Runtime behavior, database queries, repositories, DI lifetimes, threading,
queues, GC settings, Render configuration, or Docker configuration were changed.** Every new call added
by this milestone reads process/GC state, records a caller-supplied size estimate, or logs — none of
them influence any recognition, matching, or persistence decision.

**What this milestone delivers:** a new diagnostics-only service, `RecognitionMemoryAudit`
(`Abhyanvaya.Infrastructure/Diagnostics/MemoryAudit/`), wired into the same three files AI17.RUNTIME
already instrumented (`ClassroomRecognitionPipeline.cs`, `InsightFaceEngine.cs`,
`DependencyInjection.cs`), that produces a **complete, per-stage, per-object memory profile** of one
classroom recognition run — richer than AI17.RUNTIME's `RecognitionForensicsAudit` (adds GC heap
fragmentation, GC memory load, OS handle count, process processor time, and four independent running
peaks to every snapshot), plus a generic heavy-object registry, a **TOP 20 MEMORY CONSUMERS** table, and
a final **MEMORY FORENSICS REPORT** block.

**Status of underlying data:** this document is written from static code analysis of the pipeline this
audit instruments — it explains exactly what the audit records and how to read it, and is **not** backed
by a captured Render production log (none exists in this workspace yet; see AI17.RUNTIME.8 for the same
caveat). Section 12 ("Investigation checklist") is the literal procedure to run once a production run has
been captured; every other section is the analysis framework the resulting logs plug into.

---

## 1. Architecture

### 1.1 Why a separate service from AI17.RUNTIME

| | `RecognitionPipelineDiagnostics` (AI15/16) | `RecognitionForensicsAudit` (AI17.RUNTIME) | `RecognitionMemoryAudit` (AI18.MEMORY.1) |
|---|---|---|---|
| Activation | `Begin()` | Implicit on first `Checkpoint()` | **Explicit `Begin()`** (per prompt: "completely inert until Begin()") |
| Snapshot fields | WorkingSet, Private, ManagedHeap, ThreadId | + Native Estimate, Gen0/1/2, Process Thread Count | + **GC Fragmentation, GC Memory Load, Handle Count, Processor Time, 4× running Peak fields** |
| Object tracking | Named create/dispose events only | Named create/dispose + lifetime + long-lived flag | **Generic `RegisterObject`/`DisposeObject` registry with size, Top-20 ranking, per-category "largest" tracking** |
| EF/embedding audit | — | Field-level (student/embedding counts) | + **explicit "was this materialization structurally impossible to inflate" reasoning per query** |
| Final output | Recognition Memory Summary | Forensics Audit Summary (long-lived-disposable sweep) | **TOP 20 MEMORY CONSUMERS + MEMORY FORENSICS REPORT (peaks, 15 "Largest ___" fields)** |
| Lifetime | Scoped | Scoped | Scoped |

Keeping this as a fourth, independent Scoped service (rather than extending `RecognitionForensicsAudit`)
means **zero regression risk** to every AI15/AI16/AI17 log line already shipped — none of that code was
touched. The new service is purely additive: every call site that already has an AI17 `_forensics.*` call
next to it now also has an AI18 `_memoryAudit.*` call, reading the same process state a second time with
richer fields.

### 1.2 Component diagram

```
                         ┌───────────────────────────────────────┐
                         │      IRecognitionMemoryAudit           │
                         │  (Abhyanvaya.Infrastructure.Diagnostics │
                         │            .MemoryAudit)                │
                         ├───────────────────────────────────────┤
                         │ Begin()                                 │
                         │ Snapshot(stage, faceNumber?)            │
                         │ RegisterObject(type,bytes,stage) → id   │
                         │ DisposeObject(id)                       │
                         │ RecordEntityFrameworkQuery(...)         │
                         │ RecordStudentEmbeddingLoad(...)         │
                         │ RecordOnnxInference(...)                │
                         │ RecordMatchingMemory(...)                │
                         │ RecordDatabaseSave(phase, ...)          │
                         │ Complete()                               │
                         └───────────────────────────────────────┘
                                          ▲
                                          │ Scoped (one instance per job)
                    ┌─────────────────────┴─────────────────────┐
                    │                                             │
     ┌──────────────┴───────────────┐                ┌───────────┴────────────┐
     │ ClassroomRecognitionPipeline  │                │   InsightFaceEngine     │
     │  .ProcessAsync                │                │   .DetectAsync           │
     │  .LoadStudentEmbeddingsAsync  │                │   .DetectFaces           │
     │                               │                │   .ExtractEmbedding      │
     │ Begin / Queue Received        │                │ Image Decode / Face      │
     │ Image Download / Face         │                │ Detection / Face Crop    │
     │ Detection Complete            │                │ Loop / ONNX before-after │
     │ EF queries / Student          │                │ per face                 │
     │ Embedding audit               │                └─────────────────────────┘
     │ Matching / Thumbnail loop     │
     │ Database Save / Completed     │
     └───────────────────────────────┘
```

`MemoryAuditSnapshot` (`MemoryAuditSnapshot.cs`) is the immutable, stateless per-call reading — it never
holds history itself; `RecognitionMemoryAudit` owns all running state (previous snapshot, four peaks,
per-category "largest" trackers, the object registry) as private instance fields, isolated per DI scope.

### 1.3 Activation and gating

* Gated by the same `RecognitionDiagnosticsOptions.Enabled` flag AI15/16/17 already use — no new
  configuration section was added (STEP 13: no configuration changes).
* Inert (every method a no-op) until `ClassroomRecognitionPipeline.ProcessAsync` calls `Begin()` as its
  very first diagnostic action (immediately after `_forensics.Checkpoint`-style AI17 wiring, before the
  session even loads).
* `InsightFaceEngine` is shared with the unrelated student-enrollment pipeline
  (`GenerateSingleFaceEmbedding`) and the debug `FaceDetectionController` endpoint — neither of those
  paths ever calls `Begin()`, so every `_memoryAudit.*` call inside `InsightFaceEngine` is a guaranteed
  no-op there, exactly mirroring how AI17's `_forensics` calls already behave in those same methods.
* Every public method is wrapped in `try { } catch (Exception ex) { SafeLogInternalFailure(ex); }` — a
  diagnostics failure can only ever produce one extra warning log line, never break the recognition job.

---

## 2. Memory flow diagram

```
Begin() ──► Snapshot("Queue Received")
   │
   ▼
Snapshot("Image Download Started") ──► ReadObjectAsync/ReadVariantAsync ──► Snapshot("Image Download Finished")
   │                                                                              RegisterObject(Byte Array, classroom photo)
   ▼
InsightFaceEngine.DetectAsync
   ├─ Snapshot("Image Decode Started") ──► Image.Load<Rgb24> ──► Snapshot("Image Decode Finished")
   │                                                                RegisterObject(ImageSharp Image, source)
   ├─ Snapshot("Face Detection Started")
   │     └─ DetectFaces: RecordOnnxInference(det_10g.onnx, before/after)
   ├─ Snapshot("Face Detection Finished")
   ├─ Snapshot("Face Crop Loop Begin")
   └─ foreach face:
        ├─ Snapshot("Before Face Crop", N) ──► AlignFace ──► Snapshot("After Face Crop", N)
        │                                                       RegisterObject(Face Crop, N)
        ├─ Snapshot("Before Embedding Generation", N)
        │     └─ ExtractEmbedding: RecordOnnxInference(w600k_r50.onnx, before/after)
        ├─ Snapshot("After Embedding Generation", N)
        │                                                       RegisterObject(Embedding Array, N) → Dispose
        └─ Snapshot("After Thumbnail Encode", N)
                                                                  RegisterObject(MemoryStream, N) → Dispose
                                                                  RegisterObject(Byte Array webp, N) → Dispose
             Snapshot("After Dispose", N) ──────────────────────  DisposeObject(Face Crop, N)
   │
   ▼
Snapshot("Before Student Embedding Load")
   └─ LoadStudentEmbeddingsAsync
        ├─ RecordEntityFrameworkQuery("Students (id projection)")
        ├─ RecordEntityFrameworkQuery("StudentFaceEmbeddings (AsNoTracking)")
        └─ RecordStudentEmbeddingLoad(...) + RegisterObject(StudentEmbedding Collection)
Snapshot("After Student Embedding Load")
   │
   ▼
Snapshot("Before Matching") ──► IFaceMatcher.Match ──► Snapshot("After Matching")
                                    RecordMatchingMemory(before/after)
   │
   ▼
Snapshot("Before Thumbnail Persistence")
   └─ foreach face: Snapshot(Before/After Thumbnail Persistence, N)
        RegisterObject(Byte Array, N) → PersistFaceThumbnailAsync → DisposeObject(N)
Snapshot("After Thumbnail Persistence")
   RegisterObject(AttendanceRecognition Collection)
   │
   ▼
Snapshot("Before Database Save") ──► RecordDatabaseSave("Before")
   └─ SaveChangesAsync
RecordDatabaseSave("After") ──► Snapshot("After Database Save")
   │
   ▼
Snapshot("Completed") ──► Complete()
                              ├─ Final Snapshot
                              ├─ TOP 20 MEMORY CONSUMERS
                              ├─ MEMORY FORENSICS REPORT
                              └─ "STILL ALIVE AT COMPLETION" sweep for undisposed registrations
```

Failure path: the `catch` block in `ProcessAsync` calls `Snapshot("Completed (Failed)")` then `Complete()`
— the audit always produces a final report, whether the job succeeded or threw.

---

## 3. Allocation diagram (what gets registered, and why)

```
Byte Array   ── classroom photo bytes (Image Download) ─────────────── RegisterObject only, never disposed
             ── per-face thumbnail webp bytes (Thumbnail Encode)       (survives inside DetectedFaceDto;
                                                                          re-registered/disposed around the
                                                                          upload call in the pipeline)
ImageSharp
  Image      ── source classroom image (Image Decode) ────────────────  Disposed at end of DetectAsync
  Face Crop  ── per-face aligned 112×112 crop (After Face Crop) ──────  Disposed at loop-iteration end
MemoryStream ── per-face WebP encode buffer (8 KB initial capacity) ──  Disposed via `await using`
Embedding
  Array      ── per-face 512-float ArcFace embedding ─────────────────  Registered+disposed immediately
                                                                          (embedding itself lives on in
                                                                          DetectedFaceDto.Embedding)
StudentEmbedding
  Collection ── all active StudentFaceEmbedding rows for the class ──  Registered, never explicitly
                                                                          disposed (managed List<T>, GC-
                                                                          collectible once matching + the
                                                                          method's local variable go out
                                                                          of scope)
AttendanceRecognition
  Collection ── the `recognitions` list built for this job ──────────  Registered, same GC-collectible
                                                                          reasoning as above
```

`RegisterObject`/`DisposeObject` are the STEP 3 generic primitives — every registration is classified
into one or more of 15 "largest" buckets (Object, Collection, Disposable, Image, Tensor, Float Array,
Byte Array, Student Graph, EF Graph, ImageSharp/ONNX/Matching/Thumbnail/Database Allocation) purely by
string-matching the object type and stage label (`RecognitionMemoryAudit.ClassifyRegisteredObject`), so
the final report can answer "which category grew the most" without a rigid taxonomy baked into the
registration call sites themselves.

---

## 4. Object lifetime diagram

```
Object              Created At                    Disposed At                  Typical Lifetime
──────────────────  ─────────────────────────────  ────────────────────────────  ─────────────────
Source Image         Image Decode Finished          End of DetectAsync            Whole detection+
                                                                                    crop-loop duration
Face Crop (Face N)    After Face Crop (N)            After Dispose (N), same       One loop iteration
                                                       iteration
Embedding Array (N)   After Embedding Generation(N)  Immediately (same call)       ~0 ms (audit-only;
                                                                                     the float[] itself
                                                                                     survives via DTO)
WebP MemoryStream(N)  After Embedding Generation(N)  Same iteration (await using)  One WebP encode
Thumbnail byte[](N)   After Thumbnail Encode (N)     Re-registered in pipeline's   Whole remaining job
                                                       thumbnail-persistence loop    (until upload)
StudentEmbedding      After Student Embedding Load    Never explicit — GC-          Whole matching +
  Collection                                           collectible once             remainder of job
                                                        `studentEmbeddings` local
                                                        goes out of scope
AttendanceRecognition After Thumbnail Persistence      Never explicit — GC-          Whole DB-save phase
  Collection                                            collectible after
                                                         SaveChangesAsync
```

Anything left in the "Disposed At: Never explicit" column is **expected**, not a leak — these are plain
managed `List<T>` collections with no `IDisposable` implementation; they are reclaimed by the GC once
their last reference (a local variable) goes out of scope at method return, same as any other .NET
object. The audit's "STILL ALIVE AT COMPLETION" warning (STEP 11) only fires for objects registered via
`RegisterObject` that were never paired with a `DisposeObject` call **and are of a disposable-flavored
type** (Image/Stream/Tensor/NamedOnnxValue/DisposableCollection/Crop) — plain collections are excluded
from that specific warning by design, since flagging every un-disposed `List<T>` would be noise, not a
finding.

---

## 5. Stage memory diagram (illustrative shape, pending live capture)

Same caveat as AI17.RUNTIME.7 §2: Working Set is monotonically non-decreasing under normal .NET
operation, so the expected shape across one job is a staircase, not a sawtooth:

```
Working Set (relative, one job)

 High │                                                    ┌───────┐
      │                                             ┌──────┘       └──
      │                                      ┌──────┘
      │                       ┌──────────────┘
      │                ┌──────┘
      │         ┌──────┘
 Low  │──────────
      └────────────────────────────────────────────────────────────────►
        Queue  Image   Image   Face      Face Crop    Student   Matching  Thumbnail  DB Save
        Recv   DL      Decode  Detect    Loop (×N)     Embed              Persist
```

Once a real run is captured, replace this with the actual sequence of `WorkingSetMB` values logged by
every `AI18 MEMORY SNAPSHOT` block, in order — that is the true stage memory diagram for that job.

---

## 6. Example logs

### 6.1 One stage snapshot (`Snapshot("After Face Detection Finished")`)

```
====================================================
AI18 MEMORY SNAPSHOT
  Stage                               : Face Detection Finished
  Timestamp                            : 2026-07-14T12:03:41.201Z
  Execution Trace Id                   : TRACE-20260714-120340-9F3A21B0
  Elapsed                               : 812.4 ms
  Working Set                           : 187.42 MB
  Private Memory                       : 191.05 MB
  Managed Heap                         : 61.83 MB
  GC Heap Fragmentation                : 3.10 MB
  GC Memory Load                       : 412.77 MB
  Native Estimate                      : 129.22 MB
  Gen0 / Gen1 / Gen2                    : 14 / 3 / 0
  Thread Count                          : 27
  Handle Count                         : 214
  Processor Time                       : 1904.6 ms
  Peak Working Set                     : 187.42 MB
  Peak Private Memory                  : 191.05 MB
  Peak Managed Heap                    : 61.83 MB
  Peak Native Estimate                 : 129.22 MB
  Increase Since Previous Stage       : 41.08 MB (from 'Face Detection Started')
  Increase Since Pipeline Start        : 52.94 MB
====================================================
```

### 6.2 Object registration / disposal pair

```
AI18 OBJECT REGISTERED: Id=7 Type=Face Crop ApproxBytes=37632 Stage=After Face Crop (Face 3) ExecutionTraceId=TRACE-20260714-120340-9F3A21B0
AI18 OBJECT DISPOSED: Id=7 Type=Face Crop ApproxBytes=37632 CreatedStage=After Face Crop (Face 3) DisposedStage=After Dispose (Face 3) LifetimeMs=41.2 ExecutionTraceId=TRACE-20260714-120340-9F3A21B0
```

### 6.3 ONNX inference audit

```
====================================================
AI18 ONNX RUNTIME AUDIT
  Model                               : det_10g.onnx
  Input Tensor Shape                  : [1x3x640x640]
  Output Tensor Shape                 : 9 tensors
  Tensor Bytes (in+out, approx.)       : 4953600 bytes (4.72 MB)
  Disposable Output Count             : 9
  Native Estimate Before               : 118.60 MB
  Native Estimate After                : 129.22 MB
  Peak Native Increase                 : 10.62 MB
  Inference Duration                   : 71 ms
  Outputs Disposed                     : True
====================================================
```

### 6.4 Top 20 / final report (excerpt)

```
====================================================
AI18 TOP 20 MEMORY CONSUMERS
  Total Objects Registered             : 34
  Total Objects Disposed               : 31
  #1  Type=ImageSharp Image             Bytes=   6912000 LifetimeMs=   812.4 Disposed=True  CreatedStage=Image Decode Finished       DisposedStage=End of DetectAsync            TraceId=TRACE-...
  #2  Type=StudentEmbedding Collection  Bytes=    492544 LifetimeMs=  1980.1 Disposed=False CreatedStage=After Student Embedding Load DisposedStage=(not disposed)                TraceId=TRACE-...
  ...
====================================================
====================================================
MEMORY FORENSICS REPORT
  ExecutionTraceId                     : TRACE-20260714-120340-9F3A21B0
  Peak Working Set                     : 241.88 MB
  Peak Private Memory                  : 246.30 MB
  Peak Managed Heap                    : 74.15 MB
  Peak Native Estimate                 : 172.15 MB
  Largest Stage Increase                : 8912896 bytes (8.50 MB) — Image Decode Started -> Image Decode Finished
  Largest Object                       : 6912000 bytes (6.59 MB) — ImageSharp Image @ Image Decode Finished
  ...
====================================================
```

---

## 7. How to identify leaks

A **leak** in this context means a managed or native allocation that survives past the point it should
have been reclaimed, across *multiple* jobs (a single long-lived object inside one job is not a leak by
itself — see §4).

1. Compare `AI18 OBJECT STILL ALIVE AT COMPLETION` lines across many jobs' logs. A leak shows the same
   `Type` appearing in this list **every job**, with a growing `AliveMs` if the process itself isn't
   restarted between jobs.
2. Compare `Peak Managed Heap` across consecutive jobs on the same process instance (same PID, visible in
   Render logs). A monotonically increasing peak across jobs (not just within one job) with no
   corresponding drop after `GC.Collect()` (AI16.RUNTIME.5's forced-GC diagnostic, if enabled) indicates
   a true managed leak, not just normal per-job churn.
3. Compare `Peak Native Estimate` the same way. A native leak is `Private Memory` growing job-over-job
   while `Managed Heap` stays flat — see §10.

## 8. How to identify spikes

A **spike** is a single stage where memory jumps far more than its neighbors within one job.

1. Read every `Increase Since Previous Stage` line in order for one `ExecutionTraceId`.
2. The stage with the largest value is also reported once, cleanly, in the final report's
   `Largest Stage Increase` field — no need to scan the whole log by hand.
3. Cross-reference that stage name against §3/§4 to identify which object(s) were registered at that
   exact stage — that object is almost certainly the spike's cause.

## 9. How to identify fragmentation

1. Read `GC Heap Fragmentation` (from `GC.GetGCMemoryInfo().FragmentedBytes`) across the job's snapshots.
2. A healthy job shows this value staying roughly flat or shrinking after Gen2 collections (visible via
   the `Gen0/Gen1/Gen2` counters incrementing between snapshots).
3. A fragmentation problem shows `GC Heap Fragmentation` growing monotonically *and* `Gen2` count staying
   flat for a long stretch — meaning the GC isn't compacting because it hasn't needed to run a full
   collection, but free space is scattered across many small holes (a classic symptom of many
   short-lived, varying-size allocations — exactly what `image.Clone()` + per-face crop encoding
   produces, per AI16.RUNTIME.2/AI17.RUNTIME.4's findings).

## 10. How to identify native leaks

1. `Native Estimate` = `Private Memory - Managed Heap` (same formula as AI16/AI17, kept identical for
   comparability). A native leak shows this value increasing **within a single job** across ONNX
   inference calls (see `AI18 ONNX RUNTIME AUDIT` blocks' `Native Estimate Before`/`After`) without ever
   coming back down, even after the corresponding `outputs` `using` block disposes.
2. If `Native Estimate After` > `Native Estimate Before` on **every single inference** (not just
   occasionally, which is normal allocator behavior), and the gap widens as more faces are processed in
   the same job, that is native memory growth consistent with either an ONNX Runtime arena that never
   shrinks (see AI16.RUNTIME.1's `EnableCpuMemArena` findings) or genuinely undisposed native handles —
   cross-check against `UNDISPOSED ONNX OUTPUT` warnings from this same audit and AI17.RUNTIME.5's
   `NATIVE MEMORY GROWTH DETECTED` warnings, which measure the identical metric independently.

## 11. How to identify EF object graph inflation

1. Every `AI18 ENTITY FRAMEWORK AUDIT` block reports `Student Photos Loaded` and
   `Navigation Collections Loaded`. Both queries this audit currently covers
   (`Students (id projection)`, `StudentFaceEmbeddings (AsNoTracking)`) report **False** for both,
   because neither query has a `.Include()` and the Students query projects only `s.Id` — this is a
   structural fact verifiable directly in `ClassroomRecognitionPipeline.LoadStudentEmbeddingsAsync`, not
   a runtime guess.
2. If a *future* query is added that reports either flag `True`, or if `Entities Materialized` is
   larger than the number of rows the calling code actually needs, that is EF inflation — the fix
   (documentation only, per STEP 13) would be to add `.Select(...)` projections or remove an accidental
   `.Include()`.
3. `EF OBJECT GRAPH INFLATION SUSPECTED` is logged automatically by the audit whenever either flag is
   `True` — no manual log-scanning needed to catch a regression here.

## 12. How to identify ImageSharp inflation

1. Compare the number of `RegisterObject("ImageSharp Image"/"Face Crop", ...)` calls against the number
   of faces detected (`session.DetectedFaces`, visible in the `PIPELINE ENTRY`/summary logs). Expected
   count = 1 source image + N face crops. More than that indicates an unexpected clone or duplicate
   decode.
2. `WARNING: Multiple classroom images resident` (still emitted by AI17's `RecognitionForensicsAudit`,
   unchanged) is the authoritative signal for this — this AI18 audit's `Largest Image`/
   `Largest ImageSharp Allocation` fields in the final report corroborate it with an actual byte figure.
3. `image.Clone()` inside `InsightFaceImageMath.BuildDetectionInput` (documented in AI16.RUNTIME.2 and
   AI17.RUNTIME.7 §7) creates a full-resolution copy before resizing — this shows up as a large
   `Increase Since Previous Stage` at "Face Detection Started" without a corresponding
   `RegisterObject` call (that clone is internal to `BuildDetectionInput` and not separately registered
   by this audit) — cross-reference against the `Largest Stage Increase` field if that stage dominates.

## 13. How to identify ONNX inflation

1. Every `AI18 ONNX RUNTIME AUDIT` block's `Tensor Bytes (in+out, approx.)` field is the audit's estimate
   of one inference call's own tensor payload. Compare across the two models: detection
   (`det_10g.onnx`, 640×640 input) is expected to dominate recognition (`w600k_r50.onnx`, 112×112 input)
   by roughly (640/112)² ≈ 32× on the input side alone.
2. `Peak Native Increase` > 0 on **every single call** (not intermittently) for the same model is the
   signal to watch — see §10.
3. The final report's `Largest ONNX Allocation` field names whichever single inference call (by model)
   had the largest `max(tensor bytes, native increase)` for the whole job.

## 14. How to identify matching inflation

1. `AI18 MATCHING MEMORY AUDIT` reports `Working Set Before`/`After` bracketing the single batched
   `IFaceMatcher.Match(...)` call. Given the matcher is O(faces × candidates) with a temporary
   `FaceMatchResultDto` per face (unchanged, per AI17.RUNTIME.6's findings), Working Set growth here
   should scale with `detectedFaceCount`, not with `candidateStudentCount` alone.
2. If `Largest Matching Allocation` in the final report is unexpectedly large relative to
   `detectedFaceCount`, that is the signal to re-open AI17.RUNTIME.6's matching audit for a specific
   class roster size, not evidence of a new problem introduced by this milestone (this audit only reads
   Working Set before/after — it cannot itself distinguish a large-class-roster effect from a genuine
   inefficiency).

## 15. How to identify thumbnail inflation

1. Every `Snapshot("Before/After Thumbnail Persistence", N)` pair reports the per-face Working Set delta
   for one `RecognitionMediaService.PersistFaceThumbnailAsync` call (AI18.REVIEW.2). The audit
   automatically classifies any stage whose name contains "Thumbnail" into the `ThumbnailAllocation`
   bucket (`RecognitionMemoryAudit.Snapshot`/`ClassifyRegisteredObject`).
2. The final report's `Largest Thumbnail Allocation` field names the single largest per-face thumbnail
   upload delta for the whole job. Given AI18.REVIEW.3's measured ~1 ms per-thumbnail local-disk write
   latency and small WebP payload sizes (see that report's Task 10), this bucket is expected to be small
   relative to ImageSharp/ONNX — if it is not, that points at the storage provider (S3 network latency
   under load, not local disk) rather than the recognition pipeline itself.

---

## 16. Investigation checklist (run this once a production log exists)

1. [ ] Locate the `AI18 MEMORY AUDIT — BEGIN` line for the target `ExecutionTraceId` and every
       `AI18 MEMORY SNAPSHOT` block that shares it, in order.
2. [ ] Read the `MEMORY FORENSICS REPORT` block at the end of that job — this alone answers 12 of the 15
       success-criteria questions directly (Peak Working Set/Private/Managed/Native, every "Largest ___"
       field).
3. [ ] Cross-reference `Largest Stage Increase` against §5's stage list to name the exact pipeline stage
       responsible for the single biggest jump.
4. [ ] Read the `AI18 TOP 20 MEMORY CONSUMERS` table to identify the single largest object/collection.
5. [ ] Check every `AI18 ENTITY FRAMEWORK AUDIT` block's `Student Photos Loaded`/
       `Navigation Collections Loaded` fields — both must be `False` (§11); if either is `True`, EF
       loaded unnecessary objects.
6. [ ] Check every `AI18 ONNX RUNTIME AUDIT` block's `Outputs Disposed` field — must be `True` on every
       line; any `False` triggers an automatic `UNDISPOSED ONNX OUTPUT` warning.
7. [ ] Check for `AI18 OBJECT STILL ALIVE AT COMPLETION` warnings — every `MemoryStream`/`Face Crop`/
       `ImageSharp Image` registration must have a matching disposal by job completion.
8. [ ] Note the process Working Set (from the *last* snapshot before whichever checkpoint corresponds to
       "45%" in the production symptom description) against the Render Starter's 512 MB ceiling — this
       is the stage that "exceeded 512 MB".
9. [ ] Compare that stage's `Native Estimate` vs `Managed Heap` delta to classify the increase as managed
       or native (§10).
10. [ ] Compare `GC Heap Fragmentation` at that stage against earlier stages to assess fragmentation
        (§9).
11. [ ] Answer success-criteria question 15 ("what single optimization would recover the largest amount
        of memory?") using the `Largest ___` field with the single biggest value across the whole final
        report — that is, by construction, the single largest identified allocation source for that job.

---

## 17. Recommendations (documentation only — **not implemented in this milestone**)

These are observations from the instrumentation added here and the prior AI16/AI17 milestones, offered
strictly as candidates for a *future*, separately-scoped optimization milestone (e.g. AI19). **Nothing
below has been implemented as part of AI18.MEMORY.1.**

1. If production logs confirm `image.Clone()` inside `BuildDetectionInput` (§12) is the dominant
   per-job allocation, a future milestone could evaluate resizing directly during decode instead of
   clone-then-resize — but only after this audit's real numbers confirm it is worth the risk, per
   AI17.RUNTIME.7's existing "Top three optimization opportunities" ranking.
2. If `AI18 OBJECT STILL ALIVE AT COMPLETION` consistently flags the same object type across many
   production jobs, that specific object's disposal path should be reviewed for a genuine bug (as
   opposed to an expected long-lived managed collection, per §4).
3. If `GC Heap Fragmentation` is confirmed to grow across the job (§9), a future milestone could evaluate
   whether `GCSettings.LargeObjectHeapCompactionMode` or `DOTNET_GCHeapHardLimit` (Render container
   memory ceiling) are configured appropriately — this milestone deliberately does not touch either.
4. If the `Largest Native Allocation`/ONNX section shows the native estimate genuinely never returning to
   baseline between inferences, AI16.RUNTIME.1's already-documented `EnableCpuMemArena=false` evaluation
   should be revisited with the new evidence.

---

## 18. Files added/changed by this milestone

**Added:**
* `Abhyanvaya.Infrastructure/Diagnostics/MemoryAudit/MemoryAuditSnapshot.cs`
* `Abhyanvaya.Infrastructure/Diagnostics/MemoryAudit/IRecognitionMemoryAudit.cs`
* `Abhyanvaya.Infrastructure/Diagnostics/MemoryAudit/RecognitionMemoryAudit.cs`
* `docs/AI18_MEMORY1_COMPLETE_FORENSICS.md` (this document)

**Changed (diagnostics wiring only — no behavior changes):**
* `Abhyanvaya.Infrastructure/DependencyInjection.cs` — registers `IRecognitionMemoryAudit` Scoped.
* `Abhyanvaya.Infrastructure/Recognition/ClassroomRecognitionPipeline.cs` — `Begin()`/`Snapshot()`/
  `RecordEntityFrameworkQuery()`/`RecordStudentEmbeddingLoad()`/`RecordMatchingMemory()`/
  `RecordDatabaseSave()`/`RegisterObject()`/`DisposeObject()`/`Complete()` calls added alongside every
  existing AI17 `_forensics.*` call.
* `Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs` — `Snapshot()`/`RegisterObject()`/
  `DisposeObject()`/`RecordOnnxInference()` calls added alongside every existing AI17 `_forensics.*`
  call.

**Verification:** `dotnet build` on `Abhyanvaya.Infrastructure` and the full `Abhyanvaya.API` solution
both succeed with **0 errors** (warnings are pre-existing and unrelated to this change — see the
project's existing `NU1902`/`NU1903`/`CS8618` warnings, none of which originate from the files this
milestone touched). No recognition, matching, threshold, ImageSharp, ONNX, database, DI, threading,
queue, GC, Render, or Docker behavior was changed.
