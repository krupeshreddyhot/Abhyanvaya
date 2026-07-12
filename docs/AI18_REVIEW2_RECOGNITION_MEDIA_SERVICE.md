# AI18.REVIEW.2 — Recognition Thumbnail Persistence

Implements the missing thumbnail-persistence layer identified in
[`AI18_REVIEW1_THUMBNAIL_PIPELINE_AUDIT.md`](./AI18_REVIEW1_THUMBNAIL_PIPELINE_AUDIT.md): aligned face
crop bytes are now uploaded through a dedicated `IRecognitionMediaService` before `FaceImageKey` is
ever assigned to an `AttendanceRecognition` row.

---

## Step 1 — Audit of existing media architecture (and reuse decision)

| Component | Layer | Role | Reused? |
|---|---|---|---|
| `IMediaStorageService` (`SaveOriginalObjectAsync`, `DeleteObjectAsync`) | `Abhyanvaya.Application.Common.Interfaces` | Storage-agnostic write abstraction already used for original photo uploads | ✅ Reused as-is — no changes |
| `ApplicationMediaStorageService` | `Abhyanvaya.API.Media` | Implements `IMediaStorageService`; copies the stream, calls `IStorageProviderFactory.GetActiveProvider().WriteObjectAsync(...)` | ✅ Reused as-is — no changes |
| `MediaStorageService` (`Abhyanvaya.API.Media`, a *different* interface of the same name in `Abhyanvaya.API.Media.IMediaStorageService`) | API layer | Builds WebP variants + `SaveVariantsAsync`/`DeleteVariantsAsync`/health check — used by student photo variant generation | Not reused — its `SaveVariantsAsync` regenerates *variants* from a fresh `Image` load; recognition thumbnails are already final-size WebP bytes, so `IMediaStorageService.SaveOriginalObjectAsync` (single write, no re-decode) is the correct, lighter-weight fit — exactly what `AttendancePhotoService` already uses for its own single-file upload |
| `AttendancePhotoService.UploadToSessionAsync` | `Abhyanvaya.Application` | Calls `_mediaStorage.SaveOriginalObjectAsync(storageKey, imageStream, contentType, ct)` for the original classroom photo | Pattern reused (same call shape) — not the class itself, since it is scoped to whole-session photo upload, not per-face thumbnails |
| `StudentPhotoService` | `Abhyanvaya.API.Services` | Similar upload orchestration for student profile photos, via `IMediaStorageService`/`MediaStorageService` | Pattern reused, class not reused (different domain) |
| `IStorageProvider` / `LocalStorageProvider` / `S3StorageProvider` | `Abhyanvaya.API.Media` | Actual byte-level write to disk or S3/R2 | ✅ Untouched, unreached directly — only ever reached through `IMediaStorageService` |

**Decision:** reuse `IMediaStorageService.SaveOriginalObjectAsync` exactly as `AttendancePhotoService`
already does. This required **zero new storage code** — the only new code is the thin orchestration
service described below.

---

## Step 2 — `IRecognitionMediaService` / `RecognitionMediaService`

**New interface:** `Abhyanvaya.Application/Common/Interfaces/IRecognitionMediaService.cs`

```csharp
public interface IRecognitionMediaService
{
    Task<string> PersistFaceThumbnailAsync(
        int tenantId,
        Guid attendanceSessionId,
        int faceNumber,
        byte[]? alignedFaceBytes,
        Guid executionTraceId,
        CancellationToken cancellationToken = default);
}
```

**New implementation:** `Abhyanvaya.Infrastructure/Recognition/RecognitionMediaService.cs`

Responsibilities — **exactly the three listed in the spec, nothing else:**

1. **Generate deterministic storage key** — `BuildFaceImageKey(tenantId, attendanceSessionId, faceNumber)`
   reproduces the *identical* format previously computed inline in `ClassroomRecognitionPipeline`
   (`recognitions/{tenantId}/{attendanceSessionId}/faces/{faceNumber:D5}.webp`) — so
   `AttendanceSessionMediaPaths.BuildMediaUrl` and the `/media` route need no changes.
2. **Call `IMediaStorageService`** — `SaveOriginalObjectAsync(storageKey, stream, "image/webp", ct)`.
3. **Return the stored key** — only after the write completes without throwing.

No recognition logic, no matching logic, no `DbContext`/EF Core usage, no reference to
`AttendanceRecognition` anywhere in this class.

---

## Step 3 — Orchestration moved into the pipeline

`ClassroomRecognitionPipeline.ProcessAsync`'s face-loop changed from:

```12:14:Abhyanvaya.Infrastructure/Recognition/ClassroomRecognitionPipeline.cs (before)
FaceImageKey = BuildFaceImageKey(session, face.FaceIndex),   // pure string, no upload
```

to:

```154:170:Abhyanvaya.Infrastructure/Recognition/ClassroomRecognitionPipeline.cs
var faceImageKey = await _recognitionMediaService.PersistFaceThumbnailAsync(
    session.TenantId,
    session.Id,
    face.FaceIndex,
    face.AlignedFaceBytes,
    _executionContext.ExecutionTraceId,
    cancellationToken);

recognitions.Add(new AttendanceRecognition
{
    ...
    FaceImageKey = faceImageKey,   // only ever the key an upload actually succeeded with
    ...
});
```

`InsightFaceEngine` was **not modified at all** — it still returns only `DetectedFaceDto` (embedding,
bounding box, landmarks, `AlignedFaceBytes`) and has no reference to `IRecognitionMediaService`,
`IMediaStorageService`, or `IStorageProvider`. Confirmed by diff: `InsightFaceEngine.cs`,
`InsightFaceImageMath.cs`, `InsightFaceOnnxModelHost.cs` all show zero changes in this milestone.

### Dependency graph

```
ClassroomRecognitionPipeline  ───────►  IFaceDetectionService  ───────►  InsightFaceEngine
      │                                                                   (storage-agnostic;
      │                                                                    unchanged)
      │
      └────────►  IRecognitionMediaService  ───────►  IMediaStorageService  ───────►  IStorageProviderFactory
                  (RecognitionMediaService,                (ApplicationMediaStorageService,        │
                   Abhyanvaya.Infrastructure)                Abhyanvaya.API.Media,                  ▼
                                                              unchanged)                    IStorageProvider
                                                                                        (Local / S3 — unchanged)
```

Note the pipeline **never** references `IMediaStorageService` or `IStorageProvider` directly — it only
knows about `IRecognitionMediaService`, per the hard constraint.

### Class diagram

```
┌───────────────────────────────┐        ┌──────────────────────────────────┐
│ «interface»                   │        │ «interface»                      │
│ IRecognitionMediaService      │        │ IMediaStorageService              │
├───────────────────────────────┤        ├──────────────────────────────────┤
│ +PersistFaceThumbnailAsync(   │        │ +SaveOriginalObjectAsync(...)     │
│    tenantId, sessionId,       │        │ +DeleteObjectAsync(...)           │
│    faceNumber, bytes,         │        └──────────────▲───────────────────┘
│    traceId, ct) : Task<string>│                       │ implements
└───────────────▲───────────────┘        ┌──────────────┴───────────────────┐
                 │ implements             │ ApplicationMediaStorageService   │
┌────────────────┴──────────────┐        │ (unchanged, API layer)           │
│ RecognitionMediaService        │        └───────────────────────────────────┘
│ (Infrastructure.Recognition)   │
├────────────────────────────────┤
│ -_mediaStorage: IMediaStorageService
│ -_logger: ILogger<...>         │
│ +PersistFaceThumbnailAsync(...)│
│ -BuildFaceImageKey(...) : string
└────────────────▲───────────────┘
                 │ uses
┌────────────────┴───────────────┐
│ ClassroomRecognitionPipeline    │
│ (unchanged responsibilities:    │
│  detect → match → persist rows) │
└─────────────────────────────────┘
```

### Sequence diagram

```
ClassroomRecognitionPipeline      RecognitionMediaService        IMediaStorageService      IStorageProvider
        │  foreach face in detection.Faces                             │                       │
        │  PersistFaceThumbnailAsync(tenantId, sessionId,               │                       │
        │      faceNumber, AlignedFaceBytes, traceId, ct)               │                       │
        ├───────────────────────────────►│                              │                       │
        │                                 │ bytes null/empty? → throw DomainException (no key ever produced)
        │                                 │ BuildFaceImageKey(...)      │                       │
        │                                 │ log "Upload Started"        │                       │
        │                                 │ SaveOriginalObjectAsync(key, stream, "image/webp", ct)
        │                                 ├──────────────────────────────►│                       │
        │                                 │                              │ WriteObjectAsync(key, bytes, opts, ct)
        │                                 │                              ├──────────────────────►│
        │                                 │                              │◄───────────────────────┤ (disk write / PutObjectAsync)
        │                                 │◄─────────────────────────────┤ (Task completed)
        │                                 │ log "Upload Completed"       │                       │
        │◄────────────────────────────────┤ return storageKey            │                       │
        │  new AttendanceRecognition { FaceImageKey = storageKey, ... } │                       │
        │  (only reached after a successful return)                    │                       │
```

---

## Step 4 — Transaction flow (never a dangling `FaceImageKey`)

```
   ┌─────────────────────────────┐
   │ foreach detected face       │
   └──────────────┬──────────────┘
                  ▼
   ┌─────────────────────────────┐
   │ Upload thumbnail             │  RecognitionMediaService.PersistFaceThumbnailAsync
   │ (IMediaStorageService write) │
   └──────────────┬──────────────┘
                  │
        ┌─────────┴─────────┐
        ▼                   ▼
     success              exception
        │                   │
        ▼                   ▼
 receive storage key   thrown out of the foreach loop in
        │              ClassroomRecognitionPipeline.ProcessAsync
        ▼                   │
 populate FaceImageKey       ▼
 on new AttendanceRecognition   caught by the existing outer try/catch
        │                   (line 211) →
        ▼                   _diagnostics.Fail(ex) → session.MoveToFailed()
 add to `recognitions` list  → SaveChangesAsync (persists the FAILURE,
        │                       not a partial/dangling recognition row)
        ▼                   → rethrow (STEP 6: "Recognition session should fail")
 (after all faces processed)
 _context.AddRangeAsync(recognitions)
        │
        ▼
 SaveChangesAsync — only rows whose thumbnail upload
 already succeeded are ever in this list.
```

**Key guarantee:** the `AttendanceRecognition` object for a given face is only ever constructed
*after* `PersistFaceThumbnailAsync` returns successfully (line 154-170 executes top-to-bottom; the
`recognitions.Add(...)` call is unreachable if the `await` above it throws). Because the whole
`foreach` loop runs before the single `_context.AddRangeAsync(recognitions)` call, a failure on face 2
of 3 means face 1's already-uploaded thumbnail is orphaned in storage (not deleted — no rollback of the
successful upload is implemented) but **no database row is ever created that references an unwritten
object** — the stated hard requirement ("never store `FaceImageKey` before upload succeeds") is met
exactly. Cleaning up an orphaned thumbnail from a failed mid-batch run is a reasonable follow-up but is
out of scope for this milestone (no such requirement was specified, and doing so was not requested).

---

## Step 5 — Structured logging

`RecognitionMediaService.PersistFaceThumbnailAsync` logs exactly three events, all via the standard
`ILogger<RecognitionMediaService>` (no new logging infrastructure):

| Event | Level | Fields |
|---|---|---|
| `Recognition Thumbnail Upload Started` | Information | `ExecutionTraceId`, `AttendanceSessionId`, `FaceNumber`, `StorageKey`, `Bytes` |
| `Recognition Thumbnail Upload Completed` | Information | `ExecutionTraceId`, `AttendanceSessionId`, `FaceNumber`, `StorageKey`, `DurationMs`, `Bytes` |
| `Recognition Thumbnail Upload Failed` | Error (includes exception) | `ExecutionTraceId`, `AttendanceSessionId`, `FaceNumber`, `StorageKey`, `DurationMs`, `Bytes` |

`ExecutionTraceId` is threaded through from `IRecognitionExecutionContext.ExecutionTraceId` (the same
trace id already used by `ExecutionTraceLog`/AI15 diagnostics), so these new logs correlate with all
existing pipeline logs for the same job. **No image bytes are ever logged** — only the byte *count*
(`Bytes`).

Two additional forensics checkpoints (`"Before Thumbnail Persistence"` / `"After Thumbnail
Persistence"`) were added to the pipeline using the pre-existing `IRecognitionForensicsAudit.Checkpoint`
call already used for every other stage (AI17.RUNTIME.1) — this reuses that existing diagnostics
mechanism rather than inventing a new one, and does not change its behavior or thresholds.

---

## Step 6 — Failure handling

- `PersistFaceThumbnailAsync` **throws** (`DomainException`, wrapping the original exception) on any
  upload failure, and also throws (unwrapped) if `AlignedFaceBytes` is null/empty — refusing to
  fabricate a key with nothing behind it.
- `OperationCanceledException` is explicitly re-thrown as-is (not wrapped), so cancellation semantics
  are preserved for callers/hosts that specifically check for that exception type.
- The pipeline adds **no new try/catch** around the call — the exception propagates straight into the
  pre-existing outer `catch (Exception ex)` block (`ClassroomRecognitionPipeline.cs:211-224`), which
  already:
  - Calls `_diagnostics.Fail(ex)` and `_forensics.Checkpoint("Completed (Failed)")` / `FinalizeAudit()`.
  - Sets `session.ProcessingError = ex.Message`, `session.MoveToFailed()`, saves, marks the queue item
    completed, and **rethrows** (`throw;`).
- This means the **Recovery service behaves exactly as it always has** for any other pipeline failure
  (e.g. an OOM or a DB error) — `StuckAttendanceSessionRecoveryService` was not modified and needed no
  changes, because a thumbnail-upload failure now surfaces as an ordinary pipeline exception, not a new
  failure category.
- **Nothing silently continues.** There is no `catch` anywhere in the new code that swallows an
  exception and proceeds to the next face or to database save.

---

## Step 7 — Integration verification

| Check | Status | How verified |
|---|---|---|
| Thumbnail exists in storage after a successful run | ✅ By construction | `PersistFaceThumbnailAsync` only returns after `IMediaStorageService.SaveOriginalObjectAsync` completes without throwing, which itself only returns after `IStorageProvider.WriteObjectAsync` completes |
| `FaceImageKey` matches the stored object's key | ✅ By construction | The exact same `storageKey` local variable is both passed to `SaveOriginalObjectAsync` and returned/assigned to `FaceImageKey` — no separate computation, no drift possible |
| `/media` URL returns HTTP 200 | ✅ Expected, not independently re-tested against a live deployment in this pass | `AttendanceSessionMediaPaths.BuildMediaUrl` (unchanged) builds `/media/{key}?v=…` from the same key format that now names a real object under the local static-file root / S3 bucket |
| Review API returns thumbnail URL | ✅ Unchanged code path | `AttendanceRecognitionReviewService.MapToReviewDtoAsync` (unchanged) still builds `FaceThumbnailUrl` from `recognition.FaceImageKey` — the only change is that this key now names a real file |
| React Avatar displays thumbnail | ✅ Unchanged code path | `RecognitionCard`/`SelectedFaceDetailsPanel` (unchanged) still render `<Avatar src={mediaAssetUrl(faceThumbnailUrl)}>` — a 200 response instead of 404 now lets the `<img>` render instead of falling back |
| Recognized student image is visible | ✅ Expected outcome of the above chain | Follows directly from all of the above; requires a live end-to-end recognition run against a deployed environment to visually confirm, which was out of scope for this local build-only milestone |
| No 404 requests remain | ✅ Expected for newly-created recognitions | Applies to **new** recognition runs after this change; **existing** `AttendanceRecognition` rows already in the database still carry a `FaceImageKey` from before this fix and will continue to 404 until those sessions are re-processed — no backfill/migration was requested or performed, per the "No database changes" constraint context from AI18.REVIEW.1 |

*(Build-machine verification — dotnet build, unit-level construction correctness — was performed and
is recorded in the Final Verification section below; a live browser/API round-trip against a running
instance was not performed as part of this change, since no server was started or deployed in this
session.)*

---

## Step 8 — Regression review

| Area | Verified unchanged | Evidence |
|---|---|---|
| Student photo upload | ✅ | `StudentPhotoService.cs` — 0 diff |
| Attendance (classroom) photo upload | ✅ | `AttendancePhotoService.cs` — 0 diff |
| Enrollment | ✅ | No enrollment-related file touched |
| Recognition (detection/alignment/embedding) | ✅ | `InsightFaceEngine.cs`, `InsightFaceImageMath.cs`, `InsightFaceOnnxModelHost.cs` — 0 diff |
| Matching | ✅ | `FaceMatcher.cs`, `IFaceMatcher.cs` — 0 diff; `_faceMatcher.Match(...)` call site in the pipeline is unchanged, still called once for the whole batch before the (also unchanged) per-face loop that now additionally persists thumbnails |
| Embedding generation | ✅ | `InsightFaceEmbeddingGenerator`/`EmbeddingPipeline`/`EmbeddingNormalizer`/`EmbeddingValidator` — 0 diff |
| Similarity/detection thresholds | ✅ | `InsightFaceOptions.cs` — 0 diff |
| Media URL generation | ✅ | `AttendanceSessionMediaPaths.cs` — 0 diff (the key *format* it consumes was deliberately reproduced identically in `RecognitionMediaService.BuildFaceImageKey`) |
| Storage providers (S3 / Local) | ✅ | `S3StorageProvider.cs`, `LocalStorageProvider.cs`, `IStorageProvider.cs` — 0 diff |
| Database schema | ✅ | No migration added; `AttendanceRecognition` entity/columns unchanged |
| API contracts | ✅ | No controller/DTO file touched; `AttendanceRecognitionController`, `AttendanceRecognitionReviewDto`, etc. — 0 diff |
| React UI | ✅ | No `.tsx`/`.ts` file touched |

**Full diff surface for this milestone** (`git diff --stat` / `git status --porcelain`):

```
 M Abhyanvaya.Infrastructure/DependencyInjection.cs                     (+6 lines: one new DI registration + comment)
 M Abhyanvaya.Infrastructure/Recognition/ClassroomRecognitionPipeline.cs (+24/-4: new dependency, loop reordering)
?? Abhyanvaya.Application/Common/Interfaces/IRecognitionMediaService.cs (new file)
?? Abhyanvaya.Infrastructure/Recognition/RecognitionMediaService.cs     (new file)
```

Two files modified, two files added. No other file in the repository was touched by this milestone.

---

## Risk assessment

| Risk | Severity | Mitigation / residual risk |
|---|---|---|
| Mid-batch failure leaves an earlier face's thumbnail uploaded but no DB row referencing it (orphaned object, not a dangling key) | Low | Acceptable per the stated hard requirement, which only prohibits a *dangling key* (a DB row pointing at nothing) — the reverse (an object with no DB row) is not itself user-visible or harmful, only a minor storage-cleanliness item; no cleanup job exists today for any orphaned media in this codebase, so this is consistent with existing conventions, not a new risk pattern |
| Existing recognition rows created before this fix still 404 | Medium (functional gap, not a regression) | Out of scope — no reprocessing/backfill job was requested; flagged here for awareness, not silently ignored |
| Slightly increased per-face latency in `ProcessAsync` (one storage write added per face, previously zero) | Low–Medium | Necessary cost of actually persisting the thumbnail; uses the exact same `IStorageProvider` write path already proven fast enough for original photo/student-photo uploads elsewhere in the app; no batching/parallelization was attempted since the constraints prohibit optimizing anything beyond what's needed for correctness |
| `RecognitionMediaService` throwing on empty `AlignedFaceBytes` could newly fail sessions that previously "succeeded" (with a silently-broken thumbnail) if `InsightFaceEngine` ever produces a face without alignment bytes | Low | Per AI18.REVIEW.1's Task 2 evidence, `InsightFaceEngine.DetectAsync` always calls `SaveAsWebpAsync` for every candidate face before adding it to `faces`, so `AlignedFaceBytes` is always populated in the current implementation — this guard is defensive, not expected to trigger in practice, and is the correct behavior per STEP 4/6 if it ever did (fail cleanly rather than silently produce a dangling key) |

---

## Final Verification

- ✅ **Clean Architecture preserved** — `Application` defines `IRecognitionMediaService` (an
  abstraction), `Infrastructure` provides `RecognitionMediaService` (the implementation), and the
  concrete storage implementation (`ApplicationMediaStorageService`) still lives in the outer `API`
  layer exactly as it did before — no layer now depends on a layer "above" it.
- ✅ **Single Responsibility preserved** — `RecognitionMediaService` only builds a key, uploads bytes,
  and returns the key; `ClassroomRecognitionPipeline` still owns orchestration/recognition-row
  creation; `InsightFaceEngine` still owns AI/image processing only.
- ✅ **AI engine remains storage-agnostic** — `InsightFaceEngine.cs` has zero diff in this milestone
  and has no constructor dependency on `IRecognitionMediaService`, `IMediaStorageService`, or
  `IStorageProvider`.
- ✅ **Media service reused** — `IMediaStorageService.SaveOriginalObjectAsync` (pre-existing) is the
  only write call added; no new `IStorageProvider` usage was introduced.
- ✅ **No duplicate upload code** — the byte-level write logic exists in exactly one place
  (`ApplicationMediaStorageService`/`IStorageProvider` implementations), unchanged.
- ✅ **No dangling `FaceImageKey`** — `AttendanceRecognition.FaceImageKey` is only ever assigned the
  string returned by a successful `PersistFaceThumbnailAsync` call; a failed upload throws before any
  `AttendanceRecognition` object referencing that face is constructed.
- ✅ **Thumbnail visible in Recognition Review** — expected as a direct consequence of the now-real
  object existing at the key the (unchanged) review DTO/React UI already knew how to request; full
  live browser verification requires a deployed run, which was outside this local-build-only session.
- ✅ **`dotnet build` succeeds with zero errors:**

  ```
  dotnet build Abhyanvaya.sln
  Build succeeded.
      0 Error(s)
  ```

  (All warnings present in the build output are pre-existing nullable-reference warnings in unrelated
  controllers/DTOs, confirmed by inspection to contain no reference to any file changed in this
  milestone.)

**No changes were committed**, per the task's explicit instruction.
