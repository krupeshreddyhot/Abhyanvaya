# AI16.RUNTIME.3 — Recognition Buffer Reuse

**Status: IMPLEMENTED (allocator change only — no caching, no AI behavior change)**
**Date:** 2026-07-12
**Reviewer:** Chief Software Architect
**Scope:** `InsightFaceEngine.ExtractEmbedding`, `InsightFaceImageMath.BuildRecognitionInput`,
`InsightFaceEngine.DetectFaces`/`BuildDetectionInput`

---

## 1. Objective

Reduce temporary allocations during embedding generation — specifically the per-face
`DenseTensor<float>`/backing `float[]` and `NamedOnnxValue` — without caching any embedding or
recognition result, and without changing recognition output.

## 2. Per-face allocation review — before this milestone

`ExtractEmbedding(Image<Rgb24> alignedFace)` ran once per detected face, sequentially, within one
classroom photo (`InsightFaceEngine.DetectAsync`'s `foreach (var candidate in selectedCandidates)`
loop — never parallel, never re-entrant for the same job):

```csharp
var inputTensor = InsightFaceImageMath.BuildRecognitionInput(alignedFace); // new float[3*size*size] every call
var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
using var outputs = session.Run(inputs);
var embedding = outputs.First().AsEnumerable<float>().ToArray();
```

For the default `RecognitionInputSize = 112`, `BuildRecognitionInput` allocated a fresh
`3 * 112 * 112 = 37,632`-element `float[]` (147 KB) on **every single face**, in a photo that can
contain dozens of students. Each of these arrays is short-lived (used only until `session.Run(...)`
returns) but, because they're the same fixed size on every call, they are a textbook case for
`ArrayPool<T>` reuse rather than repeated fresh heap allocation + GC.

## 3. Can the input tensor be reused safely? — yes, with one condition

`BuildRecognitionInput` (in `InsightFaceImageMath`) unconditionally writes **every element** of the
backing buffer:

```csharp
alignedFace.ProcessPixelRows(accessor =>
{
    for (var y = 0; y < accessor.Height; y++)
    {
        var row = accessor.GetRowSpan(y);
        for (var x = 0; x < row.Length; x++)
        {
            var pixel = row[x];
            tensor[0, 0, y, x] = ...;
            tensor[0, 1, y, x] = ...;
            tensor[0, 2, y, x] = ...;
        }
    }
});
```

Every `(channel, y, x)` cell in the `[1, 3, size, size]` tensor is assigned from the source pixel — for
a square `alignedFace` (guaranteed, since `AlignFace` always produces `outputSize × outputSize`), there
is no padding region, no untouched cell, and no code path that reads a tensor element before writing
it. **This is the condition that makes pooling safe**: a rented array from `ArrayPool<float>.Shared`
may contain stale data from a previous, unrelated rental, but since every element is overwritten
before the tensor is handed to `session.Run(...)`, that stale data can never reach the model. Output is
therefore identical to the unpooled path — confirmed by inspection, not by change of the fill logic
itself (which is untouched).

This condition is explicitly **not** true for `BuildDetectionInput` (used by `DetectFaces`, the other
tensor-building method reviewed): it deliberately leaves the letterbox padding region at whatever the
tensor's initial value is (0 for a fresh `float[]`, arbitrary for a pooled one) when
`srcX < 0 || srcY < 0 || srcX >= resizedWidth || srcY >= resizedHeight`. Pooling that buffer *without*
also zeroing the padding region first would leak stale values from a previous rental into the model
input for the padded border pixels — a real behavior change, not merely a performance one. **This
method was therefore deliberately left unpooled** — see §5.

## 4. The change: pool the recognition input buffer

```csharp
// InsightFaceEngine.ExtractEmbedding
var size = alignedFace.Width;
var length = 3 * size * size;
var rented = ArrayPool<float>.Shared.Rent(length);
try
{
    var inputTensor = InsightFaceImageMath.BuildRecognitionInput(alignedFace, rented.AsMemory(0, length));
    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
    using var outputs = session.Run(inputs);
    var embedding = outputs.First().AsEnumerable<float>().ToArray();
    return InsightFaceImageMath.L2Normalize(embedding);
}
finally
{
    ArrayPool<float>.Shared.Return(rented);
}
```

`InsightFaceImageMath.BuildRecognitionInput` gained an overload taking a caller-supplied
`Memory<float> buffer` (the original parameterless-array overload is kept, delegating to the new one,
for the one other unaffected caller `GenerateSingleFaceEmbedding` never calls it directly — it's
unaffected because `ExtractEmbedding` is the single call site the pooled overload was wired into). The
fill logic in the shared inner method is **completely unchanged** — only *where the backing array comes
from* changed, not what gets written into it or in what order.

`ArrayPool<float>.Shared.Return(rented)` is inside a `finally`, so the rented array is returned to the
pool even if `session.Run(...)` throws — no growth in outstanding rentals under repeated failures, and
no risk of a "returned twice" double-return bug since the `try`/`finally` guarantees exactly one
`Rent`/one `Return` per call.

**No embedding is cached. No recognition result is cached.** Only the *scratch input buffer* — which
never leaves this method and is fully overwritten before use — is pooled. The returned `float[]`
embedding (the actual recognition output) is a fresh, non-pooled array from
`outputs.First().AsEnumerable<float>().ToArray()`, exactly as before; nothing about the embedding's own
lifetime, ownership, or caching changed.

## 5. `BuildDetectionInput` — reviewed, not pooled, and why

`DetectFaces` calls `BuildDetectionInput` exactly **once per photo** (not once per face — detection runs
once against the whole image before any faces are cropped), so the allocation-frequency case for
pooling is far weaker there than for the per-face recognition tensor. More importantly, as shown in
§3, its padding region is intentionally left at the buffer's initial value rather than explicitly
written — pooling would require adding an explicit zero-fill of the padding cells first to preserve
correctness, which is itself an extra pass over the buffer that would partially offset the allocation
savings for a call that only happens once per photo anyway. Given the low call frequency and the
correctness risk of getting the padding fill wrong, this was left as a plain `new DenseTensor<float>(...)`
allocation — a deliberate, documented "not worth it" rather than an oversight.

## 6. `NamedOnnxValue` / output tensors

`NamedOnnxValue.CreateFromTensor(...)` wraps an existing tensor by reference — it does not itself copy
or allocate the underlying data, so there was no separate buffer to pool there; the `List<NamedOnnxValue>`
holding it is a small, single-element list allocated once per `Run()` call (unavoidable — that's the
API shape `InferenceSession.Run` requires). Output tensors (`DisposableNamedOnnxValue` collection from
`session.Run(...)`) are owned and disposed by ONNX Runtime itself via the existing `using var outputs`
— they are not application-managed buffers and are out of scope for `ArrayPool` reuse (ORT allocates
them internally per its own `SessionOptions.EnableCpuMemArena`/`EnableMemoryPattern` configuration,
addressed in AI16.RUNTIME.1).

## 7. Requirements verified

- ✅ No embedding caching — the returned `float[]` per face is still computed fresh from that face's
  pixels on every call; nothing persists it across calls.
- ✅ No recognition-result caching — `ClassroomRecognitionPipeline`'s matching logic is untouched by
  this milestone.
- ✅ No AI behavior change — the fill logic, tensor shape, normalization, and ONNX `Run()` call are
  byte-for-byte unchanged; only the backing array's *source* (pool vs. fresh heap allocation) changed.
- ✅ `dotnet build` — `Abhyanvaya.Infrastructure` builds with 0 errors.
