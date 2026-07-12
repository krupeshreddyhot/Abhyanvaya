# AI17.RUNTIME.7 — Final Architecture Review

**Scope:** Review of AI17.RUNTIME.1 through AI17.RUNTIME.6. Diagnostics/documentation only — **no
fixes were implemented and no recognition/matching/persistence behavior was changed.**

**Status of underlying data:** AI17.RUNTIME.1–.6 added the *instrumentation* (stage checkpoints,
disposable-object audit, EF load audit, ImageSharp audit, ONNX audit, matching audit) needed to
observe a live classroom-recognition job on the Render Starter instance. This review is written from
static code analysis plus first-principles size calculations of every object the pipeline allocates —
it is **not** yet backed by a captured production log, because no such run has been executed against
this build. Section 15 ("How to close the loop") explains exactly what to capture next and how the
numbers below should be corrected once real logs exist. Every number below that isn't a literal
constant from the code is explicitly marked **[estimate]**.

---

## 1. Complete recognition pipeline timeline

```
ClassroomRecognitionBackgroundService.ExecuteAsync
 └─ DequeueAllAsync yields message
     ├─ [CHECKPOINT] Queue Received                         (AI17.RUNTIME.1)
     └─ ClassroomRecognitionPipeline.ProcessAsync
         ├─ Load AttendanceSession (EF)
         ├─ session.MoveToProcessing / SaveChangesAsync
         ├─ [CHECKPOINT] Image Download Started
         │   └─ IMediaObjectReader.ReadObjectAsync/ReadVariantAsync   (S3 or local disk)
         ├─ [CHECKPOINT] Image Download Finished
         └─ IFaceDetectionService.DetectAsync → InsightFaceEngine.DetectAsync
             ├─ [CHECKPOINT] Image Decode Started
             │   └─ Image.Load<Rgb24>(bytes)                          — source classroom image
             ├─ [CHECKPOINT] Image Decode Finished
             ├─ [CHECKPOINT] Face Detection Started
             │   └─ DetectFaces(image)
             │       ├─ BuildDetectionInput → image.Clone() + Resize(640×640) → DenseTensor<float>
             │       ├─ session.Run(inputs)                            — SCRFD (det_10g.onnx)
             │       └─ ParseDetectionOutputs + ApplyNms
             ├─ [CHECKPOINT] Face Detection Finished
             ├─ [CHECKPOINT] Face Crop Loop Begin
             └─ foreach detected face:
                 ├─ [CHECKPOINT] Before Face Crop (Face N)
                 │   └─ AlignFace → new Image<Rgb24>(112×112)
                 ├─ [CHECKPOINT] After Face Crop (Face N)
                 ├─ [CHECKPOINT] Before Embedding Generation (Face N)
                 │   └─ ExtractEmbedding → BuildRecognitionInput (pooled) → session.Run (w600k_r50.onnx)
                 ├─ [CHECKPOINT] After Embedding Generation (Face N)
                 ├─ [check] Face crop retained? (aligned face still open — expected, see §7)
                 └─ SaveAsWebpAsync(aligned) → alignedBytes; aligned disposed at loop-iteration end
         ├─ [CHECKPOINT] Before Student Embedding Load
         │   └─ LoadStudentEmbeddingsAsync (2 EF queries, AsNoTracking)
         ├─ [CHECKPOINT] After Student Embedding Load
         ├─ [CHECKPOINT] Before Matching
         │   └─ IFaceMatcher.Match (cosine distance, O(faces × candidates))
         ├─ [CHECKPOINT] After Matching
         ├─ Build AttendanceRecognition rows, sync session summary
         ├─ [CHECKPOINT] Before Database Save
         │   └─ SaveChangesAsync (EF)
         ├─ [CHECKPOINT] After Database Save
         └─ [CHECKPOINT] Completed → FinalizeAudit() (long-lived-disposable sweep + summary)
```

Every `[CHECKPOINT]` line above is a real call into `IRecognitionForensicsAudit.Checkpoint(...)`
introduced in AI17.RUNTIME.1 (`ClassroomRecognitionBackgroundService.cs`,
`ClassroomRecognitionPipeline.cs`, `InsightFaceEngine.cs`). One classroom photo with *N* detected
faces produces **16 fixed checkpoints + 4×N per-face checkpoints**.

## 2. Memory graph by stage (estimated shape, pending live capture)

Working Set is monotonically non-decreasing in .NET under normal operation (the CLR/OS rarely returns
freed pages to the OS immediately), so the *shape* of the curve across one job is expected to look
like a staircase that only truly resets between GC generations kicking in or the process being
recycled — not a sawtooth that drops after every stage. Based on object sizes computed in §5:

```
Working Set (relative, one job, illustrative)

  High │                              ┌──────┐
       │                       ┌──────┘      └───┐
       │                ┌──────┘                  └────────┐
       │         ┌──────┘                                   └───────────
       │  ┌──────┘
   Low │──┘
       └──────────────────────────────────────────────────────────────────
         Queue   Image    Image    Face      Face Crop  Embedding  Matching  DB Save
         Recv    DL       Decode   Detection  Loop       Gen                 /Completed
```

The two steepest expected risers are **Image Decode** (allocates the full-resolution `Rgb24` buffer)
and **Face Detection** (additionally clones that same full-resolution buffer inside
`BuildDetectionInput` before resizing it down to 640×640 — see §5 and §11). The face-crop/embedding
loop is expected to be comparatively flat per-face (each face's objects are ~0.04–0.15 MB, see §5) but
could show a slow *cumulative* drift upward across many faces if any per-face object were leaking —
which is exactly what the AI17.RUNTIME.2 disposable audit and AI17.RUNTIME.4 "peak concurrent images"
counter were built to catch.

## 3. Peak native memory

Not yet measured from a live run. Structurally, native (unmanaged) memory in this pipeline comes from
two independent sources, both already tracked as `NativeEstimateBytes` (`PrivateBytes − ManagedHeapBytes`,
AI16.RUNTIME.4) and now sampled immediately before/after every `session.Run(...)` call
(`IRecognitionForensicsAudit.RecordOnnxInference`, AI17.RUNTIME.5):

- **ONNX Runtime**: two `InferenceSession` instances (detection `det_10g.onnx`, recognition
  `w600k_r50.onnx`), each with its own native execution-plan graph, and (per AI16.RUNTIME.1)
  `EnableCpuMemArena=false` / `EnableMemoryPattern=false`, so ORT is *not* expected to retain a growing
  native arena across calls — native memory after inference should return close to native memory
  before inference on every single call. If AI17.RUNTIME.5's `NATIVE MEMORY GROWTH DETECTED` warning
  fires repeatedly across many faces in one job, that is the strongest possible signal that the arena
  settings are not behaving as expected on the deployed ORT build.
- **ImageSharp/libwebp native codecs**: ImageSharp's JPEG/WebP codecs are managed-code implementations
  in this project's dependency graph (no native libjpeg/libwebp P/Invoke), so — unlike a native-codec
  build — ImageSharp itself should **not** be a native-memory contributor here; its cost is almost
  entirely managed-heap (`Rgb24` pixel buffers), which is why §5 attributes it to Managed, not Native.

**Action:** capture one real job's `AI17 ONNX RUNTIME INFERENCE AUDIT` blocks and the `PeakNativeEstimateBytes` field already exposed on `/health` (AI16.RUNTIME.4) to fill this section with real MB figures.

## 4. Peak managed memory

Structurally, the single largest managed allocation in the whole pipeline is the decoded classroom
photo itself (§5). A `RecognitionMemorySnapshot.ManagedHeapBytes` peak should track almost exactly with
"is a classroom photo currently decoded + cloned", i.e. the peak should occur somewhere between the
**Image Decode Finished** and **Face Detection Finished** checkpoints, not during the face-crop/embedding
loop. `/health`'s existing `peakManagedHeapMB`/`peakWorkingSetDeltaMB` fields (AI15/AI16) plus the new
per-checkpoint `Peak Memory So Far` field (AI17.RUNTIME.1) together should confirm this precisely once
captured from a live job.

## 5. Largest allocation source (sizes computed from actual code constants)

| Object | Dimensions / shape (from code) | Size formula | Estimated size **[estimate]** |
|---|---|---|---|
| Source classroom image (`Rgb24`) | Depends on upload — a 12 MP phone photo is commonly ~4000×3000 | `W × H × 3 bytes` | **~34.3 MB** for 4000×3000 |
| Detection-resize clone (`ImageSharp Clone`, `BuildDetectionInput`) | **Same as source** — `image.Clone()` clones at full original resolution *before* `Resize(640,640)` runs | `W × H × 3 bytes` | **~34.3 MB** for 4000×3000 — i.e. a second, momentarily-concurrent copy of the *entire* source image |
| Detection input tensor (`DenseTensor<float>`) | Fixed `1×3×640×640` | `3 × 640 × 640 × 4 bytes` | **4.7 MB** |
| Aligned face crop (`ImageSharp Image`, per face) | Fixed `112×112` | `112 × 112 × 3 bytes` | **0.04 MB** |
| Recognition input tensor (pooled, per face) | Fixed `1×3×112×112` | `3 × 112 × 112 × 4 bytes` | **0.14 MB** (rented from `ArrayPool<float>.Shared`, not a fresh allocation — AI16.RUNTIME.3) |
| Per-face WebP buffer (`MemoryStream`) | ~112×112 crop, WebP-compressed | starts at 8 KB capacity | **≈8–20 KB** |
| Student embedding vectors (all candidates) | Fixed 512 floats each | `studentCount × 512 × 4 bytes` | **~2 KB/student** — e.g. 40 students ≈ **80 KB** total |
| ONNX InferenceSession native graphs (×2, loaded once, cached for process lifetime) | Fixed per model file | N/A (native, one-time) | Not per-job; amortized across every job for the life of the process |

**Conclusion:** by more than three orders of magnitude, **decoding and then cloning the full-resolution
classroom photo** dwarfs every other allocation this pipeline makes per job. Two ~34 MB buffers
existing simultaneously (source image + detection-resize clone) for the few hundred milliseconds
`BuildDetectionInput` runs is the single largest transient allocation in the entire recognition path —
see §11 for why this matters on a 512 MB instance.

## 6. Disposable object audit summary (AI17.RUNTIME.2)

Objects now tracked end-to-end (creation stage → disposal stage → lifetime) via
`IRecognitionForensicsAudit.ObjectCreated/ObjectDisposed`, layered onto every disposable already
tracked by AI15/AI16's `IRecognitionPipelineDiagnostics`:

| Object type | Tracked at | Expected disposal point | Notes |
|---|---|---|---|
| `ImageSharp Image` — "source image" | `InsightFaceEngine.DetectAsync` | End of `DetectAsync` (`using var image`) | Alive for the **entire** detect+crop+embed loop — unavoidable, it's read by every face crop |
| `ImageSharp Clone` — "detection-resize clone" | `InsightFaceImageMath.BuildDetectionInput` | End of that method (`using var working`) | New tracking added in AI17.RUNTIME.2/.4 — was previously invisible to AI15/AI16 diagnostics |
| `ImageSharp Image` — "aligned face N" | Per face, `InsightFaceEngine.DetectAsync` | End of that face's loop iteration | **Intentionally** still open across the "After Embedding Generation" checkpoint — see §7 |
| `MemoryStream` — "face N webp buffer" | Per face | Immediately after `SaveAsWebpAsync` | Short-lived, `await using` |
| `DenseTensor<float>` — "detection input" | `DetectFaces` | Immediately after `session.Run` returns | Not `IDisposable`; tracked as "logically done" once no longer referenced |
| `DenseTensor<float>` — "recognition input (pooled)" | Per face, `ExtractEmbedding` | `finally` block, `ArrayPool<float>.Shared.Return` | Backing array returned to the pool even on exception |
| `NamedOnnxValue` — detection/recognition input wrapper | Per call | Immediately after `session.Run` returns | Not `IDisposable`; same "logically done" convention |
| `DisposableNamedOnnxValue` collection — detection/recognition outputs | Per call | End of `using var outputs` scope | Always disposed under current code — see §8 |

**Bitmap / SKBitmap:** neither type exists anywhere in this codebase (grep-verified) — ImageSharp is
the only imaging library in use. No instrumentation was needed for them; this itself is a useful,
confirmed finding, not a gap.

**"LONG LIVED DISPOSABLE" / "UNDISPOSED ONNX OUTPUT":** under current code, every tracked object above
is disposed before `FinalizeAudit()` runs at job completion, so — assuming the happy path — these
warnings are expected to fire **zero times** per job. They exist specifically to catch regressions or
exception paths that skip a `using`/`finally` block; a live run that ever logs either warning is a
concrete, actionable bug report on its own.

## 7. ImageSharp audit summary (AI17.RUNTIME.4)

- **Maximum concurrent `ImageSharp Image`/`Clone` instances observed, structurally**: **3** —
  source image + detection-resize clone (both alive simultaneously during `BuildDetectionInput`), then
  source image + one aligned face crop (alive simultaneously during each face's crop/embed/encode
  step). Never more than one *aligned face* at a time (the loop is sequential, not parallel) and never
  more than one *source image* at a time (confirmed: only one call site creates one per job) — so the
  "WARNING: Multiple classroom images resident." trip-wire added in AI17.RUNTIME.4 is not expected to
  fire under current code; it exists to catch a future regression (e.g. parallelizing the face loop)
  that would violate this today.
- **"WARNING: Face crop retained." is *expected* to fire on every single face**, not a bug: `aligned`
  is deliberately reused for `SaveAsWebpAsync` immediately after `ExtractEmbedding` returns, so it is
  correctly still open at the "After Embedding Generation" checkpoint by design. AI17.RUNTIME.7 records
  this as a confirmed, benign, and *necessary* finding — flagging it in code comments as a bug would be
  incorrect.
- **Pixel format**: `Rgb24` everywhere (3 bytes/pixel) — never `Rgba32`/`Argb32`, so there is no
  wasted alpha channel byte per pixel already.

## 8. ONNX audit summary (AI17.RUNTIME.5)

- **Inference Session Reused = true** for both detection and recognition, on every single call —
  `InsightFaceOnnxModelHost` lazily creates each `InferenceSession` exactly once (double-checked
  locking) and caches it for the lifetime of the process. Session construction cost/memory is paid
  once per process, not once per job.
- **Tensor Reused**: `false` for detection (fresh `DenseTensor<float>` per `BuildDetectionInput` call —
  AI16.RUNTIME.3 deliberately did not pool this one, since detection runs once per photo, not once per
  face), `true` for recognition (`ArrayPool<float>.Shared`-backed, per AI16.RUNTIME.3, since embedding
  generation runs once per detected face).
- **Disposed Outputs**: always `true` under current code — every `session.Run(...)` call site audited
  here is already wrapped in `using var outputs = session.Run(inputs);`, so disposal is unconditional
  by construction; "UNDISPOSED ONNX OUTPUT" is a regression trip-wire, not an expected finding today.
- **Native memory growth expectation**: with `EnableCpuMemArena=false` / `EnableMemoryPattern=false`
  (AI16.RUNTIME.1), ORT is documented (Microsoft's low-memory CPU inference guidance) to avoid retaining
  a growing native arena across calls — repeated `NATIVE MEMORY GROWTH DETECTED` warnings across many
  faces in one job's real logs would directly contradict that expectation and should be treated as a
  high-priority finding for AI18.

## 9. EF materialization summary (AI17.RUNTIME.3)

From `ClassroomRecognitionPipeline.LoadStudentEmbeddingsAsync` (confirmed by direct code read, not
inferred):

- **Two queries**, both `.AsNoTracking()` — `EF Tracking Enabled = false` for both. No `.Include()`
  anywhere in this method — `Navigation Properties Loaded = "None"`.
- **Lazy loading**: confirmed disabled — `ApplicationDbContext`'s `DbContextOptions` registration was
  grepped for `UseLazyLoadingProxies`; no match found anywhere in the codebase, and EF Core lazy loading
  requires that explicit opt-in, so it cannot be silently on.
- **Materialized objects per job** ≈ `studentCount + 2 × embeddingCount` (student-id list + materialized
  `StudentFaceEmbedding` entities + mapped `StudentEmbeddingMatchInput` DTOs) — for a class of 40
  students with one active embedding each, that's **~120 small objects**, i.e. negligible next to §5's
  image buffers.
- **Total embedding bytes** ≈ `embeddingCount × 512 × 4 bytes` — **~80 KB for 40 students**.
- **Duplicate loads**: `LoadStudentEmbeddingsAsync` is called exactly once per `ProcessAsync` call, and
  `RecognitionForensicsAudit` now increments a per-job counter and would log
  `DUPLICATE LOAD DETECTED` if that ever changed — not expected to fire under current code.

## 10. Candidate matching summary (AI17.RUNTIME.6)

`FaceMatcher.Match` is a plain nested loop: for each detected face, scan every candidate student's
512-float embedding and keep the minimum cosine distance (`FindBestMatch`/`CosineDistance` in
`FaceMatcher.cs`). No new buffer is allocated per comparison — the two arrays being compared already
exist (detected-face embedding, candidate embedding), and the running "best" result is a single
`Nullable<(int,Guid,float)>` value on the stack. **Comparisons performed = detectedFaces × candidates**
(e.g. 20 faces × 40 students = 800 comparisons for one photo) — trivially cheap CPU work, and the only
per-face heap allocation is one `FaceMatchResultDto` (~96 bytes **[estimate]**) added to the results
list. Structurally, matching should be one of the **cheapest** stages in the entire pipeline relative
to image decode/clone (§5) — if a live run's `MATCHING MEMORY SPIKE` (>30 MB) ever fires, it is far more
likely measuring GC noise from the *preceding* stage's Working Set not yet having settled than an
actual cost intrinsic to matching itself.

## 11. Most probable root cause

**Decoding and cloning the full-resolution classroom photo is the dominant memory driver, not ONNX
Runtime, not EF Core, and not the recognition/matching math.** Concretely:

1. `Image.Load<Rgb24>(request.ImageBytes)` decodes the *entire* uploaded photo at its original
   resolution into a managed `Rgb24` buffer (§5: ~34 MB for a 4000×3000 phone photo).
2. `InsightFaceImageMath.BuildDetectionInput` then calls `image.Clone()` — which clones **at that same
   full original resolution**, not at the already-known target 640×640 detection size — and only
   *afterwards* calls `Mutate(ctx => ctx.Resize(...))` on the clone. For the few hundred milliseconds
   this method runs, the process is holding **two ~34 MB buffers simultaneously** for a single 640×640
   detection pass that only ever needed a 640×640 buffer.
3. On a 512 MB Render Starter instance already carrying the ASP.NET Core + EF Core + Npgsql baseline
   (commonly 80–150 MB **[estimate]**, unmeasured here) plus two cached ONNX `InferenceSession`
   native graphs (tens of MB each **[estimate]**), a single classroom photo upload transiently adds
   another ~68–70 MB (source + full-resolution clone) on top — and if the uploaded photo is larger than
   the illustrative 4000×3000 used above (some phones default to 4000×3000 or higher, and no explicit
   maximum upload resolution is enforced anywhere in this pipeline), that transient cost scales
   linearly with megapixels, with no ceiling.

This is consistent with the original symptom ("Render Starter instance still exceeds memory during
classroom recognition") without requiring any change to recognition accuracy, thresholds, or the
matching algorithm — it is purely about *how big a buffer is held, and for how long*, exactly the class
of finding AI17 was scoped to surface without fixing.

## 12. Top three optimization opportunities (NOT implemented — AI18 candidates only)

1. **Resize before cloning at full resolution in `BuildDetectionInput`.** `image.Clone()` followed by
   `Resize` currently clones at full resolution, then shrinks the clone. Cloning is not required to
   produce a 640×640 buffer — a resize can target a new, appropriately-sized buffer directly. This
   would collapse the ~34 MB (or larger) transient clone down to the same ~1.2 MB a 640×640 `Rgb24`
   buffer costs, independent of the uploaded photo's resolution. Highest-impact, most localized change.
2. **Cap/normalize the maximum decoded resolution of an uploaded classroom photo before it reaches the
   recognition pipeline** (e.g. at upload/media-processing time, before `Image.Load<Rgb24>` in
   `InsightFaceEngine.DetectAsync`). Detection only ever needs 640×640 pixels of information; every
   megapixel beyond what's needed to keep faces resolvable is pure memory cost with no accuracy
   benefit. This addresses the *source* of §5's ~34 MB figure, not just the clone in item 1.
3. **Re-validate whether `EnableCpuMemArena`/`EnableMemoryPattern` (AI16.RUNTIME.1) are actually holding
   at their configured `false` on the deployed ORT build**, using the new AI17.RUNTIME.5
   `NATIVE MEMORY GROWTH DETECTED` signal from a live run. If native memory is confirmed flat
   post-inference (as expected), this closes out ONNX Runtime as a contributor entirely and lets AI18
   focus exclusively on items 1–2; if not, it becomes the second target.

## 13. Risk assessment

| Area | Risk if left unaddressed | Risk of the *optimization* itself (AI18) |
|---|---|---|
| Full-resolution clone-then-resize (§11 item 1) | Continues to transiently double the decoded photo's memory footprint on every job; scales with phone camera resolution, which trends upward over time | Low — resizing directly into a target-sized buffer is a well-understood ImageSharp operation; must preserve identical resize algorithm/output pixels (already a hard constraint from AI16) |
| No maximum upload resolution cap (§12 item 2) | Memory cost per job is unbounded and outside this pipeline's control (depends on whatever the uploading device/browser sends) | Medium — must be careful not to change effective detection accuracy; any downscaling must happen at a resolution still comfortably above `DetectionInputSize` (640) so `BuildDetectionInput`'s own scale/pad math is unaffected |
| ORT arena settings unverified in production (§12 item 3) | Diagnostics-only risk — if the assumption is wrong, native memory growth continues undetected until AI17.RUNTIME.5 logs are actually read | None — this item is "go read the logs", not a code change |
| General | AI17 added log volume (one block per checkpoint × faces) | Already mitigated: every new method is gated behind `RecognitionDiagnostics:Enabled` (default `true` today, same switch AI15/AI16 use) and wrapped in try/catch that never rethrows, per the interface's contract |

## 14. Recommended AI18 implementation roadmap

1. **AI18.RUNTIME.1** — Run one real classroom recognition job against this build with
   `RecognitionDiagnostics:Enabled = true` on the actual Render Starter instance (or a size-matched
   local repro) and capture the full log: all AI17.RUNTIME.1–.6 blocks for at least one multi-face
   photo. Use this to replace every **[estimate]** in this document with a measured number and confirm
   or refute §11's root-cause hypothesis before writing a single line of optimization code.
2. **AI18.RUNTIME.2** — Implement §12 item 1 (resize-without-full-resolution-clone in
   `BuildDetectionInput`) behind the same "identical pixel output" constraint AI16 already established,
   and re-run the AI17 checkpoints to confirm the Image Decode → Face Detection Working Set delta drops
   by roughly the clone's measured size.
3. **AI18.RUNTIME.3** — Decide on and implement §12 item 2 (a maximum decode/working resolution for
   uploaded classroom photos), sized against `DetectionInputSize`/typical classroom photo composition,
   with product sign-off since it is the one item in this list that touches upload-time behavior rather
   than purely internal buffer management.
4. **AI18.RUNTIME.4** — Read back the AI17.RUNTIME.5 `NATIVE MEMORY GROWTH DETECTED` signal from the
   AI18.RUNTIME.1 capture; only invest further effort in ONNX Runtime allocator tuning if that signal
   actually fired.
5. **AI18.RUNTIME.5** — Re-run the full AI16 native-memory-optimization validation suite plus one final
   AI17 checkpoint capture after AI18.RUNTIME.2–.4 land, to produce a before/after peak-Working-Set
   comparison for the Render Starter 512 MB budget.

---

## 15. How to close the loop (operational note, not a numbered deliverable item)

To turn every **[estimate]** above into a measured figure:

1. Deploy this branch with `RecognitionDiagnostics:Enabled = true` (already the default).
2. Trigger one classroom photo recognition job with a realistic multi-face photo.
3. Collect the full log stream for that job's `ExecutionTraceId` (every AI17 block below carries it):
   `AI17 STAGE CHECKPOINT` (×16+4N), `AI17 Object Created`/`AI17 Disposable Audit` lines, `AI17 STUDENT
   EMBEDDING LOAD AUDIT` (×1), `AI17 ONNX RUNTIME INFERENCE AUDIT` (×(1+N)), `AI17 CANDIDATE MATCHING
   MEMORY AUDIT` (×1), and the terminal `AI17 FORENSICS AUDIT SUMMARY`.
4. Cross-reference against `/health`'s existing `peakNativeEstimateMB`/`peakWorkingSetDeltaMB`
   (AI16.RUNTIME.4) for that same job's time window.
5. Update this document's §2–§10 with the real numbers before starting any AI18 implementation work.
