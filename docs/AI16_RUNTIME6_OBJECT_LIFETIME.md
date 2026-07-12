# AI16.RUNTIME.6 — Recognition Object Lifetime Review

**Status: REVIEWED (no additional code changes — obvious improvements already applied in AI16.RUNTIME.2/.3)**
**Date:** 2026-07-12
**Reviewer:** Chief Software Architect
**Scope:** Every heavy object created during one classroom photo's detection→alignment→embedding flow.

---

## 1. Objective

Trace the complete lifetime (creation → last use → dispose → GC eligibility) of every heavy object in
the recognition path, and flag anything surviving longer than necessary. This is a review-only
deliverable — code changes are only made where an obvious improvement exists, and any such change made
here was already applied and documented under AI16.RUNTIME.2 (ImageSharp) / AI16.RUNTIME.3 (buffer
reuse) rather than duplicated in this pass.

## 2. Scope note: per-photo vs. per-process objects

Two very different lifetime classes are in play, and mixing them up is the easiest way to misjudge
this review — they are separated explicitly below:

- **Per-process, long-lived by design**: `InferenceSession` (×2). Created once, lazily, on first use,
  and held for the lifetime of the singleton `InsightFaceOnnxModelHost`.
- **Per-photo / per-face, intentionally short-lived**: everything else in this document.

## 3. Object lifetime table

| Object | Created | Last used | Disposed | GC-eligible | Notes |
|---|---|---|---|---|---|
| `InferenceSession` (detection) | Once, lazily, on the first call to `GetDetectionSession()` (`InsightFaceOnnxModelHost.EnsureLoaded`, under `lock (_gate)`) | Every `session.Run(...)` call in `DetectFaces`, for the life of the process | `InsightFaceOnnxModelHost.Dispose()` — only called when the DI container itself is disposed (host shutdown), since `InsightFaceOnnxModelHost` is registered with the default (effectively Singleton-for-the-app-lifetime) lifetime | Only after `Dispose()` at shutdown | **By design, not a leak** — re-loading a 10s-of-MB ONNX model file and re-initializing ORT's native session on every job would be far more expensive than keeping one resident session; this is the correct tradeoff for a service that processes many jobs. |
| `InferenceSession` (recognition) | Same pattern, via `GetRecognitionSession()` | Every `session.Run(...)` in `ExtractEmbedding` | Same — process shutdown | Same | Same. |
| `DenseTensor<float>` (detection input) | Once per photo, in `BuildDetectionInput` (wraps a fresh `new DenseTensor<float>(new[] { 1, 3, inputSize, inputSize })`, backed by a plain `float[]`) | Read by `session.Run(inputs)` in `DetectFaces`, once | Not `IDisposable` — no explicit dispose; becomes unreachable the moment `DetectFaces` returns (nothing retains a reference to `inputTensor`/`inputs` beyond that method's local scope) | Immediately after `DetectFaces` returns — eligible for the next Gen0 collection | Reviewed for pooling under AI16.RUNTIME.3 and deliberately **not** pooled — its padding-region fill pattern makes a pooled (potentially stale) buffer unsafe without an extra explicit zero-fill pass; see `docs/AI16_RUNTIME3_BUFFER_REUSE.md` §5 for the full reasoning. Not a lifetime problem — it never survives past the method that created it either way. |
| `DenseTensor<float>` (recognition input) | Once **per detected face**, in `ExtractEmbedding`, now backed by an `ArrayPool<float>.Shared.Rent(...)`-ed array instead of a fresh `float[]` (AI16.RUNTIME.3) | Read by `session.Run(inputs)`, once | The tensor object itself is not `IDisposable`; its *backing array* is explicitly returned via `ArrayPool<float>.Shared.Return(rented)` in a `finally` block | The tensor wrapper is GC-eligible immediately after `ExtractEmbedding` returns; the backing array is returned to the pool (not GC'd — it becomes available for the *next* face's rental instead) | **Improved in AI16.RUNTIME.3** — this used to be a fresh 147 KB (at default 112×112) heap allocation per face; it is now a pool rental, reducing Gen0 churn proportional to face count. See that document for the full safety analysis. |
| `NamedOnnxValue` (×2 per call: detection input, recognition input) | Once per `session.Run(...)` call, via `NamedOnnxValue.CreateFromTensor(...)` — wraps the tensor above by reference, no data copy | Consumed by `session.Run(inputs)` immediately | Not independently disposed — it is a thin wrapper; becomes unreachable together with the `List<NamedOnnxValue>` holding it, right after `Run(...)` returns | Immediately after the `Run(...)` call that consumed it | No improvement opportunity — already the minimum possible: one small wrapper object per call, holding no data of its own. |
| `DisposableNamedOnnxValue` collection (outputs, ×2: detection, recognition) | Returned by `session.Run(inputs)` — owned by ONNX Runtime, allocated per its own `SessionOptions.EnableCpuMemArena`/`EnableMemoryPattern` configuration (AI16.RUNTIME.1) | `outputs.First().AsEnumerable<float>().ToArray()` (recognition) / iterated by `ParseDetectionOutputs` (detection) — read once, immediately after `Run()` returns | `using var outputs` — disposed at the end of the `using` block, i.e. immediately after the one read above, still within the same method (`DetectFaces`/`ExtractEmbedding`) | Right after the `using` block's dispose call, which releases the native buffers ORT allocated for them | Already minimal — `using var` ensures this native-backed collection is never held past its one read. |
| ImageSharp `Image<Rgb24>` (source classroom photo) | Once per photo, `Image.Load<Rgb24>(request.ImageBytes)` in `DetectAsync` | Read by `DetectFaces` (once) and by `AlignFace` (once per face, for every face in the photo) | `using var image` — disposed when `DetectAsync` returns, i.e. after the *last* face in the photo has been aligned | Right after `DetectAsync` returns | Held for the correct duration — it must stay alive for every `AlignFace` call in the loop, since alignment samples directly from the original-resolution source image, not a cached copy. Not held any longer than that. |
| ImageSharp `Image<Rgb24>` (`working`, resized clone inside `BuildDetectionInput`) | Once per photo, `image.Clone()` then `Mutate(Resize(...))` | Read by the tensor-fill loop immediately below it, within the same method | `using var working` — disposed at the end of `BuildDetectionInput` | Right after `BuildDetectionInput` returns | Necessarily a clone (see AI16.RUNTIME.2 §2 row 2 — the original `image` must remain unresized for later face cropping); already disposed as early as possible. |
| ImageSharp `Image<Rgb24>` (aligned face) | Once per detected face, `new Image<Rgb24>(outputSize, outputSize)` inside `AlignFace` | Read by `ExtractEmbedding` (embedding extraction) and by `SaveAsWebpAsync` (thumbnail encoding) — both within the same loop iteration | `using var aligned` — disposed at the end of that loop iteration, before the next face is processed | Right after each iteration, well before the *next* face's aligned image is even created | Already minimal — one aligned image alive at a time, never accumulated across faces. |
| `MemoryStream` (per-face WebP buffer) | Once per face, inside the same loop iteration, now pre-sized `new MemoryStream(8192)` (AI16.RUNTIME.2) | Written by `SaveAsWebpAsync`, read once by `ToArray()` | `await using (var ms = ...)` — disposed at the end of that 4-line block | Immediately | **Improved in AI16.RUNTIME.2** — pre-sizing avoids 2–4 internal doubling-copy cycles; lifetime itself was already minimal. |
| `MemoryStream` (media read buffers, `MediaObjectReader`/`S3StorageProvider`) | Once per storage read; now either eliminated entirely (`MediaObjectReader`, when the source stream is seekable — reads straight into one exactly-sized `byte[]`) or pre-sized from the response's `ContentLength` (`S3StorageProvider`) (AI16.RUNTIME.2) | Read once by the immediate caller | `using`/`await using`, disposed at the end of the read method | Immediately | **Improved in AI16.RUNTIME.2** — see that document §3.2/§3.3 for the full before/after. |
| Face Embedding (`float[]`, length 512 by default) | Once per face, `outputs.First().AsEnumerable<float>().ToArray()` then `L2Normalize(...)` (which itself allocates one more `float[]` of the same length) inside `ExtractEmbedding` | Returned up through `DetectAsync` into `DetectedFaceDto.Embedding`, then consumed by whatever calls `InsightFaceEngine.DetectAsync` (the recognition pipeline's matching step) | Never explicitly disposed (not `IDisposable` — a plain managed array) | Whenever the last reference to the containing `DetectedFaceDto`/`FaceDetectionResponse` is dropped by the caller — outside this class's control | **Not cached anywhere in this class or in `ArrayPool`** — this is real recognition output, and per the AI16 constraints and AI16.RUNTIME.3 requirements, it is deliberately a fresh, ordinary array every time, never pooled or persisted beyond what the caller itself chooses to do with the DTO. |
| Alignment buffers (the pixel-sampling loop inside `AlignFace`) | No separate buffer — `AlignFace` writes directly into the `aligned` image's own pixel storage via indexer (`aligned[x, y] = SampleBilinear(...)`); there is no intermediate scratch array | N/A | N/A (owned by the `aligned` image's own lifetime, above) | N/A | Nothing to review here beyond the `aligned` image row above — there is no separate "alignment buffer" object in this codebase; the task's naming maps directly onto the aligned-face `Image<Rgb24>` already covered. |
| Detection buffers (the padding/letterbox `float[]` inside `BuildDetectionInput`'s tensor) | Covered by the "detection input" `DenseTensor<float>` row above | — | — | — | Same object; listed separately here only to explicitly confirm the task's "Detection Buffers" item maps onto the detection-input tensor already reviewed, not a distinct allocation. |

## 4. Lifetime diagram — one classroom photo, N detected faces

```
DetectAsync(imageBytes)
 │
 ├─ image = Image.Load<Rgb24>(imageBytes)              ─┐ lives until DetectAsync returns
 │                                                       │ (needed by every AlignFace call below)
 ├─ DetectFaces(image)                                   │
 │    ├─ inputTensor = BuildDetectionInput(image, ...)   │
 │    │     └─ working = image.Clone(); Resize(working)  │  disposed at end of BuildDetectionInput
 │    ├─ inputs = [NamedOnnxValue.CreateFromTensor(...)] │  unreachable right after Run() below
 │    ├─ outputs = session.Run(inputs)   ← native alloc  │  `using` — disposed right after ParseDetectionOutputs
 │    └─ candidates = ParseDetectionOutputs(outputs)     │
 │                                                        │
 ├─ for each face 1..N:                                  │
 │    ├─ aligned = AlignFace(image, landmarks, size)      │  disposed at end of THIS iteration
 │    │                                                    │
 │    ├─ embedding = ExtractEmbedding(aligned)             │
 │    │    ├─ rented = ArrayPool.Rent(length)               │  returned to pool in `finally`, THIS call only
 │    │    ├─ inputTensor = BuildRecognitionInput(aligned, rented)
 │    │    ├─ outputs = session.Run(inputs)  ← native alloc  │  `using` — disposed right after ToArray()
 │    │    └─ return L2Normalize(outputs...ToArray())         │  ← Face Embedding: survives past this method
 │    │                                                        │
 │    ├─ ms = new MemoryStream(8192)                            │  disposed at end of THIS iteration
 │    │    └─ aligned.SaveAsWebpAsync(ms); alignedBytes = ms.ToArray()
 │    │                                                          │
 │    └─ [aligned disposed]  [ms already disposed]                │
 │                                                                  │
 └─ [image disposed]  ← after the LAST face's iteration completes ─┘

(InferenceSession × 2 — created once, outside this diagram entirely, on first use;
 lives for the whole process, disposed only at host shutdown.)
```

## 5. Objects surviving longer than necessary — findings

**None found that were not already addressed.** Every per-photo/per-face object in the table above is
disposed (or, for non-`IDisposable` arrays, becomes unreachable) at the earliest point its own data
dependency allows — none is retained across loop iterations, across jobs, or past the method that
created it. The two changes that *were* made as a result of this and the preceding reviews
(AI16.RUNTIME.2's buffer pre-sizing/duplicate-buffer removal, AI16.RUNTIME.3's `ArrayPool` reuse for
the per-face recognition tensor) reduce **allocation size/frequency**, not **lifetime** — nothing in
this codebase was found holding a heavy object open for longer than its last use required.

The one deliberately long-lived object, `InferenceSession` (×2), is long-lived by design and is the
correct tradeoff for a service processing a steady stream of jobs — flagged here explicitly, per the
task's instruction to "highlight objects surviving longer than necessary," as **reviewed and
confirmed intentional**, not an oversight.

## 6. Requirements verified

- ✅ No code changes made in this pass — all applicable improvements were already implemented and
  documented under AI16.RUNTIME.2/.3.
- ✅ Every object named in the task (`InferenceSession`, `DenseTensor`, `NamedOnnxValue`,
  `DisposableNamedOnnxValue`, ImageSharp `Image`, `MemoryStream`, Face Embedding, Alignment Buffers,
  Detection Buffers) is accounted for in §3.
