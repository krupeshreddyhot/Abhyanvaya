# AI19.MEDIA.2 — Media Retrieval Pipeline Forensic Audit

**Type:** Production forensic investigation (READ-ONLY — no code, config, DB, React, or Cloudflare changes made)
**Scope:** Trace `GET /media/recognitions/1/{sessionId}/faces/00003.webp` from browser to response and determine exactly why it 404s now that uploads go to Cloudflare R2.

---

## Executive Summary

**The `/media/*` route is not connected to Cloudflare R2 (or to the `IStorageProvider` abstraction) at all.** It is served exclusively by ASP.NET Core's built-in static-file middleware (`UseStaticFiles`) pointed at a **local-disk** `PhysicalFileProvider`. There is no controller, no S3/R2 SDK call, and no "download" code path for `/media/*` whatsoever.

Meanwhile, **every upload** (classroom photos, recognition thumbnails, and student/logo photo variants) goes through the exact same `IStorageProviderFactory.GetActiveProvider()` selection, which **does** honor the configured provider (`local` or `s3`) and, when `s3`, correctly writes to Cloudflare R2 via `S3StorageProvider.WriteObjectAsync` → `AmazonS3Client.PutObjectAsync`.

So once the deployed configuration's active provider became `s3` (R2), the write side and the read side silently diverged:

- **Write side** (`RecognitionMediaService`, `AttendancePhotoService`, `MediaStorageService`) → provider-aware → now writes to R2.
- **Read side** (`GET /media/*`) → provider-**unaware**, hard-coded to a local directory (`Program.cs:396-403`) → still looks on local disk, where the object was never written.

This is a **structural gap that has existed since the static-file route was first added**, not a regression introduced by any recent code change, a case/slash/prefix bug in a key, or an R2 credential/permission problem. It only became *visible* the moment production's active storage provider switched from `local` to `s3`, because under `local` both the write and the (accidental) read happened to land on the same disk.

---

## Architecture Diagram

```
                         WRITE PATH (upload) — provider-aware
┌────────────────────┐   ┌──────────────────────────┐   ┌───────────────────────────┐
│ RecognitionMedia    │   │ ApplicationMediaStorage  │   │ IStorageProviderFactory    │
│ Service /           │──▶│ Service.SaveOriginalObj  │──▶│ .GetActiveProvider()       │
│ AttendancePhoto     │   │ ectAsync                 │   │ (reads MediaOptions        │
│ Service /           │   │ ApplicationMediaStorage  │   │  .Provider = "local"|"s3") │
│ MediaStorageService │   │ Service.cs:37             │   └─────────────┬─────────────┘
└────────────────────┘                                                 │
                                                     ┌───────────────────┴───────────────────┐
                                                     ▼                                       ▼
                                     LocalStorageProvider.WriteObjectAsync      S3StorageProvider.WriteObjectAsync
                                     (File.WriteAllBytesAsync, local disk)      (AmazonS3Client.PutObjectAsync → R2)
                                     LocalStorageProvider.cs:41-53               S3StorageProvider.cs:30-74


                         READ PATH (browser GET /media/...) — provider-BLIND
┌────────────────────┐   ┌────────────────────────────────────────────┐
│ Browser             │──▶│ UseStaticFiles(RequestPath="/media")       │
│ GET /media/          │  │ FileProvider = PhysicalFileProvider(       │
│ recognitions/1/{sid} │  │   ResolveLocalMediaPhysicalRoot(...))      │
│ /faces/00003.webp    │  │ Program.cs:396-403                         │
└────────────────────┘   └───────────────────┬────────────────────────┘
                                              │  looks up file on LOCAL DISK ONLY
                                              │  never calls IStorageProviderFactory,
                                              │  IStorageProvider, or any S3/R2 SDK method
                                              ▼
                                    local dir does not contain the R2-uploaded
                                    object → StaticFileMiddleware finds no file
                                    → passthrough → no endpoint matches → 404
```

---

## Sequence Diagram — `GET /media/recognitions/1/{sessionId}/faces/00003.webp`

```
Browser          Kestrel/ASP.NET Core Pipeline                                  Disk / R2
  │  GET /media/recognitions/1/{sid}/faces/00003.webp?v=169...
  │──────────────────────────────────────▶│
  │                                        │ UseExceptionHandler()      (Program.cs:345)
  │                                        │ UseCors("AllowReact")      (:347)
  │                                        │ [UseSwagger/UI — dev only] (:358-362)
  │                                        │ [UseHttpsRedirection — dev only] (:363-364)
  │                                        │ UseStaticFiles RequestPath=/branding (:388-393, conditional)
  │                                        │   → path doesn't start with /branding → next()
  │                                        │ UseStaticFiles RequestPath=/media (:398-403)
  │                                        │   → PhysicalFileProvider(mediaPhysical)
  │                                        │        .GetFileInfo("/recognitions/1/{sid}/faces/00003.webp")
  │                                        │───────────────────────────────────────▶│ local disk lookup
  │                                        │◀───────────────────────────────────────│ NOT FOUND
  │                                        │   → StaticFileMiddleware does NOT write a response,
  │                                        │     calls next() (default passthrough-on-miss behavior)
  │                                        │ UseStaticFiles (default wwwroot) (:405-412)
  │                                        │   → also not found → next()
  │                                        │ UseAuthentication() (:413)  — attaches identity, no block (no matched endpoint yet)
  │                                        │ UseAuthorization()  (:414)  — no-op (no endpoint selected to authorize)
  │                                        │ MapControllers()    (:415)  — endpoint routing tries to match
  │                                        │   → NO controller declares a route under "/media" (verified: zero matches
  │                                        │     for "media" in Abhyanvaya.API/Controllers/*.cs)
  │                                        │   → no endpoint matched
  │                                        │ MapPlatformHealthEndpoints (:417) — only /health*, no match
  │                                        │ [end of configured pipeline — nothing wrote a response]
  │◀───────────────────────────────────────│ 404 Not Found (framework default terminal response;
  │                                        │  no application code executes NotFound()/404 explicitly)
```

**Cloudflare R2 SDK is never invoked anywhere in this sequence.** `IStorageProviderFactory`, `IStorageProvider`, `S3StorageProvider`, and `LocalStorageProvider` do not appear in this request's call stack at all.

---

## Task 1 — Locate the endpoint

**Controller/action handling `/media/*`: none exists.**

- Searched every file in `Abhyanvaya.API/Controllers/*.cs` (30 controllers) case-insensitively for the literal `media`. Zero matches. (The only near-miss is `MediumController` → route `api/medium`, an unrelated language/medium lookup entity, confirmed by reading `Abhyanvaya.API/Controllers/MediumController.cs:14`.)
- Searched `Program.cs` for any `MapGet`/`Map(` containing `"media"`. Zero matches — the only `MapGet` calls found are for `/health`, `/health/live`, `/health/ready` inside `MapPlatformHealthEndpoints` (`Program.cs:417` call site).
- `/media` is registered purely as static-file middleware:

```396:403:Abhyanvaya.API/Program.cs
var mediaPhysical = ResolveLocalMediaPhysicalRoot(app.Configuration, app.Environment);
Directory.CreateDirectory(mediaPhysical);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaPhysical),
    RequestPath = "/media",
    OnPrepareResponse = AddPublicBrandingHeaders,
});
```

**Route attributes:** none — this is middleware, not MVC routing; there is no `[Route]`/`[HttpGet]` attribute involved.

**Endpoint registration:** `app.UseStaticFiles(...)` at `Program.cs:398`, immediately after the conditional `/branding` static files block (`:384-394`) and immediately before the default-wwwroot static files block (`:405-412`).

**Middleware ordering (exact, from `Program.cs`):**

| Line | Call |
|---|---|
| 345 | `app.UseExceptionHandler()` |
| 347 | `app.UseCors("AllowReact")` |
| 358-362 | `app.UseSwagger()` / `UseSwaggerUI()` (dev/EnableSwagger only) |
| 363-364 | `app.UseHttpsRedirection()` (Development only) |
| 384-394 | `app.UseStaticFiles(...)` → `/branding` (conditional on `Branding:PhysicalRoot`) |
| **398-403** | **`app.UseStaticFiles(...)` → `/media`** |
| 405-412 | `app.UseStaticFiles(...)` → default wwwroot |
| 413 | `app.UseAuthentication()` |
| 414 | `app.UseAuthorization()` |
| 415 | `app.MapControllers()` |
| 417 | `MapPlatformHealthEndpoints(app)` |

**Answer: Does the request actually enter this controller?** There is no controller to enter. The request is handled entirely by the static-file middleware registered at `Program.cs:398-403`; if that middleware doesn't find the file, the request falls through the rest of the pipeline (auth, controllers, health endpoints) without any of them matching, and terminates as an empty 404.

---

## Task 2 — Trace route parameter extraction

**There is no "route parameter extraction" in the MVC-routing sense, because static-file middleware does not use model binding.** Instead:

```
Incoming URL:      /media/recognitions/1/{sessionId}/faces/00003.webp?v=1752...
   ↓  ASP.NET Core strips the query string before path matching (query string is never
      inspected by StaticFileMiddleware; confirmed by StaticFileOptions having no
      querystring-related option and by RFC/ASP.NET Core static-file-middleware semantics —
      only HttpContext.Request.Path is matched)
Request.Path:      /media/recognitions/1/{sessionId}/faces/00003.webp
   ↓  StaticFileMiddleware compares Request.Path against StaticFileOptions.RequestPath = "/media"
      (Program.cs:401) using a case-insensitive segment prefix match
Matched prefix:    /media
Remaining subpath: /recognitions/1/{sessionId}/faces/00003.webp
   ↓  PhysicalFileProvider(mediaPhysical).GetFileInfo(subpath)  — subpath's leading "/" is
      combined directly with the provider's root directory; "/" is translated to the OS
      directory separator
Physical path probed: {mediaPhysical}/recognitions/1/{sessionId}/faces/00003.webp
                       where mediaPhysical = ResolveLocalMediaPhysicalRoot(...) (Program.cs:372-382)
                       = Media:PhysicalRoot, else Branding:PhysicalRoot, else
                         "{WebRootPath ?? ContentRootPath/wwwroot}/branding"
Result:            IFileInfo.Exists == false (object was written to R2, not to this local path)
   ↓
No "storage key" is ever constructed. StorageKeyHelper.NormalizeRelativeKey (used by both
IStorageProvider implementations, S3StorageProvider.cs:230-231 and LocalStorageProvider.cs:105)
is NEVER called for this request. IStorageProviderFactory.GetActiveProvider() is NEVER called
for this request. This is the single most important fact of this audit: the "download" side has
no storage-key concept at all — it is a raw filesystem path lookup.
```

**Exact string value after every step**, for the example in the prompt:

| Step | Value |
|---|---|
| Incoming URL (path only) | `/media/recognitions/1/{sessionId}/faces/00003.webp` |
| Matched `RequestPath` | `/media` |
| Remaining subpath handed to `PhysicalFileProvider` | `/recognitions/1/{sessionId}/faces/00003.webp` |
| Physical path probed on disk | `{mediaPhysical}\recognitions\1\{sessionId}\faces\00003.webp` (Windows) or `{mediaPhysical}/recognitions/1/{sessionId}/faces/00003.webp` (Linux/Render) |
| R2 object key that actually holds the bytes (per upload side, Task 3) | `recognitions/1/{sessionId}/faces/00003.webp` (via `RecognitionMediaService.BuildFaceImageKey`) |

These last two rows are the same *string*, but one is evaluated against a **local filesystem** and the other lives in an **R2 bucket** — they are never compared or reconciled by any code.

---

## Task 3 — Compare upload vs download keys

| | Upload (write) | Download (`/media/*` GET) |
|---|---|---|
| Entry point | `RecognitionMediaService.PersistFaceThumbnailAsync` (`RecognitionMediaService.cs:59-119`) → `ApplicationMediaStorageService.SaveOriginalObjectAsync` (`ApplicationMediaStorageService.cs:17-38`) | `UseStaticFiles` middleware (`Program.cs:398-403`) |
| Key/path construction | `$"recognitions/{tenantId}/{attendanceSessionId}/faces/{faceNumber:D5}.webp"` (`RecognitionMediaService.cs:126-127`), then `.Trim('/')` (`ApplicationMediaStorageService.cs:37`), then `StorageKeyHelper.NormalizeRelativeKey` inside the active provider (slash-normalize, trim leading `/`, reject traversal) | Raw URL path segment after the `/media` prefix, fed directly to `PhysicalFileProvider.GetFileInfo` — **no `StorageKeyHelper` call, no trimming/normalization shared with the upload side** |
| Destination | Whatever `IStorageProviderFactory.GetActiveProvider()` currently resolves to — **R2** if `Media:Provider`/`Branding:Provider` = `s3` | **Always** `ResolveLocalMediaPhysicalRoot(...)` (`Program.cs:372-382`) — **local disk**, regardless of `Media:Provider`/`Branding:Provider` |
| Example resulting key/path | R2 object key: `recognitions/1/3fae.../faces/00003.webp` | Local path probed: `{mediaPhysical}/recognitions/1/3fae.../faces/00003.webp` |

**Differences found:**

- **Slash:** none — both sides use `/` as the logical separator; the local side additionally converts to the OS separator only at the very last step (`LocalStorageProvider.cs:106`), which is irrelevant here because the local provider is never consulted for `/media` GETs at all.
- **Case:** no case transformation on either side (`StorageKeyHelper` does not call `.ToLower()`/`.ToUpper()` anywhere — confirmed by reading the full file, `StorageKeyHelper.cs:1-53`).
- **Tenant prefix:** identical on both "sides" as strings (`recognitions/{tenantId}/...`), but this is coincidental — the download side doesn't know or care what a tenant prefix is; it just treats the whole thing as a relative file path.
- **Trimming:** upload trims leading/trailing `/` (`ApplicationMediaStorageService.cs:37`, `StorageKeyHelper.cs:10`); the static-file middleware does its own independent leading-slash handling internal to `PhysicalFileProvider`/`StaticFileMiddleware` (framework code, not application code — not inspected further per the "no speculation" rule; **evidence not available** beyond documented ASP.NET Core behavior that it treats the path as relative to the provider root).
- **Encoding:** the incoming URL's query string (`?v=...`) is stripped by ASP.NET Core's own path/query splitting before any middleware sees `Request.Path` — this is standard framework behavior, not app-specific code, and applies identically to both a working and a failing request; it is not a source of divergence.
- **The one real, load-bearing difference:** the download side never touches `IStorageProviderFactory`, `IStorageProvider`, `StorageKeyHelper`, or any S3/R2 client. It is a completely separate, parallel code path from the upload side. This is not a string-formatting bug — it is an entire subsystem (the read/serve path) that was never wired to the provider abstraction.

---

## Task 4 — Cloudflare R2 configuration audit

Configuration is bound by `ConfigureMediaOptions.Configure` (`ConfigureMediaOptions.cs:16-28`), which reads (in order of precedence) `Media:{key}` then falls back to `Branding:{key}` via `MediaConfigurationResolver.Get` (`ConfigureMediaOptions.cs:34-40`), and each of those in turn falls back from `appsettings`/JSON to environment variables via `BrandingSettingsResolver.Get` (`Abhyanvaya.API/Common/BrandingSettingsResolver.cs:5-20`, checking `Key:Path` → `Key__Path` env var → `Key_Path` env var).

**Committed configuration** (`appsettings.json`):

```
"Branding": {
  "Provider": "local",
  "PhysicalRoot": "",
  "PublicBaseUrl": "",
  "S3": { "Bucket": "", "Region": "us-east-1", "Endpoint": "", "AccessKeyId": "", "SecretAccessKey": "", "ForcePathStyle": true }
}
```

`appsettings.Production.json` only overrides `Cors:AllowCloudflarePages` and `Branding:Provider: "local"` — **still `local`, in the committed file.**

**Evidence not available:** the actual live Render environment variables (`Media__Provider`, `Media__S3__Bucket`, `Media__S3__Endpoint`, `Media__S3__AccessKeyId`, `Media__S3__SecretAccessKey`, or their `Branding__...` equivalents) are not stored in this repository (`render.yaml` declares only `ASPNETCORE_ENVIRONMENT`, `Cors__AllowCloudflarePages`, `UseRedis`, `Jwt__Key`, `ConnectionStrings__DefaultConnection` — no `Media__*`/`Branding__S3*` keys). Given the task's stated premise that uploads are now landing in R2, the live environment must be setting `Media:Provider`/`Branding:Provider=s3` plus the `S3:*` bucket/endpoint/credential values via Render dashboard environment variables that are not checked into source control. This cannot be verified from the repository alone.

**Verify: does upload use Bucket X and download use Bucket X?**

- **Upload** uses whatever `_mediaOptions.S3.Bucket` resolves to at the moment `S3StorageProvider.GetRequiredBucket()` runs (`S3StorageProvider.cs:187-193`) — i.e. bucket **X** (R2), per the task's premise.
- **Download** (`/media/*`) **never references any bucket at all.** `ResolveLocalMediaPhysicalRoot` (`Program.cs:372-382`) only ever resolves `Media:PhysicalRoot` / `Branding:PhysicalRoot` (a local directory path) or the `wwwroot/branding` fallback — none of `MediaOptions.S3.*` is read anywhere in the static-file registration code.

**Finding:** this is not a "different bucket" problem — it is a "download never talks to any bucket" problem.

---

## Task 5 — Storage provider audit

**`GetObjectAsync()` / `ReadObjectAsync()` / `ExistsAsync()` implementations found:**

```76:106:Abhyanvaya.API/Media/S3StorageProvider.cs
public async Task<Stream> ReadObjectAsync(string relativeKey, CancellationToken cancellationToken)
{
    var bucket = GetRequiredBucket();
    var (s3, _, _, _) = BuildS3Client();
    using var _ = s3;

    var keyPath = NormalizeKey(relativeKey);
    try
    {
        using var response = await s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucket,
            Key = keyPath,
        }, cancellationToken).ConfigureAwait(false);
        ...
    }
    catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
        throw new FileNotFoundException($"Object not found: {keyPath}", keyPath, ex);
    }
}
```

```108:129:Abhyanvaya.API/Media/S3StorageProvider.cs
public async Task<bool> ExistsAsync(string relativeKey, CancellationToken cancellationToken)
{
    ...
    await s3.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = bucket, Key = keyPath }, cancellationToken)...
    return true; // or false on 404
}
```

```55:65:Abhyanvaya.API/Media/LocalStorageProvider.cs
public Task<Stream> ReadObjectAsync(string relativeKey, CancellationToken cancellationToken)
{
    var fullPath = ResolveFullPath(relativeKey);
    if (!File.Exists(fullPath))
        throw new FileNotFoundException($"Object not found: {relativeKey}", fullPath);
    ...
}
```

**Exact key passed to the SDK:** `NormalizeKey(relativeKey)` = `StorageKeyHelper.NormalizeRelativeKey(relativeKey)` (`S3StorageProvider.cs:230-231`) → slash-normalized, leading-slash-trimmed, e.g. `recognitions/1/{sessionId}/faces/00003.webp` — **no extra `media/` prefix is ever added.**

**Who calls these methods?**

- `S3StorageProvider.ReadObjectAsync` / `ExistsAsync`: only consumer found project-wide is `MediaObjectReader` (`Abhyanvaya.API/Media/MediaObjectReader.cs`), which is only injected into recognition/embedding services (`ClassroomRecognitionPipeline`, `InsightFaceEmbeddingGenerator` — confirmed via DI registration search), **never** into any controller or into the static-file pipeline.
- **For the `/media/*` HTTP route specifically, none of `GetObjectAsync`, `ReadObjectAsync`, `ExistsAsync`, or any other `IStorageProvider`/`S3StorageProvider` method is ever called.** The SDK is not part of this request's call stack (confirmed by the complete middleware trace in Task 1/the Sequence Diagram, and by there being no controller or endpoint that could invoke `IMediaObjectReader`/`IStorageProvider` for this route).

**Document the exact call:** N/A for this specific failing request — **evidence not available in the sense that there is no such call to show**, because it never happens.

---

## Task 6 — HTTP 404 origin

**Who returns HTTP 404?** By elimination against the given list:

| Candidate | Ruled in/out | Evidence |
|---|---|---|
| MediaController | Ruled out | No such controller exists (Task 1) |
| StorageProvider | Ruled out | Never invoked for this route (Task 5) |
| Cloudflare SDK | Ruled out | Never invoked for this route (Task 5) |
| Exception Filter | Ruled out | `UseExceptionHandler()` (`Program.cs:345`) only fires when a downstream middleware **throws**; `StaticFileMiddleware` does not throw when a file is missing — it silently calls `next()` (this is documented, standard ASP.NET Core static-file-middleware behavior: missing-file is a pass-through, not an exception) |
| Middleware | **Most consistent with evidence** | The `/media` `StaticFileMiddleware` (`Program.cs:398-403`) finds no file and passes the request on; every subsequent middleware/endpoint (`UseAuthentication`, `UseAuthorization`, `MapControllers`, `MapPlatformHealthEndpoints`) also fails to match this path (Task 1 table); with no endpoint selected and no middleware writing a response, ASP.NET Core's hosting pipeline emits the framework-default 404 |
| Authorization | Ruled out | Authorization middleware only acts on a *matched* endpoint; no endpoint matches `/media/*`, so no authorization policy is ever evaluated for this request |
| Custom `NotFound()` | Ruled out | No controller action, and therefore no `NotFound()`/`ActionResult` of any kind, executes for this route |

**Conclusion:** the 404 is the ASP.NET Core hosting pipeline's default terminal response for a request that matched no endpoint and no middleware chose to handle — it is **not** produced by any explicit application code (no controller, filter, or handler writes this 404). This is a direct architectural consequence of Task 1's finding (no endpoint exists for `/media/*` beyond the local-disk static-file probe).

---

## Task 7 — R2 SDK response

**Evidence not available** — and, per Task 5/6, this is expected: the R2/S3 SDK (`AmazonS3Client`) is never invoked for a `GET /media/*` browser request in the current architecture, so there is no SDK-level response (`NoSuchKey`, `403`, `AccessDenied`, etc.) to observe for this specific failure. Any such SDK-level error code would only be observable for calls made through `MediaObjectReader`/`S3StorageProvider.ReadObjectAsync` (i.e. when the recognition pipeline re-reads a classroom photo it previously wrote) — not for the browser's `/media/*` request path.

---

## Task 8 — Compare classroom image retrieval

| | Classroom image | Recognition thumbnail |
|---|---|---|
| Upload code | `AttendancePhotoService.cs:138-145` → `AttendanceSessionStoragePaths.BuildClassroomImageKey` → `attendance/{tenantId}/sessions/{sessionId}/classroom{ext}` → `IMediaStorageService.SaveOriginalObjectAsync` | `RecognitionMediaService.cs:59-127` → `BuildFaceImageKey` → `recognitions/{tenantId}/{sessionId}/faces/{faceNumber:D5}.webp` → `IMediaStorageService.SaveOriginalObjectAsync` |
| Upload provider selection | `ApplicationMediaStorageService.SaveOriginalObjectAsync` → `IStorageProviderFactory.GetActiveProvider()` (`ApplicationMediaStorageService.cs:30`) | **Identical call** — same interface, same implementation, same factory (`ApplicationMediaStorageService.cs:30`) |
| Public URL builder | `AttendanceSessionMediaPaths.BuildMediaUrl` → `/media/{key}?v=...` | **Identical function** — same `AttendanceSessionMediaPaths.BuildMediaUrl` |
| HTTP serving | Same `/media` `StaticFileMiddleware` (`Program.cs:398-403`) | **Identical middleware, identical local-disk root** |

**Determine whether both travel through identical code:** **Yes — byte-for-byte identical code paths** on both the write side (same `IMediaStorageService` interface call, same provider factory) and the read side (same static-file middleware, same physical root resolution). There is no branch anywhere that treats `attendance/...` keys differently from `recognitions/...` keys.

**Consequence:** if the active provider is `s3`, classroom images uploaded through this same code are subject to the **exact same** local-disk/R2 mismatch as recognition thumbnails. The task statement says classroom images are "uploaded to Cloudflare R2" but does not confirm whether classroom images currently render correctly in the browser; based purely on code-path identity, there is no architectural reason for classroom images to behave any differently from recognition thumbnails once the provider is `s3`. **Evidence not available** to confirm the live browser-rendering state of classroom images in production one way or the other — this statement is a code-path inference, not an observed result.

---

## Task 9 — Middleware audit

Reviewed `Program.cs` (983 lines, full file read) for `MapControllers()`, `UseStaticFiles()`, media endpoints, authentication, tenant middleware, and exception middleware. There is no `Startup.cs` in this project (minimal hosting model, everything is in `Program.cs`).

**Full relevant ordering** (already itemized in Task 1's table). Key facts:

- `UseExceptionHandler()` (`:345`) is the very first middleware — it can only affect requests where something **throws**. `StaticFileMiddleware` does not throw on a missing file, so it never engages for this scenario (Task 6).
- There is no tenant-resolution middleware anywhere in this pipeline ahead of the `/media` static files (searched for `UseMiddleware`, `Use(` calls — the only custom `Use...()` calls in `Program.cs` are `UseExceptionHandler`, `UseCors`, `UseSwagger`/`UseSwaggerUI`, `UseHttpsRedirection`, three `UseStaticFiles` calls, `UseAuthentication`, `UseAuthorization`). Tenant scoping in this codebase is applied via `ICurrentUserService`/DbContext query filters inside controllers/services, not via HTTP middleware, and therefore cannot affect a request that never reaches a controller.
- `UseAuthentication()`/`UseAuthorization()` (`:413-414`) run **after** all three `UseStaticFiles` calls. This means: (a) a successful static-file hit is served with **no authentication check at all** (by design — branding/media assets are intentionally public, consistent with the `Access-Control-Allow-Origin: *` header added in `AddPublicBrandingHeaders`, `Program.cs:366-370`); (b) a missed static-file lookup (this bug) still passes through auth/authz harmlessly because no endpoint match occurs, so authorization middleware has nothing to authorize.
- **Can `/media/*` be intercepted?** No blocking interception was found — the request is never rejected by CORS, auth, or an exception filter. It simply finds no code anywhere in the pipeline willing to serve it once the local file is absent.

---

## Task 10 — URL generation audit

| Generator | Produces | Matches configured route? |
|---|---|---|
| `AttendanceSessionMediaPaths.BuildMediaUrl` (`AttendanceSessionMediaPaths.cs:6-14`) | `/media/{relativeKey}?v={unix}` | Yes — literal prefix `/media` matches `StaticFileOptions.RequestPath = "/media"` (`Program.cs:401`) exactly, case for case |
| `AttendanceSessionMediaPaths.BuildImageUrl` (`:16-27`) | `/media/{imageKey}/{variant}.webp?v={unix}` (or delegates to `BuildMediaUrl` if the key already has an extension) | Same `/media` prefix, same result |
| `StudentMediaPaths.BuildVariantPath` (`StudentMediaPaths.cs:11-27`) | `/media/{photoKey}/{variant}.webp?v={unix}` (or `{publicBaseUrl}/...` if a base URL is supplied — not used by the review DTO mapping per AI19.MEDIA.1) | Same `/media` prefix |

**Verify Generated URL → Controller Route → Expected Route match:** the generated URL prefix (`/media`) and the registered `StaticFileOptions.RequestPath` (`/media`) are **identical strings** — there is no mismatch here. The URL generation layer is not the defect; the defect is that whatever is behind `/media` (the static-file middleware) resolves against local disk instead of the active storage provider.

---

## Task 11 — Logging audit

**Existing logging relevant to this failure:**

- `S3StorageProvider.WriteObjectAsync` logs `"S3 upload failed for key {KeyPath}..."` on exception (`S3StorageProvider.cs:63-73`) — **upload-side only**, and only on failure; a *successful* R2 upload is not logged at all by this method (no `LogInformation` on the success path in `S3StorageProvider.WriteObjectAsync`).
- `RecognitionMediaService` logs `"Recognition Thumbnail Upload Started/Completed/Failed"` (`RecognitionMediaService.cs:70-77, 88-95, 103-113`) — confirms whether the *upload* succeeded, but says nothing about later retrieval.
- **Program.cs** logs the resolved branding provider/public base URL once at startup (`Program.cs:349-354`: `"Branding configured with Provider={Provider}, PublicBaseUrl={PublicBaseUrl}"`) — this *does* reveal which provider is active at boot, but does not reveal the resolved `mediaPhysical` root path, nor does it log anything for individual `/media/*` requests (ASP.NET Core's default `StaticFileMiddleware` does not emit application-level logs for cache hits/misses beyond its own internal `Microsoft.AspNetCore.StaticFiles` category at `Debug`/`Trace` verbosity, which is not elevated by this app's `Logging` configuration in any appsettings file reviewed).
- **No log statement anywhere records**: the physical root directory `/media` is actually serving from at runtime, or the outcome (hit/miss) of an individual static-file lookup, or a comparison between "provider currently active" and "provider the static-file route is bound to."

**Is existing logging sufficient to explain this issue from log output alone?** No. An operator watching current logs would see successful R2 uploads (`RecognitionMediaService`'s "Completed" log) and would see the one-time startup provider log, but would have no log line correlating a specific `/media/*` 404 back to "this route only ever checks local disk, never the active provider."

**Diagnostic logging recommendation (documentation only — do not implement):**

1. Add a one-time startup log immediately after `Program.cs:396` (`mediaPhysical` resolution) that prints the resolved `mediaPhysical` absolute path **and** the currently active `MediaOptions.Provider`/`GetActiveProviderName()` value side by side, so a operator can immediately see "static files are serving from `X`, but the active storage provider is `Y`" whenever they differ.
2. Consider (documentation only) a `StaticFileOptions.OnPrepareResponse`/complementary `IApplicationBuilder` branch that logs at `Warning` level whenever a request under `/media` results in a 404 and the active provider is `s3` — today, `AddPublicBrandingHeaders` (`Program.cs:366-370`) only runs `OnPrepareResponse`, which — per ASP.NET Core's `StaticFileMiddleware` semantics — is invoked only when a file **is found**, so it cannot be used as-is to log misses; a genuinely new middleware/logging hook would be needed to observe misses, which is out of scope for this diagnostics-only recommendation and is intentionally not designed further here.

---

## Root Cause Candidates

Only one candidate is supported by direct code evidence; it is listed first and is the confirmed cause. The remaining candidates from the task's own example list are explicitly checked and ruled out below.

1. **CONFIRMED — `/media/*` retrieval never consults the active storage provider.** The static-file middleware registered at `Program.cs:398-403` always resolves against a local-disk `PhysicalFileProvider`, entirely independent of `MediaOptions.Provider`. Once the active provider became `s3` (R2) for uploads, the read side kept looking in a directory that never receives R2-written objects. No S3/R2 SDK call, no `IStorageProvider` call, and no controller participate in serving `/media/*` at all.
2. **Ruled out — key/case/slash mismatch between upload and download keys.** Task 3 shows the download side does not construct a "key" in the application sense at all (no `StorageKeyHelper` call); the string that *would* match is never even compared against anything R2-related.
3. **Ruled out — wrong bucket configured for download.** Task 4 shows the download side references no bucket, configuration section, or S3 option whatsoever.
4. **Ruled out — R2 credentials/permissions (403/AccessDenied).** Task 5/7 show the SDK is never called for this route; there is no credential or permission check to fail.
5. **Ruled out — authorization blocking the request.** Task 9 shows `/media` static files run before `UseAuthentication`/`UseAuthorization`, and no endpoint match occurs for a miss, so authorization is never evaluated.
6. **Ruled out — exception swallowed by a filter.** Task 6 shows `StaticFileMiddleware` does not throw on a miss; `UseExceptionHandler()` never engages.

---

## Evidence Table

| # | Claim | File | Line(s) |
|---|---|---|---|
| 1 | `/media` registered as local-disk static files, not a controller | `Program.cs` | 396-403 |
| 2 | Local media root resolution ignores `MediaOptions.Provider`/S3 settings entirely | `Program.cs` | 372-382 |
| 3 | No controller declares any `/media` route | `Abhyanvaya.API/Controllers/*.cs` | (absence confirmed project-wide) |
| 4 | Recognition thumbnail upload key + write call | `RecognitionMediaService.cs` | 59-127 |
| 5 | Classroom image upload key + write call | `AttendancePhotoService.cs` | 138-145; `AttendanceSessionStoragePaths.cs:6-9` |
| 6 | Shared write entry point for both | `ApplicationMediaStorageService.cs` | 17-38 |
| 7 | Provider selection (Local vs S3) — single source used by all uploads | `StorageProviderFactory.cs` | 1-25; `MediaOptions.cs:21-27` |
| 8 | S3/R2 write call | `S3StorageProvider.cs` | 30-74 |
| 9 | S3/R2 read call (used only by `MediaObjectReader`, never by `/media` HTTP) | `S3StorageProvider.cs` | 76-106 |
| 10 | Local-disk read call (used only by `MediaObjectReader`, never by `/media` HTTP directly) | `LocalStorageProvider.cs` | 55-65, 100-113 |
| 11 | Key normalization helper (no case transform, slash-normalize only) | `StorageKeyHelper.cs` | 1-53 |
| 12 | `MediaObjectReader` consumers are recognition/embedding only | `MediaObjectReader.cs`; DI registrations in `Program.cs`/`Infrastructure/DependencyInjection.cs` | full file; registration sites |
| 13 | Config binding + env var fallback mechanism | `ConfigureMediaOptions.cs` | 1-40 |
| 14 | Env var double/single-underscore fallback | `BrandingSettingsResolver.cs` | 5-20 |
| 15 | Committed provider default is `local` (both `appsettings.json` and `appsettings.Production.json`) | `appsettings.json`; `appsettings.Production.json` | Branding section |
| 16 | Live R2 credentials/provider override not present in repo | `render.yaml` | 17-29 (env var list) |
| 17 | Startup log reveals active provider but not the static-file root or per-request outcomes | `Program.cs` | 349-354 |
| 18 | URL generation prefix (`/media`) matches registered `RequestPath` exactly | `AttendanceSessionMediaPaths.cs:6-14`; `StudentMediaPaths.cs:11-27`; `Program.cs:401` | as cited |
| 19 | Middleware order: static files precede auth; no tenant middleware exists | `Program.cs` | 345-417 |

---

## Verification

No production code, configuration, database, React, or Cloudflare R2 settings were modified during this investigation. Only read-only inspection commands (`Read`, `Grep`, `Glob`) were used against the working tree.

`dotnet build` was run to confirm the build remains clean (no files were changed, so this is a no-op verification):

**Result: 0 errors** (pre-existing nullable-reference warnings only, unrelated to this investigation).

---

## Constraints Confirmed

- No changes to: recognition pipeline, AI models, face matching, thresholds, ONNX configuration, database, DTOs, React, API contracts, media URL generation, storage paths, Cloudflare configuration, or `RecognitionMediaService`.
- No fixes, no refactoring, no optimizations were implemented.
- No commits were made.
