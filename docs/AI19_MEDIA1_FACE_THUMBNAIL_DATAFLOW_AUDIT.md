# AI19.MEDIA.1 — FaceThumbnailUrl End-to-End Data Flow Audit

**Type:** Production forensic investigation (READ-ONLY — no code, DTO, React, DB, or storage changes made)
**Scope:** Trace `FaceThumbnailUrl` from `AlignedFaceBytes` to `Avatar.src` and determine exactly where/why it fails to render in Production while working locally.

---

## Executive Summary

**The value is not null anywhere in the current codebase.** Every stage of the pipeline on the currently checked‑out code (`AI19.MEDIA.1…` branch, descended from the merged `AI18.REVIEW` work) correctly produces, persists, loads, maps, serializes, and renders a non‑null `faceThumbnailUrl`. That code has been verified end‑to‑end (AI18.REVIEW.1–3) and is what runs when you `dotnet run` locally.

**Render production does not run this code.** `render.yaml` pins the deployed API to `branch: AI-attandance-feature`. That branch was diffed directly (`git show origin/AI-attandance-feature:...`) and:

- Does **not** contain `RecognitionMediaService.cs` or `IRecognitionMediaService.cs` at all (git reports `fatal: path … exists on disk, but not in 'origin/AI-attandance-feature'`).
- Its `ClassroomRecognitionPipeline.cs` (line 107) assigns `FaceImageKey = BuildFaceImageKey(session, face.FaceIndex)` — a **pure string formatter** (lines 176‑177) with **zero calls** to any upload/persistence API, and **zero references** to `face.AlignedFaceBytes` anywhere in the file.
- `InsightFaceEngine.cs` on that same branch **does** generate `AlignedFaceBytes` (confirmed: `AlignedFaceBytes = alignedBytes` after `SaveAsWebpAsync`), but the pipeline never reads them — they are generated and discarded, byte for byte the same defect originally diagnosed in `docs/AI18_REVIEW1_THUMBNAIL_PIPELINE_AUDIT.md`.

So in production, `FaceImageKey` (and therefore `faceThumbnailUrl`) **is a real, syntactically valid, non‑null string** — it just names a WebP object that was never uploaded anywhere. The browser's `GET /media/recognitions/.../00004.webp` request 404s.

**This is why the DOM looks like `Avatar.src == null` even though it isn't.** MUI Avatar's `useLoaded()` hook (`node_modules/@mui/material/Avatar/Avatar.js:111‑176`, MUI v9.0.0 per `abhyanvaya-ui/package.json`) sets `loaded = 'error'` on the image's `onerror` event, and:

```js
const hasImg = src || srcSet;
const hasImgNotFailing = hasImg && loaded !== 'error';
ownerState.colorDefault = !hasImgNotFailing;
```

`hasImgNotFailing` is `false` for **both** "`src` is falsy" **and** "`src` is truthy but the image 404s/403s." In both cases `colorDefault = true`, no `<img>` slot is rendered, and the fallback `children` (e.g. `#4`) is rendered inside a `<div class="MuiAvatar-root MuiAvatar-colorDefault">`. A null `src` and a 404'ing `src` are **DOM‑indistinguishable** — the browser inspection in the problem statement cannot, by itself, prove `Avatar.src == null`.

**Root cause:** Production is running a stale, pre‑fix build of `Abhyanvaya.API` (deployed from `AI-attandance-feature`) that predates the `RecognitionMediaService` thumbnail‑upload implementation. See Stage 13 for the single, unambiguous classification.

---

## Architecture Diagram — Intended (current/local) Data Flow

```
┌──────────────────────────┐
│ InsightFaceEngine         │  produces DetectedFaceDto.AlignedFaceBytes (WebP bytes)
└─────────────┬─────────────┘
              │
┌─────────────▼─────────────────────────────┐
│ ClassroomRecognitionPipeline.ProcessAsync   │  foreach face:
│   await _recognitionMediaService            │    line 180
│     .PersistFaceThumbnailAsync(...)         │
└─────────────┬───────────────────────────────┘
              │ returns storageKey (throws on failure — never null/empty)
┌─────────────▼─────────────────────────────┐
│ RecognitionMediaService                     │  writes bytes via IMediaStorageService
│   .PersistFaceThumbnailAsync                │  → ApplicationMediaStorageService
└─────────────┬─────────────────────────────┘   → IStorageProvider.WriteObjectAsync
              │ key: recognitions/{tenantId}/{sessionId}/faces/{NNNNN}.webp
┌─────────────▼─────────────────────────────┐
│ AttendanceRecognition.FaceImageKey (DB)     │  line 199: FaceImageKey = faceImageKey
└─────────────┬─────────────────────────────┘
              │ EF Core: nullable varchar(500), no Ignore(), no HasColumnName override
┌─────────────▼─────────────────────────────┐
│ AttendanceRecognitionReviewService          │  GetRecognitionsForSessionAsync()
│   .GetRecognitionsForSessionAsync           │  → MapToReviewDtoAsync()
└─────────────┬─────────────────────────────┘
              │ AttendanceSessionMediaPaths.BuildMediaUrl(FaceImageKey, CreatedUtc)
┌─────────────▼─────────────────────────────┐
│ AttendanceRecognitionReviewDto              │  FaceThumbnailUrl = "/media/{key}?v={unix}"
└─────────────┬─────────────────────────────┘
              │ System.Text.Json (default camelCase, nulls included)
┌─────────────▼─────────────────────────────┐
│ GET /api/attendance-sessions/{id}/recognitions │  JSON: { "faceThumbnailUrl": "/media/..." }
└─────────────┬─────────────────────────────┘
              │ fetch/axios → AttendanceRecognitionReviewDto[] (TS type)
┌─────────────▼─────────────────────────────┐
│ RecognitionCard.tsx / SelectedFaceDetailsPanel.tsx │
│   const faceUrl = mediaAssetUrl(recognition.faceThumbnailUrl) │
│   <Avatar src={faceUrl ?? undefined}>       │
└─────────────┬─────────────────────────────┘
              │ mediaAssetUrl() prefixes VITE_API_BASE_URL origin
┌─────────────▼─────────────────────────────┐
│ Browser: GET {apiOrigin}/media/{key}?v=…    │
│ Served by UseStaticFiles (local disk only)  │
└──────────────────────────────────────────────┘
```

## Sequence Diagram — Production (`AI-attandance-feature` branch, as deployed)

```
InsightFaceEngine            ClassroomRecognitionPipeline        AttendanceRecognitionReviewService   React/Avatar        Browser
      │  AlignedFaceBytes            │                                     │                              │                │
      │────────generated─────────────▶ (never read — no field/variable     │                              │                │
      │                               receives it; grep confirms 0 refs)   │                              │                │
      │                               │                                     │                              │                │
      │                               │ FaceImageKey =                     │                              │                │
      │                               │   BuildFaceImageKey(session,face)  │                              │                │
      │                               │   (pure string, no I/O)            │                              │                │
      │                               │───SaveChanges──▶ DB: FaceImageKey  │                              │                │
      │                               │                  = "recognitions/1/│                              │                │
      │                               │                  {sid}/faces/00004 │                              │                │
      │                               │                  .webp" (non-null) │                              │                │
      │                               │                                     │──BuildMediaUrl(key)─────────▶│                │
      │                               │                                     │  "/media/recognitions/…webp" │                │
      │                               │                                     │  (non-null, well-formed)     │                │
      │                               │                                     │──JSON faceThumbnailUrl───────▶│                │
      │                               │                                     │                              │──src=url───────▶│
      │                               │                                     │                              │                │ GET /media/…
      │                               │                                     │                              │                │ 404 (never
      │                               │                                     │                              │                │ uploaded)
      │                               │                                     │                              │◀─onerror────────│
      │                               │                                     │                              │ loaded='error'  │
      │                               │                                     │                              │ colorDefault=true│
      │                               │                                     │                              │ no <img> rendered│
```

---

## Stage 1 — Complete Object Flow (as designed, current code)

```
AlignedFaceBytes (byte[]?, WebP)
   ↓  ClassroomRecognitionPipeline.cs:180
RecognitionMediaService.PersistFaceThumbnailAsync(tenantId, sessionId, faceNumber, bytes, traceId)
   ↓  returns string storageKey  (or throws — never null/empty)
ClassroomRecognitionPipeline.cs:199  →  AttendanceRecognition.FaceImageKey = faceImageKey
   ↓  ConcurrencyExceptionHelper.SaveChangesAsync (EF Core, Npgsql)
Database column AttendanceRecognition.FaceImageKey (varchar(500), nullable)
   ↓  AttendanceRecognitionReviewService.GetRecognitionsForSessionAsync (line 50-56, AsNoTracking + Include(Student))
AttendanceRecognitionReviewService.MapToReviewDtoAsync (line 497-498)
   ↓  AttendanceSessionMediaPaths.BuildMediaUrl(FaceImageKey, CreatedUtc)
string? faceThumbnailUrl = "/media/{key}?v={unixSeconds}"  (or null iff key blank)
   ↓  AttendanceRecognitionReviewDto.FaceThumbnailUrl (line 534)
   ↓  System.Text.Json serialization (ASP.NET Core default: camelCase, nulls included)
JSON: { "faceThumbnailUrl": "/media/recognitions/1/{sid}/faces/00004.webp?v=..." }
   ↓  abhyanvaya-ui/src/services/attendanceRecognitionService.ts (typed fetch)
recognition.faceThumbnailUrl : string | null
   ↓  RecognitionCard.tsx:30 / SelectedFaceDetailsPanel.tsx  →  mediaAssetUrl(recognition.faceThumbnailUrl)
faceUrl : string | null  (absolute: `${apiOrigin}${relativePath}`)
   ↓  <Avatar src={faceUrl ?? undefined}>
Avatar.src
   ↓  browser GET {apiOrigin}/media/{key}?v=...
   ↓  UseStaticFiles middleware, PhysicalFileProvider(mediaPhysical), RequestPath "/media"
HTTP 200 (file exists) → <img> rendered   |   HTTP 404 (file missing) → fallback rendered, no <img>
```

---

## Stage 2 — Per-Stage Input/Output/Nullability

| # | Stage | Input | Output | Can become null? | Where | Why |
|---|-------|-------|--------|-------------------|-------|-----|
| 1 | `InsightFaceEngine` detect/align | classroom image bytes | `DetectedFaceDto.AlignedFaceBytes` | Yes, in principle | `InsightFaceEngine.cs` (crop/encode step) | Alignment/encoding could fail for a malformed crop; not observed in current diagnostics logs (AI17/AI18) |
| 2 | `RecognitionMediaService.PersistFaceThumbnailAsync` | `AlignedFaceBytes` | `string` storage key | **No** — throws `DomainException` instead | `RecognitionMediaService.cs:58-65` (null/empty bytes) and `:103-114` (upload exception) | Deliberate "never return null" contract (AI18.REVIEW.2 requirement) |
| 3 | `ClassroomRecognitionPipeline` assignment | returned key | `AttendanceRecognition.FaceImageKey` | **No**, on the *current* code — assignment happens only after `PersistFaceThumbnailAsync` returns successfully (`ClassroomRecognitionPipeline.cs:180-199`); any earlier throw skips the `recognitions.Add(...)` for that face entirely and fails the whole session (`catch` block, session → Failed) | `ClassroomRecognitionPipeline.cs:199` | Exceptions propagate out of the `foreach`, caught by the outer `try/catch`, session marked Failed — no dangling row is ever created |
| 4 | EF Core persistence | in-memory entity | DB row | Yes, only if `FaceImageKey` was already null coming in (case 3 doesn't happen on current code) | `AttendanceRecognitionConfiguration.cs:43-45` — nullable `varchar(500)`, no `.IsRequired()`, no `.Ignore()`, no `.HasColumnName()` override | Column is intentionally nullable (legacy rows / non-recognition-related failures) |
| 5 | Review query load | `Guid sessionId` | `List<AttendanceRecognition>` | N/A (query itself can't null a scalar column) | `AttendanceRecognitionReviewService.cs:50-56` | `.AsNoTracking().Include(r => r.Student)` — `FaceImageKey` is a scalar column on the base table, always included, never excluded from the `SELECT` (EF Core materializes every mapped scalar property by default) |
| 6 | DTO mapping | `recognition.FaceImageKey`, `recognition.CreatedUtc` | `AttendanceRecognitionReviewDto.FaceThumbnailUrl` | **Yes** | `AttendanceRecognitionReviewService.cs:497-498, 534` → `AttendanceSessionMediaPaths.BuildMediaUrl` (`AttendanceSessionMediaPaths.cs:6-14`) | Returns `null` **only if** `string.IsNullOrWhiteSpace(relativeKey)` — i.e. only if `FaceImageKey` itself was null/blank |
| 7 | API JSON response | DTO | JSON property `faceThumbnailUrl` | Only if DTO field is null (then serialized as `"faceThumbnailUrl": null`, not omitted) | `Program.cs` — no `AddJsonOptions`/`DefaultIgnoreCondition` found anywhere in `Abhyanvaya.API` | Default `System.Text.Json` behavior includes null properties |
| 8 | React fetch/type | JSON | `recognition.faceThumbnailUrl: string \| null` | Mirrors JSON | `attendanceRecognitionService.ts:26-39` | TypeScript type declares `string \| null`, matches API |
| 9 | `mediaAssetUrl()` | `string \| null` | `string \| null` (absolute URL) | Passes null straight through | `mediaAssetUrl.ts:4-8` — `if (!path) return null;` | Guard clause, no transformation of a non-null value into null |
| 10 | `<Avatar src={faceUrl ?? undefined}>` | `string \| null` | DOM `src` attribute (string or absent) | `?? undefined` converts `null`→`undefined`, but this is a JS/DOM-prop nuance, not a new null | `RecognitionCard.tsx:80-87`, `SelectedFaceDetailsPanel.tsx` | Standard null-coalescing for optional JSX props |
| 11 | Browser image load | non-null `src` URL | `<img>` (success) or fallback children (404/err) | **The visual symptom, but not a null *value*** | `Avatar.js:111-176` (`useLoaded`) | `loaded === 'error'` forces `hasImgNotFailing = false`, identical DOM to a null `src` |

**Conclusion of Stage 2:** on the *current* codebase, the only place the *string value* can legitimately become null is stage 6 (`BuildMediaUrl`), and only as a direct, correct consequence of `FaceImageKey` itself being null/blank in the database — which the current pipeline (stage 3) never produces for a session that completes successfully.

---

## Stage 3 — `RecognitionMediaService` Audit

File: `Abhyanvaya.Infrastructure/Recognition/RecognitionMediaService.cs`

- **Does it always return a key?** No — it returns a key **only on a verified-successful upload** (line 80: `return storageKey;`, reached only after `await _mediaStorage.SaveOriginalObjectAsync(...)` completes without throwing).
- **Can it return null or empty string?** No. The method signature is `Task<string>` (non-nullable). There is no code path that returns `null` or `""`.
- **If upload fails, does it throw or return null?** It **throws**:
  - Missing/empty bytes → `DomainException` (`RecognitionMediaService.cs:58-65`).
  - `SaveOriginalObjectAsync` throws (storage failure) → caught, logged as `"Recognition Thumbnail Upload Failed"`, re-thrown wrapped in `DomainException` (`:103-114`).
  - `OperationCanceledException` → re-thrown untouched (`:96-101`).
- **Is the returned key ever ignored?** No. `ClassroomRecognitionPipeline.cs:180-199` captures the return value into `faceImageKey` and assigns it directly to `FaceImageKey` two statements later — no discard, no ignored `Task`.

**Finding:** on the current code, this service cannot be the source of a null `FaceImageKey`. **On the deployed `AI-attandance-feature` branch, this service does not exist at all** — see Stage 13.

---

## Stage 4 — `ClassroomRecognitionPipeline` Audit

File: `Abhyanvaya.Infrastructure/Recognition/ClassroomRecognitionPipeline.cs`

```163:210:Abhyanvaya.Infrastructure/Recognition/ClassroomRecognitionPipeline.cs
            _forensics.Checkpoint("Before Thumbnail Persistence");
            _memoryAudit.Snapshot("Before Thumbnail Persistence");
            var recognitions = new List<AttendanceRecognition>();
            foreach (var face in detection.Faces)
            {
                var match = matches.First(m => m.FaceIndex == face.FaceIndex);

                _memoryAudit.Snapshot("Before Thumbnail Persistence", face.FaceIndex);
                var thumbnailBytesId = face.AlignedFaceBytes is { Length: > 0 }
                    ? _memoryAudit.RegisterObject("Byte Array", face.AlignedFaceBytes.Length, "Thumbnail Persistence", face.FaceIndex)
                    : -1;

                var faceImageKey = await _recognitionMediaService.PersistFaceThumbnailAsync(
                    session.TenantId,
                    session.Id,
                    face.FaceIndex,
                    face.AlignedFaceBytes,
                    _executionContext.ExecutionTraceId,
                    cancellationToken);

                _memoryAudit.DisposeObject(thumbnailBytesId);
                _memoryAudit.Snapshot("After Thumbnail Persistence", face.FaceIndex);

                recognitions.Add(new AttendanceRecognition
                {
                    Id = Guid.NewGuid(),
                    ...
                    FaceImageKey = faceImageKey,
                    ...
                });
            }
```

- **Is `FaceImageKey` ALWAYS assigned?** Conditionally: it is assigned **for every face that reaches line 199**, and a face only reaches line 199 after `PersistFaceThumbnailAsync` (line 180) has returned *successfully* (line 180-186 is `await`ed **before** the `AttendanceRecognition` object literal is constructed). If the await throws, control never reaches line 199 for that face, the exception propagates out of the `foreach` and out of `ProcessAsync`'s `try` block, and the outer `catch (Exception ex)` (further down in the file) marks the whole session `Failed` — no `AttendanceRecognition` row is added for *any* face in that session.
- **Exact file/line:** `Abhyanvaya.Infrastructure/Recognition/ClassroomRecognitionPipeline.cs`, line 180 (call) → line 199 (assignment).

**Finding:** on the current code, a session that reaches `AwaitingReview`/`Completed` status can only do so with every one of its `AttendanceRecognition` rows carrying a non-null `FaceImageKey`. A null `FaceImageKey` on a *successfully completed* session is not reachable through this code path.

---

## Stage 5 — Database Persistence Audit

Entity: `Abhyanvaya.Domain/Entities/AttendanceRecognition.cs:87`

```csharp
public string? FaceImageKey { get; set; }
```

EF configuration: `Abhyanvaya.Infrastructure/Persistence/Configurations/AttendanceRecognitionConfiguration.cs:43-45`

```csharp
builder.Property(x => x.FaceImageKey)
    .HasMaxLength(500)
    .HasColumnType("character varying(500)");
```

- **Mapped?** Yes — standard scalar property, mapped to column `FaceImageKey`.
- **Nullable?** Yes, both in the CLR type (`string?`) and in the EF configuration (no `.IsRequired()`).
- **Ignored?** No `.Ignore(x => x.FaceImageKey)` anywhere in the configuration.
- **Overwritten?** No other write site exists for this property outside `ClassroomRecognitionPipeline.cs:199` (confirmed via project-wide search for `FaceImageKey =`).
- **Not tracked / excluded from EF?** No — the query at `AttendanceRecognitionReviewService.cs:50-56` uses `.AsNoTracking()` for *read* performance only; this does not exclude any column from the `SELECT` projection (EF Core with `AsNoTracking()` still materializes every mapped scalar property, it just skips change-tracking bookkeeping).

**Finding:** the database mapping is correct and complete; `FaceImageKey` is faithfully round-tripped.

---

## Stage 6 — Review Query Audit

`Abhyanvaya.Application/AttendanceRecognitionReviewService.cs:50-56`

```csharp
var recognitions = await _context.AttendanceRecognitions
    .AsNoTracking()
    .Include(r => r.Student)
    .Where(r => r.AttendanceSessionId == attendanceSessionId)
    .OrderByDescending(r => r.ConfidenceScore ?? -1m)
    .ThenBy(r => r.FaceNumber)
    .ToListAsync(cancellationToken);
```

- **Is `FaceImageKey` selected?** Yes, implicitly — this is a `DbSet<AttendanceRecognition>` query with **no `.Select()` projection**, so EF Core issues `SELECT` for every mapped column of `AttendanceRecognition`, including `FaceImageKey`. There is no partial projection anywhere in this query that could "forget" the column.

**Finding:** not the source of the null.

---

## Stage 7 — DTO Mapping Audit

`Abhyanvaya.Application/AttendanceRecognitionReviewService.cs:480-536` (`MapToReviewDtoAsync`)

```csharp
var faceThumbnailUrl = AttendanceSessionMediaPaths.BuildMediaUrl(
    recognition.FaceImageKey,
    recognition.CreatedUtc);
...
return new AttendanceRecognitionReviewDto
{
    ...
    FaceThumbnailUrl = faceThumbnailUrl,
    StudentPhotoUrl = studentPhotoUrl,
    ...
};
```

- **Where is `FaceThumbnailUrl` created?** Line 497-498 (computed) → line 534 (assigned into the DTO). This mapping exists and is present — **it is not missing**, so this is **not** the root cause (ruling out the "DTO mapping omitted" example explicitly).

One separate, narrower quirk noted for completeness (does **not** apply to the initial list load used by the Recognition Review screen, only to the single-row mapper used after a teacher review *action*):

```567:567:Abhyanvaya.Application/AttendanceRecognitionReviewService.cs
            ThumbnailUrl = reviewDto.FaceThumbnailUrl ?? reviewDto.StudentPhotoUrl,
```

This substitutes the student's enrollment photo for the face thumbnail *only inside the mutation-response DTO* (`AttendanceRecognitionDto.ThumbnailUrl`, used by approve/reject/override actions), not inside `AttendanceRecognitionReviewDto.FaceThumbnailUrl` used by the initial page load. It cannot explain thumbnails being missing on first page load.

**Finding:** DTO mapping is present, correct, and not the root cause.

---

## Stage 8 — Media URL Builder Audit

`Abhyanvaya.Application/AttendanceSessionMediaPaths.cs:6-14`

```csharp
public static string? BuildMediaUrl(string? relativeKey, DateTime cacheUtc)
{
    if (string.IsNullOrWhiteSpace(relativeKey))
    {
        return null;
    }

    var v = new DateTimeOffset(DateTime.SpecifyKind(cacheUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
    return $"/media/{relativeKey.Trim('/')}?v={v}";
}
```

- **Can it return null?** Yes, **exactly one condition**: `relativeKey` is `null`, `""`, or whitespace-only.
- Pure string function — no I/O, no existence check against storage, no environment-conditional branching. Behaves identically in local and production processes.

**Finding:** this function is deterministic and correct; it is not itself capable of behaving differently between environments given the same `FaceImageKey` input.

---

## Stage 9 — API Response Audit

`Abhyanvaya.API/Controllers/AttendanceRecognitionController.cs:23-30`

```csharp
[HttpGet("~/api/attendance-sessions/{sessionId:guid}/recognitions")]
public async Task<ActionResult<IReadOnlyList<AttendanceRecognitionReviewDto>>> GetRecognitionsForSession(
    Guid sessionId,
    CancellationToken cancellationToken)
{
    var results = await _reviewService.GetRecognitionsForSessionAsync(sessionId, cancellationToken);
    return Ok(results);
}
```

- **Exact route:** `GET /api/attendance-sessions/{sessionId}/recognitions`.
- **Does the API serialize `faceThumbnailUrl`?** Yes. `Program.cs` was searched project-wide for `AddJsonOptions`, `JsonSerializerOptions`, `PropertyNamingPolicy`, `DefaultIgnoreCondition` — **none exist**. ASP.NET Core's default `System.Text.Json` output formatter is used unmodified: camelCase property names, and — critically — **null values are serialized, not omitted** (the default `JsonIgnoreCondition` is `Never`). So even a genuinely-null `FaceThumbnailUrl` would still appear in the JSON as `"faceThumbnailUrl": null`, not be dropped from the payload.

**Finding:** the API layer faithfully passes through whatever the DTO contains; it is not a source of null-ification, nor does it hide/omit the property.

---

## Stage 10 — React Audit

Files: `RecognitionCard.tsx`, `SelectedFaceDetailsPanel.tsx` (there is no `RecognitionReview.tsx`/`RecognitionPanel.tsx` in this codebase by that exact name — the review screen is composed from `RecognitionReviewPage`/`RecognitionReviewPanel`-style components that consume `attendanceRecognitionService.getSessionRecognitions`, which return the typed DTO array directly without remapping URLs).

```26:39:abhyanvaya-ui/src/services/attendanceRecognitionService.ts
export type AttendanceRecognitionReviewDto = {
  ...
  faceThumbnailUrl: string | null;
  studentPhotoUrl: string | null;
  ...
};
```

```1:31:abhyanvaya-ui/src/components/attendance-recognition/RecognitionCard.tsx
import { Avatar, Box, Card, CardActionArea, Checkbox, Chip, Stack, Typography } from "@mui/material";
...
import { mediaAssetUrl } from "../../utils/mediaAssetUrl";
...
  const faceUrl = mediaAssetUrl(recognition.faceThumbnailUrl);
```

```1:8:abhyanvaya-ui/src/utils/mediaAssetUrl.ts
export function mediaAssetUrl(path: string | null | undefined): string | null {
  if (!path) return null;
  if (/^https?:\/\//i.test(path)) return path;
  return `${getApiPublicOrigin()}${path.startsWith("/") ? path : `/${path}`}`;
}
```

```80:87:abhyanvaya-ui/src/components/attendance-recognition/RecognitionCard.tsx
            <Avatar
              variant="rounded"
              src={faceUrl ?? undefined}
              alt=""
              sx={{ width: 56, height: 56, bgcolor: "grey.300" }}
            >
              #{recognition.faceNumber}
            </Avatar>
```

- **Under what conditions does Avatar receive `undefined`/null `src`?** Only when `recognition.faceThumbnailUrl` itself is `null` (or empty/whitespace, caught by `mediaAssetUrl`'s `if (!path) return null;`). No `onError` handler exists anywhere on these `Avatar`/`img` elements that would clear a previously-valid `src` after a failed load — React never mutates the prop; the *visual* fallback on a 404 is produced entirely inside MUI's own `Avatar` component (see Executive Summary / Avatar.js), not by any code in this repository.

**Finding:** React faithfully renders whatever `faceThumbnailUrl` the API sent. It does not introduce, hide, or clear nulls.

---

## Stage 11 — Local vs Production Comparison (evidence-based, no guessing)

| Evidence | Local | Production |
|---|---|---|
| Git branch actually running | Current working branch (`AI19.MEDIA.1…`, descends from merged `AI18.REVIEW`) — verified via `git log --oneline`: `c5654b4 Merge pull request #57 from krupeshreddyhot/AI18.REVIEW`, `852f80c AI18.MEMORY.1` | `origin/AI-attandance-feature`, per `render.yaml:14` (`branch: AI-attandance-feature`) — this is the Render Blueprint's own declared deploy source |
| `RecognitionMediaService.cs` present? | **Yes** (`Abhyanvaya.Infrastructure/Recognition/RecognitionMediaService.cs`, full file on disk) | **No** — `git show origin/AI-attandance-feature:Abhyanvaya.Infrastructure/Recognition/RecognitionMediaService.cs` → `fatal: path … exists on disk, but not in 'origin/AI-attandance-feature'` |
| `ClassroomRecognitionPipeline.cs` — `FaceImageKey` assignment | Line 199: `FaceImageKey = faceImageKey` — the value **returned by a completed upload** (line 180) | Line 107 (on `AI-attandance-feature`): `FaceImageKey = BuildFaceImageKey(session, face.FaceIndex)` — a **pure string formatter** (lines 176-177), no I/O, no upload call anywhere in the file |
| `AlignedFaceBytes` referenced in the pipeline? | Yes — `face.AlignedFaceBytes` is passed into `PersistFaceThumbnailAsync` (line 185) | **No** — confirmed by `Select-String -Pattern "AlignedFaceBytes"` against the `origin/AI-attandance-feature` copy of the file: zero matches |
| Object actually uploaded to storage for that key? | Yes, guaranteed — `PersistFaceThumbnailAsync` only returns after `SaveOriginalObjectAsync` succeeds | **No** — nothing in that branch's code path ever calls `IMediaStorageService`/`IStorageProvider` for recognition thumbnails |
| `git diff origin/AI-attandance-feature HEAD --stat` | — | 64 files changed, 10,166 insertions — includes every AI13–AI18 doc and code change, none of which reached this branch |
| Resulting `faceThumbnailUrl` in JSON | Non-null, and the file it points to exists → HTTP 200 → `<img>` renders | Non-null (string is well-formed and passes `IsNullOrWhiteSpace`), **but the file was never written** → HTTP 404 → MUI `Avatar` hides `<img>`, renders fallback |
| Resulting DOM | `<img class="MuiAvatar-img" src="https://localhost:7063/media/recognitions/.../00004.webp">` | `<div class="MuiAvatar-root MuiAvatar-colorDefault">…</div>` — **exactly reproduced** by MUI's `useLoaded()`/`hasImgNotFailing` logic (`Avatar.js:174-176, 214-228`) on a 404, independent of whether `src` was null |

**Conclusion:** the two environments are not running the same code. The DOM difference is real and is caused by the network request behind a non-null URL failing (404), not by the URL itself being absent from the JSON payload. This is the one concrete, falsifiable claim of this audit and it is fully supported by `git show`/`git diff` output quoted above, plus the MUI source confirming a 404 and a null `src` are visually identical.

---

## Stage 12 — Null Propagation Table

| Stage | Value (production, deployed `AI-attandance-feature` code) | Null? | Evidence |
|---|---|---|---|
| `RecognitionMediaService` | *Does not exist on this branch* | N/A | `git show origin/AI-attandance-feature:...RecognitionMediaService.cs` → path not found |
| Returned key | `"recognitions/{tenantId}/{sessionId}/faces/{faceNumber:D5}.webp"` from `BuildFaceImageKey` (string formatter, always non-null for a well-formed session/face) | **No** | `ClassroomRecognitionPipeline.cs` (old branch) lines 176-177 |
| `FaceImageKey` (EF entity) | Same non-null string, written by `SaveChangesAsync` | **No** | old branch line 107; `AttendanceRecognitionConfiguration.cs:43-45` (nullable column, no rejection of this value) |
| DTO (`AttendanceRecognitionReviewDto.FaceThumbnailUrl`) | `"/media/recognitions/{tenantId}/{sessionId}/faces/{faceNumber:D5}.webp?v={unix}"` | **No** | `AttendanceSessionMediaPaths.BuildMediaUrl` only nulls on blank input (`AttendanceSessionMediaPaths.cs:8-11`); input here is non-blank |
| API JSON | `"faceThumbnailUrl": "/media/recognitions/…webp?v=…"` | **No** | No JSON ignore-null config in `Program.cs`; DTO field is non-null |
| React (`recognition.faceThumbnailUrl`) | Same non-null string | **No** | Typed pass-through, `attendanceRecognitionService.ts:26-39` |
| `mediaAssetUrl(recognition.faceThumbnailUrl)` | Absolute URL, e.g. `https://abhyanvaya-api.onrender.com/media/recognitions/…webp?v=…` | **No** | `mediaAssetUrl.ts:4-8` only returns null on falsy input |
| `Avatar.src` | The absolute URL above | **No** (but visually equivalent to null) | `RecognitionCard.tsx:82` (`src={faceUrl ?? undefined}`) |
| Underlying HTTP resource | 404 Not Found — object never uploaded | *(resource missing, not the URL string)* | No `IMediaStorageService`/`IStorageProvider` call exists anywhere in the recognition pipeline on the deployed branch |
| Rendered DOM | `<div class="MuiAvatar-root MuiAvatar-colorDefault">` (fallback), no `<img>` | *(visually "null-like")* | `Avatar.js:126-130` (`image.onerror` → `loaded='error'`), `:174-176` (`colorDefault = !hasImgNotFailing`), `:214-228` (fallback branch) |

---

## Stage 13 — Root Cause Classification

### ROOT CAUSE (one, and only one):

> **Production deployment/branch mismatch: Render's API service is deployed from `AI-attandance-feature` (per `render.yaml:14`), a branch that predates and never received the `RecognitionMediaService` thumbnail-persistence implementation (AI18.REVIEW.1/AI18.REVIEW.2). On that branch, `ClassroomRecognitionPipeline.cs:107` assigns `AttendanceRecognition.FaceImageKey` from a pure string formatter (`BuildFaceImageKey`) that is never backed by an actual upload of `DetectedFaceDto.AlignedFaceBytes`. This produces a syntactically valid, non-null `FaceImageKey` → non-null `FaceThumbnailUrl` → non-null JSON property → non-null `Avatar.src`, whose underlying object was never written to storage, so the browser's image request 404s. MUI Avatar's `useLoaded()` hook (`Avatar.js:111-176`) renders the identical "no `<img>`, `colorDefault` fallback" markup for a 404 as it does for a genuinely null `src`, which is why the browser inspection appears to show `Avatar.src == null` when in fact the value React received was a well-formed, non-null URL pointing at a file that does not exist.**

This is not "FaceImageKey never assigned," "DTO mapping omitted," "MediaUrlBuilder returns null," "API doesn't serialize the property," or "React ignores the property" — every one of those five hypotheses was checked against the code (Stages 3-10) and each is disproved by direct evidence on the *current* codebase. The single defect is that **production is not running the current codebase at all.**

---

## Supporting Evidence Index

| Claim | File | Line(s) |
|---|---|---|
| Render deploys from `AI-attandance-feature` | `render.yaml` | 14 |
| `RecognitionMediaService.cs` missing on deployed branch | `git show origin/AI-attandance-feature:Abhyanvaya.Infrastructure/Recognition/RecognitionMediaService.cs` | — (fatal: path not found) |
| Deployed branch's dangling `FaceImageKey` assignment | `ClassroomRecognitionPipeline.cs` (on `origin/AI-attandance-feature`) | 107, 176-177 |
| Deployed branch generates but discards `AlignedFaceBytes` | `InsightFaceEngine.cs` (on `origin/AI-attandance-feature`) | confirmed via `Select-String -Pattern "AlignedFaceBytes"` → 2 matches (assignment only, never read downstream) |
| Current (fixed) code's persistence call | `ClassroomRecognitionPipeline.cs` (current HEAD) | 180 (call), 199 (assignment) |
| `RecognitionMediaService` never returns null/empty | `RecognitionMediaService.cs` | 58-65, 68-80, 103-114 |
| `FaceImageKey` nullable, unmapped-restriction-free | `AttendanceRecognition.cs` / `AttendanceRecognitionConfiguration.cs` | 87 / 43-45 |
| Review query includes all scalar columns | `AttendanceRecognitionReviewService.cs` | 50-56 |
| DTO mapping present | `AttendanceRecognitionReviewService.cs` | 480-498, 534 |
| `BuildMediaUrl` null condition | `AttendanceSessionMediaPaths.cs` | 6-14 |
| API route + no JSON-ignore config | `AttendanceRecognitionController.cs` / `Program.cs` | 23-30 / (absence confirmed project-wide) |
| React consumption, no `onError` clearing | `RecognitionCard.tsx`, `mediaAssetUrl.ts`, `attendanceRecognitionService.ts` | 30, 80-87 / 1-8 / 26-39 |
| MUI Avatar treats 404 like null `src` | `node_modules/@mui/material/Avatar/Avatar.js` | 111-176, 214-228 |
| Diff size between deployed branch and current HEAD | `git diff origin/AI-attandance-feature HEAD --stat` | 64 files, +10166/-114 |

---

## Verification

`dotnet build` was run against the current solution. No source files were modified as part of this investigation (only `git show`/`git diff` read-only commands against remote refs, and this new documentation file were used/created).

Result: **0 errors** (see build log below).

---

## Final Report

# ROOT CAUSE CONFIRMED
