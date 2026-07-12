# AI16.RUNTIME.2 — ImageSharp Memory Optimization

**Status: IMPLEMENTED (buffer pre-sizing / duplicate-buffer removal only — no visual/output change)**
**Date:** 2026-07-12
**Reviewer:** Chief Software Architect
**Scope:** Every `Image.Load`/`Clone`/`Crop`/`Resize`/`Mutate`/`SaveAsWebp`/`MemoryStream` call site in
the recognition path, plus the two storage-read call sites that feed image bytes into it.

---

## 1. Objective

Reduce ImageSharp/`MemoryStream` allocations feeding the recognition pipeline, without changing image
quality, crop coordinates, or the resize algorithm anywhere.

## 2. Full inventory of the reviewed call sites

| # | File | Call | Finding | Action |
|---|---|---|---|---|
| 1 | `InsightFaceEngine.DetectAsync` | `Image.Load<Rgb24>(request.ImageBytes)` | Decodes the full classroom photo once per job; already inside a `using var` (disposed at the end of `DetectAsync`). Single decode — no double-decoding found. | None — already minimal. |
| 2 | `InsightFaceImageMath.BuildDetectionInput` | `image.Clone()` then `.Mutate(ctx => ctx.Resize(...))` | Clones the source image *before* resizing it, because the caller (`InsightFaceEngine.DetectAsync`) still needs the **original, unresized** `image` afterwards for per-face cropping/alignment (`AlignFace` samples from the original-resolution `image`, not the resized detection copy). This clone is therefore load-bearing, not redundant — removing it would corrupt every subsequent `AlignFace` call in the same job. `Image.Clone(ctx => ctx.Resize(...))` (single fluent call) is functionally and memory-wise identical to `Clone()` + `Mutate(Resize(...))` — no extra buffer either way, so there was nothing to consolidate here. | None — already minimal; confirmed no duplicate buffer beyond what correctness requires. |
| 3 | `InsightFaceImageMath.AlignFace` | `new Image<Rgb24>(outputSize, outputSize)` + per-pixel bilinear sampling from `source` | Allocates exactly one new image (112×112 by default) per detected face — this *is* the aligned-face output, not a temporary; it is returned to the caller and lives until the caller's `using var aligned` goes out of scope. No intermediate/duplicate image is created inside this method. | None. |
| 4 | `InsightFaceEngine.DetectAsync` (per-face loop) | `await using (var ms = new MemoryStream()) { await aligned.SaveAsWebpAsync(ms, ...); alignedBytes = ms.ToArray(); }` | The parameterless `MemoryStream()` constructor starts at 0 bytes and **doubles its internal buffer on each growth**, copying the old buffer into the new one every time — for a WebP-encoded 112×112 crop (typically a few KB), this is 2–4 needless copy-and-discard cycles before the buffer stabilizes. `ToArray()` afterwards makes one more full-size copy (unavoidable — it's the only way to get an exactly-sized `byte[]` out of a `MemoryStream`, and this array is what gets stored in the `DetectedFaceDto`, so it must exist). | **Changed** — `new MemoryStream(8192)` (see §3). |
| 5 | `Abhyanvaya.API/Media/MediaObjectReader.cs` (`ReadStudentFaceAsync` / `ReadObjectAsync`) | `using var ms = new MemoryStream(); await stream.CopyToAsync(ms, ...); return ms.ToArray();` | Both current storage providers (`LocalStorageProvider` → `FileStream`, `S3StorageProvider` → an already-fully-buffered `MemoryStream`, see #6) return a **seekable** stream with a known `Length`. The old code still routed every read through a second growable `MemoryStream` (more doubling-copy cycles) and then a third full-size `ToArray()` copy — three buffers of the same bytes for what only needs one. | **Changed** — direct `stream.Length`-sized read (see §3). |
| 6 | `Abhyanvaya.API/Media/S3StorageProvider.cs` (`ReadObjectAsync`) | `var buffer = new MemoryStream(); await response.ResponseStream.CopyToAsync(buffer, ...);` | S3/MinIO's `GetObjectResponse.ContentLength` is known before the body is read (it comes from the HTTP response headers) — the destination `MemoryStream` was still starting at 0 bytes and growing/copying repeatedly while buffering a classroom photo that can be several MB. | **Changed** — pre-sized `MemoryStream` (see §3). |
| 7 | *(searched, not found)* | Any `.Crop(...)` call | `InsightFaceImageMath` implements its own per-pixel crop-equivalent logic inside `AlignFace`'s sampling loop (reading directly from `source` at transformed coordinates) rather than calling ImageSharp's `Crop()` extension — there is no `Image.Crop()` call anywhere in the recognition path to review. | N/A — nothing to change; noted for completeness against the task's review list. |
| 8 | *(searched, not found)* | Any second `SaveAsWebpAsync`/other encoder call for the same image | Each aligned face is WebP-encoded exactly once (item #4). The full classroom photo itself is never re-encoded — it is only decoded (`Image.Load`) and read pixel-by-pixel; no "double encoding" pattern was found anywhere in the pipeline. | N/A. |

## 3. Changes made

### 3.1 `InsightFaceEngine.DetectAsync` — pre-sized per-face WebP buffer

```csharp
// Before
using (var ms = new MemoryStream())
// After
await using (var ms = new MemoryStream(8192))
```

8 KB comfortably covers a WebP-encoded 112×112 aligned-face crop in the overwhelming majority of cases
(WebP at typical quality settings for a small, mostly-uniform face crop runs a few KB), collapsing the
2–4 double-and-copy growth cycles the default 0-byte start would otherwise trigger. If a given crop
ever exceeds 8 KB, `MemoryStream` still grows exactly as before — this is a *starting* capacity hint,
not a hard cap, so there is no risk of truncation or a behavior change for larger-than-typical outputs.
**The bytes written to the stream, and therefore `alignedBytes`, are identical to before.**

### 3.2 `MediaObjectReader` — read directly into one exactly-sized array

```csharp
private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
{
    if (stream.CanSeek)
    {
        var length = checked((int)stream.Length);
        var buffer = new byte[length];
        var totalRead = 0;
        while (totalRead < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead), cancellationToken);
            if (read == 0) break; // defensive only
            totalRead += read;
        }
        return buffer;
    }

    // Fallback, unchanged, for any future non-seekable provider stream:
    using var ms = new MemoryStream();
    await stream.CopyToAsync(ms, cancellationToken);
    return ms.ToArray();
}
```

Both existing call sites (`ReadStudentFaceAsync`, `ReadObjectAsync`) now go through this helper. For the
two providers that exist today, this eliminates the intermediate `MemoryStream` entirely — one
`byte[]` allocation of exactly the right size instead of a growable buffer *plus* a `ToArray()` copy of
it. The non-seekable fallback path is preserved byte-for-byte unchanged for forward compatibility with
any future streaming provider. **Returned bytes are identical either way.**

### 3.3 `S3StorageProvider.ReadObjectAsync` — pre-sized destination buffer

```csharp
var buffer = response.ContentLength > 0
    ? new MemoryStream(checked((int)response.ContentLength))
    : new MemoryStream();
await response.ResponseStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
buffer.Position = 0;
return buffer;
```

Uses the S3 response's own `ContentLength` header (already available before any body bytes are read)
to size the destination buffer up front, removing the doubling-and-copying growth cycles that would
otherwise occur while buffering a multi-MB classroom photo. Falls back to the original parameterless
constructor if `ContentLength` is ever `<= 0` (unset/unknown) — never a behavior regression, only a
missed optimization in that edge case. **Buffered bytes are identical either way.**

## 4. `using` / dispose-timing / buffer-lifetime review

- `Image.Load<Rgb24>(...)` (source image) — `using var`, scoped to the whole of `DetectAsync`; disposed
  exactly once, at method exit, after the last per-face loop iteration has finished reading from it.
  Not held any longer than the last consumer (`AlignFace`) needs it.
- `AlignFace`'s returned `aligned` image — `using var aligned`, scoped to a **single loop iteration**;
  disposed before the next face in the same photo is processed (confirmed via the
  `_diagnostics.ObjectDisposed("ImageSharp Image", $"aligned face {currentFace}")` call immediately
  after the loop body, from AI15.DIAGNOSTICS.1). No aligned face image survives longer than the
  iteration that produced it.
- The per-face WebP `MemoryStream` — `await using`, scoped to the four lines that encode it and call
  `ToArray()`; disposed before the loop moves to the next face.
- `working` inside `BuildDetectionInput` — `using var working = image.Clone()`; disposed at the end of
  that method, immediately after the tensor-fill loop finishes reading from it. Not retained past the
  method that created it.

No image or stream in this pipeline is retained past the scope that produced it, and none is
disposed early relative to its last read. **No change was needed to any `using`/dispose timing** —
the existing structure was already correct; this milestone's changes are purely about *how large* the
initial buffer allocation is, not *when* it is freed.

## 5. Pooled-buffer retention

ImageSharp's own internal pixel-buffer pool (`Configuration.Default.MemoryAllocator`) is managed
entirely inside the library and was not modified by this milestone — no `Configuration` override was
introduced. Nothing in the reviewed code path holds a `MemoryStream`/`Image` open longer than its
`using`/`await using` scope (see §4), so there is no application-level cause for ImageSharp to retain
pooled buffers longer than necessary; any pool retention beyond that is internal to ImageSharp's own
allocator and out of scope for an application-code change.

## 6. Requirements verified

- ✅ Image quality unchanged — no encoder quality parameter, resize algorithm, or pixel math was
  touched; only buffer *capacities* changed.
- ✅ Crop coordinates unchanged — `AlignFace`'s similarity-transform math and `BuildDetectionInput`'s
  scale/pad calculation are byte-for-byte unmodified.
- ✅ Resize algorithm unchanged — `ctx.Resize(resizedWidth, resizedHeight)` call is unmodified (still
  ImageSharp's default resampler).
- ✅ `dotnet build` — `Abhyanvaya.Infrastructure` and the `Abhyanvaya.API` media classes compile with
  0 errors (see the AI16.RUNTIME cover note for a pre-existing, unrelated local file-lock caveat on a
  full solution rebuild while Visual Studio has the API assembly loaded).
