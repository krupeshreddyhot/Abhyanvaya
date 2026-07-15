# AI19.MEDIA.3.1 — Provider-Aware Media Retrieval Architecture

**Type:** Architecture and design only. No production code was modified to produce this document.

---

## Task 1 — The Existing Upload Architecture (and why it is correct)

```
RecognitionMediaService.PersistFaceThumbnailAsync   (Abhyanvaya.Infrastructure/Recognition/RecognitionMediaService.cs:59-119)
AttendancePhotoService.UploadClassroomPhotoAsync     (Abhyanvaya.Application/AttendancePhotoService.cs:~130-145)
                    │
                    ▼
IMediaStorageService.SaveOriginalObjectAsync          (Abhyanvaya.Application/Common/Interfaces/IMediaStorageService.cs)
                    │  implemented by
                    ▼
ApplicationMediaStorageService.SaveOriginalObjectAsync (Abhyanvaya.API/Media/ApplicationMediaStorageService.cs:17-38)
                    │
                    ▼
IStorageProviderFactory.GetActiveProvider()            (Abhyanvaya.API/Media/IStorageProviderFactory.cs)
                    │  implemented by
                    ▼
StorageProviderFactory.GetActiveProvider()             (Abhyanvaya.API/Media/StorageProviderFactory.cs:1-25)
                    │  returns one of
        ┌───────────┴────────────┐
        ▼                        ▼
LocalStorageProvider        S3StorageProvider
.WriteObjectAsync           .WriteObjectAsync
(local disk)                (Cloudflare R2 via AmazonS3Client.PutObjectAsync)
```

### Why this is architecturally correct

1. **Dependency Inversion is respected end‑to‑end.** `RecognitionMediaService` (Infrastructure) and `AttendancePhotoService` (Application) depend only on `IMediaStorageService` — an Application-layer abstraction. Neither knows whether bytes end up on local disk or in R2. The concrete choice is made by `IStorageProviderFactory`, which lives in the API layer (the "outermost" layer, per Clean Architecture, where infrastructure concerns like cloud SDKs are permitted to live) and is injected wherever `IMediaStorageService`/`IStorageProvider` is needed via DI.
2. **Single Responsibility is respected at every layer.**
   - `RecognitionMediaService` / `AttendancePhotoService`: know *what* to upload and *what key* to use — nothing about *how* bytes are physically stored.
   - `ApplicationMediaStorageService`: adapts the Application-layer contract (`IMediaStorageService`) onto the API-layer storage abstraction (`IStorageProvider`) — pure plumbing, no business logic.
   - `IStorageProviderFactory`: owns exactly one decision — "which provider is active right now" — driven by `MediaOptions.Provider` (bound from `Media:Provider`/`Branding:Provider`, itself overridable by environment variables via `BrandingSettingsResolver`).
   - `IStorageProvider` (`LocalStorageProvider` / `S3StorageProvider`): each owns exactly one storage technology's byte-level read/write/exists/delete semantics.
3. **The Open/Closed Principle holds for future providers.** Adding Azure Blob or MinIO support means writing one new `IStorageProvider` implementation and one branch in `StorageProviderFactory.GetActiveProvider()` — zero changes to `RecognitionMediaService`, `AttendancePhotoService`, `ApplicationMediaStorageService`, or any DTO/controller that triggers an upload.
4. **Configuration-driven, not code-driven, provider switching.** Moving from `local` to `s3` (R2) in production required zero code changes — only an environment variable flip (`Media:Provider`/`Branding:Provider=s3` plus `S3:*` credentials). This is precisely what AI19.MEDIA.2 observed actually happened.

**This is the reference architecture the retrieval path must now mirror.** AI19.MEDIA.2 established that the retrieval path currently has *none* of these properties — it is a single hard-coded `UseStaticFiles`/`PhysicalFileProvider` registration in `Program.cs:398-403` that never consults `IStorageProviderFactory` at all.

---

## Task 2 — Proposed Retrieval Architecture

```
Browser
  │  GET /media/recognitions/1/{sessionId}/faces/00003.webp
  ▼
MediaController.GetMedia(string key)                    (NEW — Abhyanvaya.API/Controllers/MediaController.cs)
  │
  ▼
IMediaObjectReader.OpenReadAsync(key, ct)                (Abhyanvaya.Application/Common/Interfaces/IMediaObjectReader.cs — EXTENDED, see Task 3)
  │  implemented by
  ▼
MediaObjectReader.OpenReadAsync(key, ct)                 (Abhyanvaya.API/Media/MediaObjectReader.cs — EXTENDED, see Task 3)
  │
  ▼
IStorageProviderFactory.GetActiveProvider()              (UNCHANGED — Abhyanvaya.API/Media/StorageProviderFactory.cs)
  │  returns one of
  ┌────────────┴─────────────┐
  ▼                          ▼
LocalStorageProvider    S3StorageProvider
.ReadObjectAsync         .ReadObjectAsync
(FileStream, local disk) (buffered Stream, Cloudflare R2 via AmazonS3Client.GetObjectAsync)
  │                          │
  └────────────┬─────────────┘
               ▼
       Stream (returned to MediaController)
               │
               ▼
     FileStreamResult (MediaController returns File(stream, contentType))
               │
               ▼
     HTTP 200, Content-Type: image/webp, body streamed to browser
```

This is the **exact mirror image** of the upload architecture in Task 1: same `IStorageProviderFactory`, same two `IStorageProvider` implementations, same configuration-driven provider selection — with `MediaController` playing the role `RecognitionMediaService`/`AttendancePhotoService` play on the write side, and `IMediaObjectReader` playing the role `IMediaStorageService` plays on the write side. **No new abstraction layer is introduced; the existing `IMediaObjectReader` abstraction (already used by the recognition pipeline to read images back for AI processing) is reused and extended, not duplicated.**

### Class diagram

```
┌───────────────────────────┐        ┌────────────────────────────────┐
│ MediaController            │──────▶│ IMediaObjectReader (Application)│
│ (Abhyanvaya.API.Controllers)│       │  + ReadObjectAsync(key)         │
│  GetMedia(key): IActionResult│      │  + ReadVariantAsync(base,variant)│
└───────────────────────────┘        │  + OpenReadAsync(key)  ◀── NEW   │
                                       └───────────────┬────────────────┘
                                                        │ implements
                                       ┌───────────────▼────────────────┐
                                       │ MediaObjectReader (API)         │
                                       │  + OpenReadAsync(key)  ◀── NEW   │
                                       └───────────────┬────────────────┘
                                                        │ uses
                                       ┌───────────────▼────────────────┐
                                       │ IStorageProviderFactory          │
                                       │  + GetActiveProvider()           │
                                       └───────────────┬────────────────┘
                                    ┌───────────────────┴───────────────────┐
                                    ▼                                       ▼
                     ┌───────────────────────────┐          ┌───────────────────────────┐
                     │ LocalStorageProvider        │          │ S3StorageProvider           │
                     │  + ReadObjectAsync(key)      │          │  + ReadObjectAsync(key)      │
                     │    → FileStream               │          │    → MemoryStream (buffered) │
                     └───────────────────────────┘          └───────────────────────────┘
```

### Dependency diagram (Clean Architecture layering, unchanged)

```
┌─────────────────────────────────────────────────────────┐
│ Abhyanvaya.API  (outermost — controllers, storage impls)  │
│  MediaController ──▶ IMediaObjectReader (interface only)   │
│  MediaObjectReader ──▶ IStorageProviderFactory              │
│  StorageProviderFactory ──▶ LocalStorageProvider,            │
│                              S3StorageProvider                │
└──────────────────────────┬──────────────────────────────┘
                           │ implements
┌──────────────────────────▼──────────────────────────────┐
│ Abhyanvaya.Application  (interfaces only, no framework/SDK)│
│  IMediaObjectReader                                          │
└─────────────────────────────────────────────────────────┘
```

`MediaController` (API) depends on `IMediaObjectReader` (Application) — the same dependency direction every other controller already uses for its services. `IMediaObjectReader`'s implementation (API) depends on `IStorageProviderFactory` (API) — both concrete types live in the outer layer, exactly like today. **No layer boundary is crossed in a new direction; the retrieval path adopts the identical dependency shape the upload path already has.**

### Sequence diagram

```
Browser        MediaController        IMediaObjectReader        IStorageProviderFactory        IStorageProvider (Local/S3)
  │  GET /media/recognitions/1/{sid}/faces/00003.webp
  │───────────────▶│
  │                │  key = "recognitions/1/{sid}/faces/00003.webp"  (catch-all route param, Task 2 design)
  │                │  log "Media Request Started" (StorageKey=key)
  │                │───────────────────▶│
  │                │                    │  OpenReadAsync(key, ct)
  │                │                    │───────────────────────────▶│
  │                │                    │                            │ GetActiveProvider()
  │                │                    │                            │───────────────────────────▶│
  │                │                    │                            │◀── LocalStorageProvider or S3StorageProvider
  │                │                    │───────────────────────────▶│  ReadObjectAsync(key, ct)
  │                │                    │                            │───────────────────────────▶│ file/object lookup
  │                │                    │                            │◀── Stream (or throws FileNotFoundException)
  │                │◀───────────────────│◀───────────────────────────│
  │                │  log "Media Request Completed" (Duration, Provider)
  │                │  resolve Content-Type from key extension (FileExtensionContentTypeProvider)
  │                │  return File(stream, contentType)  → FileStreamResult
  │◀───────────────│  HTTP 200, streamed body
```

**Failure branch** (object missing on the active provider):

```
  │                │                    │───────────────────────────▶│  ReadObjectAsync(key, ct)
  │                │                    │                            │───────────────────────────▶│ not found
  │                │                    │                            │◀── throws FileNotFoundException
  │                │                    │◀── (propagates, not caught by MediaObjectReader)
  │                │◀── (propagates, not caught inside try for anything but FileNotFoundException)
  │                │  catch (FileNotFoundException) → log "Media Request Failed" (reason=NotFound) → return NotFound()
  │◀───────────────│  HTTP 404
```

---

## Task 3 — Does `IMediaObjectReader` Already Contain Everything Required?

**No.** Current interface (`Abhyanvaya.Application/Common/Interfaces/IMediaObjectReader.cs`, full file, 15 lines):

```csharp
public interface IMediaObjectReader
{
    Task<byte[]> ReadVariantAsync(
        string storageBasePath,
        string variant = "original",
        CancellationToken cancellationToken = default);

    Task<byte[]> ReadObjectAsync(string relativeKey, CancellationToken cancellationToken = default);
}
```

Both existing methods return `Task<byte[]>` — a **fully materialized byte array**. Its implementation, `MediaObjectReader.ReadObjectAsync` (`Abhyanvaya.API/Media/MediaObjectReader.cs:27-32`), already calls `IStorageProvider.ReadObjectAsync` (which *does* return a `Stream`) and then eagerly copies that stream into a byte array via `ReadAllBytesAsync` (`:43-67`) before returning. This is correct and desirable for its existing callers (`ClassroomRecognitionPipeline`, `InsightFaceEmbeddingGenerator` — both need the complete byte array in memory anyway to hand to ImageSharp/ONNX), but it is the **wrong shape** for HTTP retrieval, where the goal (per AI19.MEDIA.3.2's requirement) is to avoid an extra full-buffer copy at the reader layer and instead let ASP.NET Core stream the response body directly from whatever `Stream` the active `IStorageProvider` already produced.

### Exactly what must be added

1. **`Abhyanvaya.Application/Common/Interfaces/IMediaObjectReader.cs`** — one new method:
   ```csharp
   Task<Stream> OpenReadAsync(string relativeKey, CancellationToken cancellationToken = default);
   ```
2. **`Abhyanvaya.API/Media/MediaObjectReader.cs`** — one new method, a direct pass-through with no extra buffering:
   ```csharp
   public Task<Stream> OpenReadAsync(string relativeKey, CancellationToken cancellationToken = default)
   {
       var provider = _providerFactory.GetActiveProvider();
       return provider.ReadObjectAsync(relativeKey.Trim('/'), cancellationToken);
   }
   ```

**Nothing else needs to change.** `IStorageProvider.ReadObjectAsync` (`Abhyanvaya.API/Media/IStorageProvider.cs`) already returns `Task<Stream>` on both implementations and already throws `FileNotFoundException` uniformly on a missing object:

- `LocalStorageProvider.ReadObjectAsync` (`LocalStorageProvider.cs:55-65`): `throw new FileNotFoundException(...)` when `!File.Exists(fullPath)`.
- `S3StorageProvider.ReadObjectAsync` (`S3StorageProvider.cs:76-106`): `catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) { throw new FileNotFoundException(...); }`.

This existing uniform contract is exactly what `MediaController`'s error-handling requirement (AI19.MEDIA.3.2: "Return 404 only when `FileNotFoundException` is returned") needs, and it requires **zero changes to `IStorageProvider`, `LocalStorageProvider`, or `S3StorageProvider`** — satisfying the "do not modify storage providers" constraint for the implementation milestone.

`ReadVariantAsync` and the existing `ReadObjectAsync` (`byte[]`) are **left completely unchanged** — `ClassroomRecognitionPipeline` and `InsightFaceEmbeddingGenerator` continue to call them exactly as before; this is a strictly additive interface change (no existing call site is touched).

---

## Task 4 — Streaming Strategy for `MediaController`

Three options were evaluated:

| Option | Description | Verdict |
|---|---|---|
| **Buffer entire file, return `FileContentResult`** (`return File(byte[] bytes, contentType)`) | Controller calls `IMediaObjectReader.ReadObjectAsync` (existing `byte[]` method), gets the whole object in memory, returns it | **Rejected.** Adds a full in-memory copy at the controller layer on top of whatever the provider already did internally. For classroom photos (several MB, per AI16/AI17 diagnostics work) this is avoidable memory pressure on a Render Starter (512 MB) instance — precisely the class of problem AI16–AI18 spent multiple milestones investigating and mitigating elsewhere in this same pipeline. Violates the explicit "never load entire files into memory" requirement. |
| **Stream manually via `HttpResponse.Body` with a manual `CopyToAsync` loop** | Controller opens the stream and copies it to `Response.Body` itself, setting headers manually | **Rejected — unnecessary.** This reimplements exactly what `FileStreamResult` already does internally (a buffered async `Stream.CopyToAsync` to the response body), with no behavioral benefit, more custom code to maintain, and it would have to hand-roll `Content-Length`/range-request semantics that `FileStreamResult` already provides for free. |
| **`return File(Stream stream, string contentType)` → `FileStreamResult`** | Controller calls the new `IMediaObjectReader.OpenReadAsync`, gets a `Stream` from the active provider, and returns it directly to ASP.NET Core's built-in `FileStreamResult` | **Recommended.** `FileStreamResult` copies the given `Stream` to the response body asynchronously (default internal buffer, ASP.NET Core's own well-tested implementation), disposes the stream when done, and supports `enableRangeProcessing: true` for `Range`/`If-Range` requests (useful for large classroom photos and consistent with what static-file middleware already provided for free). The controller itself never materializes a `byte[]` of the object — it only ever holds a `Stream` reference. |

**Recommendation:** `MediaController` must call the new `IMediaObjectReader.OpenReadAsync(key, ct)` (Task 3) and return `File(stream, contentType, enableRangeProcessing: true)`.

**Important, evidence-based caveat (not a reason to reject this design — a documented limitation inherited from an existing, unmodified component):** `S3StorageProvider.ReadObjectAsync` (`S3StorageProvider.cs:76-106`) itself already copies the entire R2 response into a `MemoryStream` before returning (`await response.ResponseStream.CopyToAsync(buffer, cancellationToken)`, `:98`) — this is **pre-existing behavior in a file this milestone is explicitly forbidden from modifying** ("do not modify storage providers"). This means true end-to-end zero-buffering streaming from R2 all the way to the browser is **not fully achievable within these constraints** for the `s3` provider — one full in-memory copy of the object still happens inside `S3StorageProvider`, exactly as it does today for the recognition pipeline's own reads. What `MediaController`/`FileStreamResult` *does* eliminate is the **second, redundant** buffering that would otherwise happen if the controller used the `byte[]`-returning methods (`ReadObjectAsync`/`ReadVariantAsync`) — those two use `ReadAllBytesAsync` to copy the already-buffered stream into a brand-new byte array. `OpenReadAsync` + `FileStreamResult` avoids that second copy. For `LocalStorageProvider`, the returned `FileStream` is genuinely lazy/unbuffered end-to-end, so this design achieves true single-pass streaming for local storage and "provider already buffered once, controller does not add a second buffer" for R2 — the best achievable outcome without touching the storage-provider files.

---

## Task 5 — Existing Media URLs Must Remain Unchanged

All URLs are generated by the same three helper functions, none of which this milestone touches:

| Helper | Example output | File |
|---|---|---|
| `AttendanceSessionMediaPaths.BuildMediaUrl` | `/media/recognitions/{tenantId}/{sessionId}/faces/{n:D5}.webp?v={unix}` | `AttendanceSessionMediaPaths.cs:6-14` |
| `AttendanceSessionMediaPaths.BuildMediaUrl` (classroom images, same function) | `/media/attendance/{tenantId}/sessions/{sessionId}/classroom{ext}?v={unix}` | same |
| `StudentMediaPaths.BuildVariantPath` | `/media/students/{tenantId}/{studentId}/{variant}.webp?v={unix}` | `StudentMediaPaths.cs:11-27` |

The proposed `MediaController` route is `GET /media/{**key}` — an ASP.NET Core **catch-all route parameter** (`{**key}`) that matches every segment after `/media/`, including nested slashes, exactly reproducing what `StaticFileOptions.RequestPath = "/media"` (`Program.cs:401`) already matched. The query string (`?v=...`) is untouched by either mechanism — ASP.NET Core routing matches only `Request.Path`, never `Request.QueryString`, identically to how `StaticFileMiddleware` behaves today (confirmed in AI19.MEDIA.2, Task 2). **No URL generator changes, no React changes, and no API contract changes are required or proposed.**

The one thing that must be preserved by the *implementation* (AI19.MEDIA.3.2), not this design document, is the response header behavior currently added by `AddPublicBrandingHeaders` (`Program.cs:366-370`: `Cache-Control: public,max-age=86400` and `Access-Control-Allow-Origin: *`) — the new controller must set the same headers so that browser caching and cross-origin image loading behave identically to today.

---

## Architecture Diagram (Combined — Upload + Retrieval, Post-AI19.MEDIA.3)

```
                       UPLOAD (unchanged)                                    RETRIEVAL (new)
┌───────────────────────┐                                      ┌───────────────────────┐
│ RecognitionMediaService │                                      │ MediaController         │
│ AttendancePhotoService  │                                      │ (NEW)                   │
└──────────┬─────────────┘                                      └──────────┬─────────────┘
           │ IMediaStorageService.SaveOriginalObjectAsync                  │ IMediaObjectReader.OpenReadAsync (NEW)
           ▼                                                               ▼
┌───────────────────────┐                                      ┌───────────────────────┐
│ ApplicationMediaStorage │                                      │ MediaObjectReader        │
│ Service                 │                                      │ (EXTENDED)               │
└──────────┬─────────────┘                                      └──────────┬─────────────┘
           │                                                               │
           └───────────────────────┬───────────────────────────────────────┘
                                    ▼
                     IStorageProviderFactory.GetActiveProvider()
                                    │
                     ┌──────────────┴──────────────┐
                     ▼                              ▼
            LocalStorageProvider              S3StorageProvider
            (unchanged)                       (unchanged)
```

Upload and retrieval now share the identical provider-selection point (`IStorageProviderFactory`), which is the single architectural fix this whole AI19.MEDIA.3 milestone exists to deliver.

---

## Risks

| Risk | Mitigation |
|---|---|
| Route ambiguity/duplicate handling if the existing `UseStaticFiles("/media")` middleware (`Program.cs:398-403`) is left in place alongside the new controller | Not a conflict: `UseStaticFiles` is registered **before** `UseAuthentication`/`UseAuthorization`/`MapControllers()` in the pipeline (`Program.cs:398` vs `:415`). If it finds a file, it serves it and the pipeline short-circuits — the controller is never reached. If it does not find a file, it calls `next()` and the request falls through to `MapControllers()`, where the new route now matches. The two mechanisms are complementary, not conflicting, for as long as both remain registered (see AI19.MEDIA.3.4 for whether the static middleware should eventually be retired). |
| `S3StorageProvider.ReadObjectAsync` fully buffers each object into memory (see Task 4 caveat) | Documented as a known, pre-existing limitation inherited from a file this milestone must not modify. Recognition thumbnails and most student/classroom photos are small enough (KB–low-MB range) that this is consistent with memory behavior the recognition pipeline already exhibits today for the same objects. Not a new regression introduced by this design. |
| Missing/incorrect `Content-Type` for an unusual extension | `FileExtensionContentTypeProvider` (the same provider ASP.NET Core's own `StaticFileMiddleware` uses internally) covers all extensions actually produced by this system (`.webp`, `.jpg`, `.jpeg`, `.png`); an explicit fallback (`application/octet-stream`) covers anything unexpected without failing the request. |
| Public (unauthenticated) access to `/media/*` must be preserved | The new controller must not carry any `[Authorize]` attribute (this app's `AddControllers()`/`AddAuthorization()` registration has no global fallback policy — confirmed by reading `Program.cs:45-131` — so an undecorated action is anonymous by default, matching today's pre-authentication static-file behavior). |
| Directory traversal via the catch-all `key` parameter | Already handled by the unmodified `IStorageProvider` implementations: `LocalStorageProvider.ResolveFullPath` (`:100-113`) rejects any path that escapes its root via `Path.GetRelativePath` + prefix check, and `StorageKeyHelper.IsValidStorageKey` (`StorageKeyHelper.cs:33-52`) rejects `..` segments for both providers. No new validation logic needs to be invented by the controller. |

## Rollback Strategy

Because this design is purely additive (new controller, new interface method, no removal of the existing static-file middleware), rollback is a single-commit revert: removing `MediaController.cs` and the two new `OpenReadAsync` method additions restores today's exact behavior with no residual state, no data migration, and no configuration changes to undo. The existing `UseStaticFiles("/media")` registration is untouched by this design and continues to serve any locally-stored legacy files exactly as it does today throughout the rollout.

## Constraints Confirmed for This Document

No production code, configuration, database, React, or Cloudflare R2 settings were modified to produce this design. This document describes an implementation plan for AI19.MEDIA.3.2; it does not itself implement the controller.
