# AI18.REVIEW.1 — Recognition Review Thumbnail Pipeline Audit

**Read-only investigation. No code, database, API, UI, S3, or configuration changes were made.**
Every finding below cites an exact file and line. Where a claim could not be backed by a direct code
reference, it is explicitly marked as such rather than stated as fact.

---

## Executive Summary

**The face-crop thumbnail image is generated in memory and then discarded — it is never written to
any storage backend.** The database column that is supposed to point at it (`FaceImageKey`) is
populated unconditionally with a well-formed *key string*, and the review API builds a syntactically
valid URL from that string — so every layer downstream of the missing upload believes a thumbnail
exists. The browser requests a URL that 404s, and the MUI `Avatar` component silently swallows the
image-load failure and renders its placeholder/initials fallback instead. This is why "recognized
students appear without face thumbnails" with no visible error anywhere in the stack — every component
is behaving exactly as coded; the one missing piece is a single upload call that was never added.

**Exact break point:**
`Abhyanvaya.Infrastructure/Recognition/ClassroomRecognitionPipeline.cs`, inside the `foreach (var face
in detection.Faces)` loop (lines 141–163) — `face.AlignedFaceBytes` (populated by
`InsightFaceEngine.DetectAsync`) is never read, and `FaceImageKey` (line 152) is computed as a pure
string formula with no corresponding call to any storage-write API.

---

## Task 1 — Complete Thumbnail Lifecycle Trace

| Stage | Class | Method | Object | File | Output | Lifetime |
|---|---|---|---|---|---|---|
| 1. Classroom Photo | `ClassroomRecognitionPipeline` | `ProcessAsync` | `imageBytes` (`byte[]`) | `Abhyanvaya.Infrastructure/Recognition/ClassroomRecognitionPipeline.cs:96-98` | Full uploaded classroom JPEG/PNG bytes, read via `IMediaObjectReader` | Local var, held for the whole `ProcessAsync` call |
| 2. Detected Face | `InsightFaceEngine` | `DetectAsync` | `candidate` → `DetectedFaceDto` | `Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs:38-124` (esp. 62-108) | One `DetectedFaceDto` per face, added to `faces` list (returned in `FaceDetectionResponse.Faces`) | Lives for the rest of `DetectAsync`, then returned up to `ClassroomRecognitionPipeline` |
| 3. ImageSharp Crop | `InsightFaceImageMath` (called from `InsightFaceEngine.DetectAsync`) | `AlignFace` | `aligned` (`Image<Rgb24>`) | `InsightFaceEngine.cs:68` (`using var aligned = InsightFaceImageMath.AlignFace(...)`) | 112×112 `Rgb24` aligned face crop | Scoped to one loop iteration (`using var`); disposed at end of that iteration (`InsightFaceEngine.cs:106`, per AI17.RUNTIME.2/.4 audit) |
| 4. Recognition Thumbnail | `InsightFaceEngine` | `DetectAsync` (inline, not a separate method) | `alignedBytes` (`byte[]`, WebP) | `InsightFaceEngine.cs:83-88` (`await aligned.SaveAsWebpAsync(ms, ...)`) | WebP-encoded byte array of the 112×112 crop | Assigned into `DetectedFaceDto.AlignedFaceBytes` at line 117 (previously "line 101" pre-AI17 numbering) — **then referenced nowhere else in the codebase** |
| 5. Storage | *(none — this stage does not exist in code)* | — | — | — | **No write call exists.** `alignedBytes`'s only consumer is the DTO field assignment in step 4; nothing ever calls `IStorageProvider.WriteObjectAsync`/`IMediaStorageService.SaveOriginalObjectAsync`/`MediaStorageService.SaveVariantsAsync` with these bytes anywhere in the codebase (confirmed by full-repository search — see Task 3/6) | The `byte[]` becomes unreachable once `DetectedFaceDto` itself is no longer referenced (after `ClassroomRecognitionPipeline.ProcessAsync` finishes using `detection.Faces` — the DTO is never persisted or forwarded past that method) and is garbage-collected |
| 6. AttendanceRecognition | `ClassroomRecognitionPipeline` | `ProcessAsync` | `AttendanceRecognition.FaceImageKey` | `ClassroomRecognitionPipeline.cs:152` + `BuildFaceImageKey` at line 278 | `string` — `recognitions/{tenantId}/{sessionId}/faces/{faceNumber:D5}.webp` | Persisted to the database via `SaveChangesAsync` (line 176); durable, but **points at an object key that was never written to storage** |
| 7. API DTO | `AttendanceRecognitionReviewService` | `MapToReviewDtoAsync` | `AttendanceRecognitionReviewDto.FaceThumbnailUrl` | `Abhyanvaya.Application/AttendanceRecognitionReviewService.cs:497-499, 534` | `string?` — `/media/{key}?v={unixTimestamp}` (via `AttendanceSessionMediaPaths.BuildMediaUrl`) | Built fresh on every `GET .../recognitions` request; pure string formatting, no existence check against storage |
| 8. React UI | `RecognitionCard` / `SelectedFaceDetailsPanel` | render | MUI `<Avatar src={faceUrl}>` | `abhyanvaya-ui/src/components/attendance-recognition/RecognitionCard.tsx:30,82`; `SelectedFaceDetailsPanel.tsx:55,66` | `<img>` element inside the Avatar, `src` = `mediaAssetUrl(recognition.faceThumbnailUrl)` | Lives for the component's render lifetime |
| 9. Browser | — | — | HTTP `GET /media/recognitions/{tenant}/{session}/faces/{n}.webp` | N/A (browser-issued request) | **404 Not Found** (object never existed at that path in either the local static-file root or the S3/R2 bucket) | Request completes, fails, triggers `<img>`'s `onerror` |

## Task 1 — Lifecycle diagram

```
Classroom Photo (bytes)
        │  ClassroomRecognitionPipeline.ProcessAsync (line 96-98)
        ▼
IFaceDetectionService.DetectAsync → InsightFaceEngine.DetectAsync (line 38)
        │
        ├─ Image.Load<Rgb24> ─────────────────────────────► "source image" (AI17 tracked)
        │
        ▼
   DetectFaces(image) → candidates                          (line 52)
        │
        ▼
   foreach candidate (line 62):
        │
        ├─► AlignFace ──► aligned: Image<Rgb24> 112×112     (line 68)   [ImageSharp Crop]
        │
        ├─► ExtractEmbedding(aligned) ──► embedding[]        (line 73)
        │
        ├─► aligned.SaveAsWebpAsync(ms) ──► alignedBytes     (line 83-88)   [Recognition Thumbnail]
        │
        └─► faces.Add(new DetectedFaceDto { ..., AlignedFaceBytes = alignedBytes })   (line 117)
                                    │
                                    ▼
                     ══════════ BREAK POINT ══════════
                     alignedBytes / DetectedFaceDto.AlignedFaceBytes
                     is READ NOWHERE ELSE IN THE CODEBASE.
                     No IStorageProvider.WriteObjectAsync call.
                     No IMediaStorageService.SaveOriginalObjectAsync call.
                     ═════════════════════════════════════

   ClassroomRecognitionPipeline.ProcessAsync (back in this method)
        │
        ▼
   foreach face in detection.Faces (line 141):
        │
        └─► new AttendanceRecognition { FaceImageKey = BuildFaceImageKey(...) }  (line 152)
                     [a STRING is computed; the BYTES from above are never used]
                                    │
                                    ▼
                     _context.AddRangeAsync(recognitions) → SaveChangesAsync (line 165, 176)
                                    │
                                    ▼
                     AttendanceRecognitionReviewService.MapToReviewDtoAsync (line 497)
                     FaceThumbnailUrl = BuildMediaUrl(FaceImageKey, CreatedUtc)
                     [a URL STRING is computed from the key; no existence check]
                                    │
                                    ▼
                     GET /api/attendance-sessions/{id}/recognitions
                                    │
                                    ▼
                     React: RecognitionCard / SelectedFaceDetailsPanel
                     <Avatar src={mediaAssetUrl(faceThumbnailUrl)}>
                                    │
                                    ▼
                     Browser: GET /media/recognitions/.../faces/00001.webp
                                    │
                                    ▼
                              404 Not Found
                                    │
                                    ▼
                     MUI Avatar <img onerror> → silently renders fallback
                     (the "#{faceNumber}" text seen in the UI today)
```

---

## Task 2 — Face Crop Creation

**Exact location:** `Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs`, method `DetectAsync`
(lines 38–124), specifically the `foreach (var candidate in selectedCandidates)` loop (line 62 onward).

| Property | Value | Evidence |
|---|---|---|
| Original image dimensions | Whatever the uploaded classroom photo's decoded `Rgb24` dimensions are (`image.Width`/`image.Height`) — not fixed | `InsightFaceEngine.cs:47` (`Image.Load<Rgb24>(request.ImageBytes)`) |
| Crop rectangle | Not a simple axis-aligned rectangle crop — `AlignFace` performs a **similarity-transform warp** (rotation + scale + translation) driven by the 5 facial landmarks, sampling from `source` into a new fixed-size buffer | `Abhyanvaya.Infrastructure/InsightFace/InsightFaceImageMath.cs:60-88` (`AlignFace`, `EstimateSimilarityTransform`) |
| Crop image object | `aligned` — `Image<Rgb24>`, newly allocated at `outputSize × outputSize` | `InsightFaceEngine.cs:68`; `InsightFaceImageMath.cs:63` (`new Image<Rgb24>(outputSize, outputSize)`) |
| Resize operation | None separate from the alignment warp itself — `outputSize` is passed in directly as `_options.RecognitionInputSize` | `InsightFaceEngine.cs:68` (`_options.RecognitionInputSize`) |
| Target dimensions | **112×112** (`InsightFaceOptions.RecognitionInputSize` default) | `Abhyanvaya.Infrastructure/InsightFace/InsightFaceOptions.cs:20` |
| Encoding format | **WebP** | `InsightFaceEngine.cs:85` (`aligned.SaveAsWebpAsync(ms, cancellationToken)`) |
| Pixel format | `Rgb24` (3 bytes/pixel, no alpha) | `InsightFaceImageMath.cs:63` |

### Task 2 — Sequence diagram

```
ClassroomRecognitionPipeline        InsightFaceEngine              InsightFaceImageMath        ImageSharp
        │  DetectAsync(request)            │                              │                       │
        ├──────────────────────────────────►│                              │                       │
        │                                   │  Image.Load<Rgb24>()         │                       │
        │                                   ├──────────────────────────────┼──────────────────────►│
        │                                   │◄─────────────────────────────┼───────────────────────┤ image
        │                                   │  DetectFaces(image)          │                       │
        │                                   │  → candidates                │                       │
        │                                   │  foreach candidate:          │                       │
        │                                   │  AlignFace(image, landmarks, 112)                    │
        │                                   ├──────────────────────────────►                       │
        │                                   │                              │ new Image<Rgb24>(112,112)
        │                                   │                              ├──────────────────────►│
        │                                   │                              │  warp-sample pixels    │
        │                                   │◄─────────────────────────────┤ aligned                │
        │                                   │  ExtractEmbedding(aligned)   │                       │
        │                                   │  → embedding[]               │                       │
        │                                   │  aligned.SaveAsWebpAsync(ms) │                       │
        │                                   ├──────────────────────────────┼──────────────────────►│
        │                                   │◄─────────────────────────────┼───────────────────────┤ alignedBytes
        │                                   │  faces.Add(new DetectedFaceDto {                     │
        │                                   │      ..., AlignedFaceBytes = alignedBytes })          │
        │                                   │      ▲                                                │
        │                                   │      └─ LAST point alignedBytes is referenced.         │
        │◄──────────────────────────────────┤  return FaceDetectionResponse { Faces = faces }        │
        │  detection.Faces[i].AlignedFaceBytes  ◄── never read from here on
```

---

## Task 3 — Thumbnail Persistence

**Answer: none of (A) database, (B) S3, (C) temporary storage, or (D) on-demand generation actually
happens for the image bytes.** Only the *key string* is "generated on demand" (a pure format-string
computation) — the pixel data it is supposed to name is never persisted anywhere.

| Aspect | Finding | Evidence |
|---|---|---|
| Storage strategy | **Intended:** the key format (`recognitions/{tenantId}/{sessionId}/faces/{faceNumber:D5}.webp`) matches the same `IStorageProvider`-backed convention used successfully elsewhere (e.g. student photos via `IMediaStorageService.SaveOriginalObjectAsync`). **Actual:** no write call exists for this key anywhere in the codebase. | `ClassroomRecognitionPipeline.cs:278-279` (`BuildFaceImageKey`); confirmed by repository-wide search for any `WriteObjectAsync`/`SaveOriginalObjectAsync`/`SaveVariantsAsync` call site referencing `FaceImageKey`, `AlignedFaceBytes`, or the `recognitions/.../faces/` path — none found outside the key-string formula itself |
| Filename / object key | `{faceNumber:D5}.webp`, e.g. `00001.webp` | `ClassroomRecognitionPipeline.cs:279` |
| Bucket / path | `recognitions/{tenantId}/{sessionId}/faces/` — a relative key under whichever `IStorageProvider` is active (`local` → `MediaOptions.PhysicalRoot`, per `LocalStorageProvider.ResolveRootDirectory`, `Abhyanvaya.API/Media/LocalStorageProvider.cs:31-39`; `s3`/R2 → bucket from `MediaOptions`, per `S3StorageProvider.WriteObjectAsync`, `Abhyanvaya.API/Media/S3StorageProvider.cs:30-60`) — **moot, since `WriteObjectAsync` is never called with this key** | `ClassroomRecognitionPipeline.cs:279`; `LocalStorageProvider.cs`; `S3StorageProvider.cs` |
| Retention | N/A — nothing is ever stored, so there is nothing to retain or expire | — |

### Task 3 — Storage diagram (intended vs. actual)

```
INTENDED (never happens):                          ACTUAL (what really happens):

InsightFaceEngine                                   InsightFaceEngine
   │ alignedBytes                                       │ alignedBytes
   ▼                                                     ▼
IStorageProvider.WriteObjectAsync(                  DetectedFaceDto.AlignedFaceBytes = alignedBytes
   "recognitions/{t}/{s}/faces/00001.webp",              │
   alignedBytes)                                         ▼ (never read again)
   │                                                 garbage-collected
   ▼
Local disk file  OR  S3/R2 object
   │
   ▼
GET /media/recognitions/{t}/{s}/faces/00001.webp
   → 200 OK, image bytes                           GET /media/recognitions/{t}/{s}/faces/00001.webp
                                                        → 404 Not Found (object never created)
```

---

## Task 4 — AttendanceRecognition Audit

**Entity file:** `Abhyanvaya.Domain/Entities/AttendanceRecognition.cs`.

| Field | Present? | Line | Purpose | Populated by |
|---|---|---|---|---|
| `FaceImageKey` | ✅ Yes — the **only** thumbnail-related field on this entity | `AttendanceRecognition.cs:87` | "Storage key for the cropped face image generated during AI processing" (per the field's own XML doc comment) | `ClassroomRecognitionPipeline.cs:152` (`BuildFaceImageKey`) |
| `ThumbnailImageKey` | ❌ Does not exist on this entity | — | — | — |
| `ThumbnailUrl` | ❌ Does not exist on the entity (exists only as a *DTO* field, computed, not stored — see Task 7) | — | — | — |
| `AnnotatedImageKey` | ❌ Does not exist on `AttendanceRecognition` (a conceptually similar "annotated" *variant name* exists for the full classroom photo on `AttendanceSession`/via `AttendanceSessionMediaPaths.BuildImageUrl(..., variant: "annotated")`, but that is a different entity and a different image entirely) | — | — | — |
| `FaceCropKey` | ❌ Does not exist | — | — | — |

**Determination:** `FaceImageKey` is unambiguously the field intended to hold the recognized face
image's storage key — its own doc comment says so directly, its format exactly matches the
webp-encoded crop `InsightFaceEngine` produces, and it is the only field the review DTO's thumbnail URL
is built from (`AttendanceRecognitionReviewService.cs:497-499`). There is no ambiguity or competing
field to investigate here — the field is correctly named and correctly wired on the *read* side; it is
only ever *written* with a key that names a non-existent object.

---

## Task 5 — Recognition Pipeline Audit

**Location:** `Abhyanvaya.Infrastructure/Recognition/ClassroomRecognitionPipeline.cs`, method
`ProcessAsync`, lines 140–165.

```
Recognition Result (matches[i], detection.Faces[i])
        │  (ClassroomRecognitionPipeline.cs:141-163)
        ▼
Create AttendanceRecognition  ── new AttendanceRecognition { ... FaceImageKey = BuildFaceImageKey(...) }
        │                                                                   (line 144-162)
        ▼
Populate Thumbnail  ──  ⚠ THIS STEP DOES NOT EXIST.
        │                No code between object construction (line 144) and
        │                `_context.AddRangeAsync(recognitions)` (line 165) ever
        │                reads `face.AlignedFaceBytes` or calls any storage-write API.
        ▼
SaveChanges  ──  ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, ct)  (line 176)
```

**Verify — does `FaceImageKey` get assigned? If yes, from where?**

**Yes**, unconditionally, from `BuildFaceImageKey(session, face.FaceIndex)`
(`ClassroomRecognitionPipeline.cs:152`, method body at line 278-279):

```278:279:Abhyanvaya.Infrastructure/Recognition/ClassroomRecognitionPipeline.cs
    private static string BuildFaceImageKey(AttendanceSession session, int faceNumber) =>
        $"recognitions/{session.TenantId}/{session.Id}/faces/{faceNumber:D5}.webp";
```

This is a **pure string formula** — it depends only on `session.TenantId`, `session.Id`, and
`faceNumber`, none of which reflect whether any image bytes were ever written anywhere. It is
`static`, has no I/O, cannot fail, and cannot return null (hence "why not" doesn't apply — it always
succeeds at producing a *key*; the gap is that nothing ever uses that key to actually store anything).

---

## Task 6 — Storage Verification

**`SaveRecognitionThumbnail()` does not exist anywhere in this codebase.** No method with this name,
or any equivalent (e.g. `UploadFaceCrop`, `PersistThumbnail`, `SaveFaceImage`), was found by
repository-wide search. The intended sequence this task asks to trace —

```
SaveRecognitionThumbnail() → S3 Upload → Returned Key → Database Assignment
```

— **has no corresponding code.** `ClassroomRecognitionPipeline` (the class that would need to call
such a method) is injected only with `IMediaObjectReader` (`ClassroomRecognitionPipeline.cs:22, 36,
49`), whose interface (`Abhyanvaya.Application/Common/Interfaces/IMediaObjectReader.cs:6-14`) exposes
only `ReadVariantAsync`/`ReadObjectAsync` — **no write method at all.** The write-capable interface
that exists elsewhere in the same layer, `Abhyanvaya.Application.Common.Interfaces.IMediaStorageService`
(`SaveOriginalObjectAsync`/`DeleteObjectAsync`, used successfully by `AttendancePhotoService.cs:145`
for the original classroom photo upload), is **never injected into `ClassroomRecognitionPipeline` or
`InsightFaceEngine`** — confirmed by reading both classes' full constructor parameter lists.

**Determinations (answered for the general `IStorageProvider.WriteObjectAsync` mechanism, since no
face-crop-specific call exists to analyze):**

- **Can upload fail silently?** Not applicable here — no upload is ever attempted for face crops, so
  there is no failure path to swallow. For the *general* mechanism (as used by
  `AttendancePhotoService`/`MediaStorageService.SaveVariantsAsync`), `S3StorageProvider.WriteObjectAsync`
  (`Abhyanvaya.API/Media/S3StorageProvider.cs:30-60`) wraps `PutObjectAsync` in a `try` block — its
  full exception-handling behavior beyond line 60 was not read in this investigation and is out of
  scope for the current bug, since it is never reached for thumbnails.
- **Can null be returned?** Not applicable — `BuildFaceImageKey` (the only thing actually invoked)
  is a non-nullable `string`-returning pure function; it cannot return null.
- **Can exceptions be swallowed?** Not applicable to the missing upload (nothing is attempted, so
  nothing can throw or be swallowed). Note separately that `ClassroomRecognitionPipeline.ProcessAsync`'s
  own `catch (Exception ex)` block (line 192-205) does **not** swallow — it calls `_diagnostics.Fail(ex)`
  and then `throw;`s (line 204), so if a future fix added an upload call here, a failure would still
  propagate and fail the whole job rather than being silently ignored, unless that future code wrapped
  it in its own try/catch.

---

## Task 7 — API Investigation

**Endpoint:** `GET ~/api/attendance-sessions/{sessionId}/recognitions`
(`Abhyanvaya.API/Controllers/AttendanceRecognitionController.cs:23-32`) →
`IAttendanceRecognitionReviewService.GetRecognitionsForSessionAsync` →
`AttendanceRecognitionReviewService.cs:44-65` → `MapToReviewDtoAsync` (line 480-548).

```
AttendanceRecognition Entity                 AttendanceRecognitionReviewDto                 JSON (camelCase)              Frontend type
FaceImageKey (string?)          ──►  FaceThumbnailUrl = BuildMediaUrl(...)  ──►  "faceThumbnailUrl": "/media/...?v=…"  ──►  faceThumbnailUrl: string | null
(AttendanceRecognition.cs:87)        (AttendanceRecognitionReviewService.cs:497-499, 534)     (ASP.NET default camelCase)   (attendanceRecognitionService.ts:38)
```

**Verify — does the DTO contain `thumbnailUrl`/`thumbnailKey`/`thumbnailImage`/`faceImage`/`previewUrl`?**

| Requested property | Present on `AttendanceRecognitionReviewDto`? | Actual property name |
|---|---|---|
| `thumbnailUrl` | ❌ Not on the *review* DTO (this exact name **is** present on the separate `AttendanceRecognitionDto` used only for mutation responses — `AttendanceRecognitionDto.cs:21`) | `FaceThumbnailUrl` on the review DTO |
| `thumbnailKey` | ❌ Not present anywhere (no DTO exposes the raw storage key, only the pre-built URL) | — |
| `thumbnailImage` | ❌ Not present | — |
| `faceImage` | ❌ Not present | — |
| `previewUrl` | ❌ Not present | — |
| **`faceThumbnailUrl`** | ✅ Present — `AttendanceRecognitionReviewDto.cs:32` | matches exactly |

**No naming mismatch exists between backend and frontend** — `AttendanceRecognitionReviewDto.FaceThumbnailUrl`
(C#, PascalCase) serializes to `faceThumbnailUrl` (ASP.NET Core's default camelCase JSON policy) and the
TypeScript type `AttendanceRecognitionReviewDto.faceThumbnailUrl: string | null`
(`abhyanvaya-ui/src/services/attendanceRecognitionService.ts:38`) consumes that exact key. The API/DTO
layer is not a contributing cause — see Task 11.

---

## Task 8 — React Investigation

**Components:** `abhyanvaya-ui/src/components/attendance-recognition/RecognitionCard.tsx` and
`SelectedFaceDetailsPanel.tsx`.

```
API Response (faceThumbnailUrl: "/media/...webp?v=…" or null)
        │
        ▼
Component prop  recognition: AttendanceRecognitionReviewDto     (RecognitionCard.tsx:12, SelectedFaceDetailsPanel.tsx:23)
        │
        ▼
Local var  faceUrl = mediaAssetUrl(recognition.faceThumbnailUrl)     (RecognitionCard.tsx:30; SelectedFaceDetailsPanel.tsx:55)
        │
        ▼
<Avatar variant="rounded" src={faceUrl ?? undefined}>#{recognition.faceNumber}</Avatar>
        (RecognitionCard.tsx:80-87; SelectedFaceDetailsPanel.tsx:66-68)
```

| | Expected property | Actual property | Fallback behavior | Placeholder behavior |
|---|---|---|---|---|
| **If `faceThumbnailUrl` is `null`/empty** | `mediaAssetUrl` returns `null` (`mediaAssetUrl.ts:5-6`) | `src={undefined}` passed to `Avatar` | MUI `Avatar` renders its `children` when `src` is falsy | Renders `#{faceNumber}` text (`RecognitionCard.tsx:86`) or the face-number `Avatar` fallback in the details panel (`SelectedFaceDetailsPanel.tsx:67`) |
| **If `faceThumbnailUrl` is a non-null URL that 404s** (the actual production case) | `src` is a valid-looking absolute URL, e.g. `https://api.../media/recognitions/1/{sessionId}/faces/00001.webp?v=…` (`mediaAssetUrl.ts:7`) | Browser `<img>` fires `onerror` | **MUI `Avatar` catches the `<img>` error internally and falls back to rendering `children`, exactly the same visual result as the null case above** — this is standard MUI `Avatar` behavior, not custom code in this repo | Same `#{faceNumber}` placeholder — **visually indistinguishable from "no thumbnail was ever generated"**, which is exactly why this bug looks like "no thumbnails" rather than "broken images" to anyone looking at the UI |

**This is the key UI-layer finding:** the React layer has *correct*, intentional fallback behavior for
a missing image — but that same fallback also perfectly masks a 404 caused by the backend never having
uploaded the file, so there is no visual distinction between "recognition succeeded, no thumbnail
feature used" and "recognition succeeded, thumbnail was supposed to exist but the upload was never
implemented." No console error surfaces to a typical user either (MUI does not log the underlying
`<img>` error to the console).

---

## Task 9 — Browser Rendering Investigation

**Does the browser request the URL?** Yes, unconditionally, whenever `faceThumbnailUrl` is non-null —
which it always is, per Task 5/7 (the string is built from `FaceImageKey`, which is always populated).
`mediaAssetUrl` (`abhyanvaya-ui/src/utils/mediaAssetUrl.ts:4-8`) turns the relative `/media/...` path
into an absolute URL against `getApiPublicOrigin()`, and MUI's `Avatar` renders a real `<img src="...">`
that the browser fetches like any other image.

**Expected response:** `404 Not Found`. Reasoning, traced through the serving path:

- `Program.cs:398-403` wires `/media` to `UseStaticFiles` backed by a `PhysicalFileProvider` rooted at
  `ResolveLocalMediaPhysicalRoot(...)` when the `local` storage provider is active — ASP.NET Core's
  static file middleware returns `404` for any path that does not exist under that root, which is
  guaranteed here since `WriteObjectAsync` was never called for this key (Task 3/6).
- If the `s3`/R2 provider is active instead, the exact `/media/{key}` → S3 object mapping was not
  further traced in this investigation (out of scope: this repo's `/media` static-file middleware as
  configured at `Program.cs:398-403` serves from local disk regardless of the active `IStorageProvider`
  — whether an additional S3-backed `/media` handler exists elsewhere was not confirmed either way).
  Regardless of provider, the outcome is the same: **no object was ever written, so no provider can
  return anything but a "not found" response** (`404` for local static files; S3/R2 would return its
  own `NoSuchKey` equivalent, which a proxying handler would typically surface as `404` or `403`
  depending on bucket policy — not independently confirmed here since it is moot without an uploaded
  object).
- `403`/`500` are not expected: there is no auth check on `/media` static files in the configuration
  read (`Program.cs:398-403` registers no `[Authorize]`-equivalent on this middleware), and a missing
  file is a `404` condition, not a server error.

**Rendering path documented (Task 1's diagram, final steps):** browser issues `GET`, receives `404`,
the `<img>` element's `onerror` fires, MUI `Avatar` (Task 8) swallows it and shows the placeholder.

---

## Task 10 — Failure Matrix

| Stage | Working | Broken | Evidence |
|---|---|---|---|
| Detection | ✅ | | `InsightFaceEngine.DetectAsync` returns faces with embeddings, bounding boxes, and `AlignedFaceBytes` populated (`InsightFaceEngine.cs:38-124`) — user's own report confirms detection/matching/confidence/`AttendanceRecognition` creation all work |
| Crop | ✅ | | `AlignFace` produces a valid 112×112 `Image<Rgb24>` (`InsightFaceImageMath.cs:60-88`), immediately consumed by both `ExtractEmbedding` (for the working match) and `SaveAsWebpAsync` (for the thumbnail bytes) — both succeed in the same code path |
| Thumbnail (bytes) | ✅ (bytes are generated) | ⚠️ (bytes are then discarded) | `alignedBytes` is correctly WebP-encoded (`InsightFaceEngine.cs:83-88`) and assigned to `DetectedFaceDto.AlignedFaceBytes` (line 117) — the *generation* works; nothing downstream *uses* it |
| Upload | | ❌ | No call site exists anywhere in the repository that writes `AlignedFaceBytes`/any face-crop bytes to `IStorageProvider`/`IMediaStorageService` — confirmed by exhaustive search (Task 3, Task 6) |
| DB | ✅ (key is stored) | ⚠️ (key names nothing) | `FaceImageKey` is reliably persisted (`ClassroomRecognitionPipeline.cs:152, 176`) — the *write to the database* works; the value it stores is a dangling reference |
| API | ✅ | | `FaceThumbnailUrl` is correctly derived from `FaceImageKey` and serialized with the exact name the frontend expects (Task 7) — no bug in this layer |
| UI | ✅ (renders correctly for what it receives) | | React renders exactly what MUI does with a 404'ing `src` — a placeholder — which is correct, unsurprising component behavior, not a UI bug (Task 8) |

**One-line summary:** Detection → Crop → Thumbnail (bytes) all work. **Upload is completely absent.**
Everything downstream of Upload (DB, API, UI) is functioning correctly *given the broken input it
receives* — they are symptoms, not causes.

---

## Task 11 — Root Cause Ranking

| Rank | Cause | Code reference |
|---|---|---|
| **Most likely** | **Missing upload step in `ClassroomRecognitionPipeline.ProcessAsync`.** The face-crop WebP bytes generated in `InsightFaceEngine.DetectAsync` (`InsightFaceEngine.cs:83-88, 117`) are never written to storage. `ClassroomRecognitionPipeline` has no write-capable media dependency injected (`ClassroomRecognitionPipeline.cs:22, 36, 49` — `IMediaObjectReader` is read-only; contrast with `IMediaStorageService`, which exists in the same layer and is used successfully elsewhere, e.g. `AttendancePhotoService.cs:145`, but was never wired into this pipeline). `FaceImageKey` (`ClassroomRecognitionPipeline.cs:152, 278-279`) is computed as a key string independent of whether any upload occurred, so it always "succeeds" at producing a plausible-looking value. This single gap fully explains the observed symptom with no other contributing factor required. |
| **Likely** | *(No second cause is needed to explain the symptom — nothing else in the traced chain is broken.)* If anything, the closest "likely" secondary observation is that **the design silently masks this class of bug**: `BuildMediaUrl` (`AttendanceSessionMediaPaths.cs:6-15`) never checks object existence, and MUI `Avatar`'s built-in `<img onerror>` fallback (Task 8) means a missing thumbnail produces no error anywhere in the browser console, server logs, or API response — so this is a "silent by design, several layers deep" class of failure, not evidence of a second independent bug. |
| **Possible** | If a *future* change added the missing upload call, the general `IStorageProvider.WriteObjectAsync` implementations reviewed in Task 6 (`S3StorageProvider.cs`, `LocalStorageProvider.cs`) were not fully audited for failure-swallowing behavior beyond their first ~60 lines — it is *possible* (not confirmed either way in this investigation) that a naive fix which fires-and-forgets the upload without awaiting/propagating exceptions could reintroduce a silent-failure mode. This is not a cause of the *current* bug — flagged only as a risk to consider if/when a fix is designed. |
| **Unlikely** | A frontend/DTO naming mismatch. Ruled out directly by Task 7 — `FaceThumbnailUrl` (C#) ↔ `faceThumbnailUrl` (JSON) ↔ `faceThumbnailUrl` (TypeScript) match exactly at every hop, with no typo or casing divergence found. |
| **Unlikely** | A `/media` static-file routing/auth misconfiguration. `Program.cs:398-403` shows a plain, unauthenticated `UseStaticFiles` mapping with no restriction that would turn an *existing* file into a 403/500 — and moot regardless, since no file exists to be blocked (Task 9). |

---

## Verification

- **No code modified.** This investigation added exactly one file: `docs/AI18_REVIEW1_THUMBNAIL_PIPELINE_AUDIT.md`.
- **No database changes.** No migrations, no data modifications.
- **No API changes.** No controller, DTO, or route modified.
- **No UI changes.** No `.tsx`/`.ts` file modified.
- **No S3 changes.** No storage configuration or provider code modified.
- **No configuration changes.** No `appsettings.json`/`.env`/`render.yaml` modified.
- **Build verification:** performed per the deliverable's explicit instruction, even though this
  milestone is read-only and no source file was changed (so a clean build was already guaranteed).

  ```
  dotnet build Abhyanvaya.sln
  Build succeeded.
      0 Error(s)
  ```
