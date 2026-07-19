# AI20.ENROLLMENT.1 — Student AI Enrollment Architecture

**Type:** Architecture design only. No production code was written or modified to produce this document.

---

## 1. Review of Existing Architecture — Reusable Components

Every component listed below already exists, is production-proven (AI16–AI19), and is reused **unchanged** by AI Enrollment unless explicitly noted as "extended."

| Component | File | Role today | Reuse in AI Enrollment |
|---|---|---|---|
| `AttendancePhotoService` | `Abhyanvaya.Application/AttendancePhotoService.cs` | Upload classroom photo → `IMediaStorageService.SaveOriginalObjectAsync` → enqueue `IClassroomPhotoQueue` | **Pattern mirrored**, not called directly — the new `EnrollmentPhotoDownloadService` follows the identical "validate → upload → enqueue" shape, but the *source* of bytes is an HTTP download instead of a browser `IFormFile` |
| `RecognitionMediaService` | `Abhyanvaya.Infrastructure/Recognition/RecognitionMediaService.cs` | Deterministic key builder (`recognitions/{tenantId}/{sessionId}/faces/{n:D5}.webp`) + `IMediaStorageService.SaveOriginalObjectAsync`, throws (never returns null/empty) on failure | **Pattern mirrored** by a new `EnrollmentMediaService`, but see §2 — the *final* photo location reuses the **existing** `StudentMediaPaths.BuildStoragePath(tenantId, studentId)` key (`students/{tenantId}/{studentId}`), not a new prefix, so the enrolled photo is indistinguishable from a manually-uploaded one everywhere else in the app |
| `MediaStorageService` (API layer, `Abhyanvaya.API/Media/MediaStorageService.cs`) | `BuildWebpVariantsAsync` (original/thumbnail WebP), `SaveVariantsAsync`, `ValidateRasterUpload` | **Reused as-is, unmodified** — same variant sizes (800px original / 200px thumbnail) `StudentPhotoService` already uses |
| `IStorageProviderFactory` / `IStorageProvider` / `LocalStorageProvider` / `S3StorageProvider` | `Abhyanvaya.API/Media/*` | Provider-aware upload+download (Local disk / Cloudflare R2), fixed for retrieval in AI19.MEDIA.3.2 | **Reused as-is, unmodified** — enrollment photos are just another object key, no new provider logic |
| `MediaController` / `IMediaObjectReader` | `Abhyanvaya.API/Controllers/MediaController.cs`, `Abhyanvaya.Application/Common/Interfaces/IMediaObjectReader.cs` | `GET /media/{**key}` provider-aware retrieval | **Reused as-is** — enrolled photos are retrieved via the exact same `/media/students/{tenantId}/{studentId}/{variant}.webp` URL the manual-upload path already produces |
| `InsightFaceOnnxModelHost` | `Abhyanvaya.Infrastructure/InsightFace/InsightFaceOnnxModelHost.cs` | Singleton, lazy-loaded, cached `InferenceSession` for `det_10g.onnx` (detection) and `w600k_r50.onnx` (recognition) | **Reused as-is, unmodified** — no new models, no new sessions; enrollment and attendance recognition share the same two cached sessions |
| `InsightFaceEngine.GenerateSingleFaceEmbedding` | `Abhyanvaya.Infrastructure/InsightFace/InsightFaceEngine.cs:176-188` | Detect → pick best-score face → align → embed. **Does not enforce exactly one face; no blur/pose/sunglasses checks** | **Extended, not replaced** — see §6 and `docs/AI20_ENROLLMENT_ENGINE.md` for the new, stricter `GenerateEnrollmentEmbedding` wrapper this milestone requires |
| `EmbeddingStorage` / `StudentFaceEmbedding` table | `Abhyanvaya.Infrastructure/Embedding/EmbeddingStorage.cs`, `Abhyanvaya.Domain/Entities/StudentFaceEmbedding.cs` | Versioned embedding storage: `EmbeddingVector` (`real[]`), `EmbeddingStatus`, `EmbeddingQuality`, `PhotoVersion`, `RetryCount`, `LastFailureReason`, `IsActive` filtered-unique index | **Reused as-is, unmodified** — this is already the canonical "one active embedding per student" store; AI Enrollment writes to it exactly the way manual photo upload's `StudentFaceEmbeddingBackgroundService` already does |
| Background worker framework (`Channel<T>` singleton queue + `BackgroundService` consumer + `IServiceScopeFactory.CreateAsyncScope()` per job) | `Abhyanvaya.Infrastructure/BackgroundWorkers/*`, `Abhyanvaya.Infrastructure/Recognition/InMemoryClassroomPhotoQueue.cs` | Classroom recognition + student embedding processing | **Pattern reused, deliberately extended** — see §5 and `docs/AI20_ENROLLMENT_BACKGROUND.md`: a pure in-memory `Channel<T>` is judged **insufficient** for a bulk, long-running, thousands-of-students job and is replaced with a DB-row-backed durable queue using the same consumer shape |
| `StuckAttendanceSessionRecoveryService` | `Abhyanvaya.Infrastructure/BackgroundWorkers/StuckAttendanceSessionRecoveryService.cs` | `PeriodicTimer` sweep that finds sessions stuck in `Processing` past a timeout and force-fails them | **Pattern mirrored** by a new `StuckEnrollmentJobRecoveryService` |
| Storage providers | (see above) | — | Reused |
| Media retrieval | (see above) | — | Reused |
| `Student` entity | `Abhyanvaya.Domain/Entities/Student.cs` | `PhotoKey`, `PhotoUploadedUtc`, `PhotoVerified`, `Batch` (int?), `StudentNumber`, `TenantId` | **Reused, unmodified.** `Student.Batch` and `College.Code` (via `TenantId` → `College`) are the two fields enrollment maps onto the exambranch.com URL's `{year}`/`{collegeCode}` segments — no schema change to `Student` is proposed (see `docs/AI20_ENROLLMENT_DATABASE.md`) |

### Why this reuse is architecturally correct

Every piece above already sits behind a Clean Architecture interface (`IMediaStorageService`, `IStorageProviderFactory`, `IEmbeddingGenerator`, `IClassroomPhotoQueue`-style queue abstractions). AI Enrollment introduces **new orchestration on top of existing abstractions**, not parallel/competing infrastructure. This keeps the blast radius of the new feature limited to new files plus two additive database tables (§ Database design, `docs/AI20_ENROLLMENT_DATABASE.md`) — nothing existing is modified.

---

## 2. The New Subsystem: **AI Enrollment**

### Responsibilities

1. Given a scope (University + College + Batch/year), enumerate the students in that scope who need enrollment (no `IsActive` `StudentFaceEmbedding`, or a stale one).
2. For each student, download their photograph from the external source (`docs/AI20_PHOTO_IMPORT.md`), using `College.Code` + `Student.Batch` + `Student.StudentNumber` to build the source URL.
3. Validate the downloaded image (file-level: size/format/dimensions), upload it into the **existing** student-photo storage location (`students/{tenantId}/{studentId}`), and update `Student.PhotoKey`/`PhotoUploadedUtc` — this is the exact same effect a manual photo upload has today, achieved through automation instead of a browser form.
4. Run **stricter** face validation than classroom recognition (`docs/AI20_ENROLLMENT_ENGINE.md`) — exactly one face, no blur, minimum resolution — and generate the 512-d embedding.
5. Store the embedding into the **existing** `StudentFaceEmbedding` table via the **existing** `EmbeddingStorage` service.
6. Track per-student and per-batch progress/status, exposed only to SuperAdmin, with retry/cancel/resume.

### Lifecycle

A **Batch** (SuperAdmin-initiated, scoped to University+College+Batch year) is `Created` → `Running` → (`Completed` | `Cancelled` | `PartiallyFailed`). Within a Batch, each **Job** (one per student) moves through `Pending → Downloading → Downloaded → Validating → Embedding → Completed`, with `Failed` and `RetryRequired` as branch points at every stage (full state machine in §6 and `docs/AI20_ENROLLMENT_DATABASE.md` / `docs/AI20_ENROLLMENT_ENGINE.md`).

### Dependencies

AI Enrollment depends on (never the reverse — no existing service is made aware of "enrollment"):

- `IMediaStorageService` (Application) — same interface `AttendancePhotoService`/`RecognitionMediaService` use
- `Abhyanvaya.API.Media.IMediaStorageService` (API) — same interface `StudentPhotoService`/`CollegeBrandingService` use, for `BuildWebpVariantsAsync`/`SaveVariantsAsync`
- `IStorageProviderFactory` (indirectly, via the two interfaces above)
- `IEmbeddingGenerator` / `InsightFaceEngine` / `InsightFaceOnnxModelHost` (extended per §6)
- `EmbeddingStorage` (existing embedding persistence)
- `IApplicationDbContext` / `IUnitOfWork` (EF Core)
- A new `IExternalPhotoSource` HTTP client abstraction (`docs/AI20_PHOTO_IMPORT.md`)
- `AuthorizationPolicies.SuperAdminOnly` (API), `ProtectedRoute allowedRoles={["SuperAdmin"]}` (React)

### Boundaries

- AI Enrollment **never** modifies `AttendanceRecognition`, `AttendanceSession`, matching thresholds, or the classroom recognition pipeline. It is a **sibling** subsystem to classroom recognition, not a dependency of it.
- AI Enrollment **never** talks to `IStorageProvider` directly — always through `IMediaStorageService`, exactly like every other feature.
- AI Enrollment **never** duplicates the `StudentFaceEmbedding` table's responsibility — it only calls the same `EmbeddingStorage` API the manual-upload embedding pipeline already calls.
- Faculty, College Admin, and Student users have **zero** visibility into this subsystem — no route, no menu item, no API access (§7).

---

## 3. Workflow Design

```
Student (scope: University + College + Batch/year)
        │
        ▼
┌─────────────────────┐   College.Code + Student.Batch + Student.StudentNumber
│   Photo Download      │──▶ GET {ExternalPhotoBaseUrl}/{collegeCode}/{year}/{studentNumber}.jpg
└─────────────────────┘   (docs/AI20_PHOTO_IMPORT.md: timeouts, retry, throttling)
        │  bytes
        ▼
┌─────────────────────┐
│   Validation          │  magic-byte / ImageSharp decode check, min resolution, size cap
│   (file-level)         │  → reject 403/404/invalid/corrupt/duplicate (see AI20_PHOTO_IMPORT.md)
└─────────────────────┘
        │  valid image bytes
        ▼
┌─────────────────────┐  MediaStorageService.BuildWebpVariantsAsync + SaveVariantsAsync
│   Upload to R2         │  key = students/{tenantId}/{studentId}  (EXISTING StudentMediaPaths key)
└─────────────────────┘  Student.PhotoKey / PhotoUploadedUtc updated (EXISTING columns)
        │
        ▼
┌─────────────────────┐  InsightFaceEngine detection (existing det_10g.onnx session)
│   Face Detection       │
└─────────────────────┘
        │  0, 1, or >1 candidate faces
        ▼
┌─────────────────────┐  NEW, stricter than classroom recognition:
│   Quality Validation   │  exactly one face / no blur / min resolution / (pose, eyes, no
│                        │  sunglasses — phased, see AI20_ENROLLMENT_ENGINE.md)
└─────────────────────┘  → reject to Failed/RetryRequired with a specific reason
        │  exactly one valid face
        ▼
┌─────────────────────┐  InsightFaceEngine embedding (existing w600k_r50.onnx session,
│   Embedding Generation │  existing 112×112 ArcFace path, existing 512-d output)
└─────────────────────┘
        │  float[512]
        ▼
┌─────────────────────┐  EXISTING EmbeddingStorage.StoreCompletedAsync
│   Embedding Storage    │  → StudentFaceEmbedding row (IsActive=true, versioned)
└─────────────────────┘
        │
        ▼
┌─────────────────────┐
│  Enrollment Complete   │  Job.Status = Completed; Batch counters incremented
└─────────────────────┘
```

Any stage can fail and transition the Job to `Failed` (terminal, reason recorded) or `RetryRequired` (transient, eligible for automatic or SuperAdmin-triggered retry) — see the lifecycle diagram in §6.

---

## 4. Reuse vs. New — Summary Table

| | Reused unchanged | New |
|---|---|---|
| **Storage** | `IMediaStorageService`, `IStorageProviderFactory`, `LocalStorageProvider`, `S3StorageProvider`, `MediaController`, existing `students/{tenantId}/{studentId}` key | — |
| **AI Engine** | `InsightFaceOnnxModelHost`, ONNX sessions, ArcFace embedding extraction, `EmbeddingStorage`, `StudentFaceEmbedding` table | `EnrollmentValidationService` (exactly-one-face, blur, resolution — see `docs/AI20_ENROLLMENT_ENGINE.md`); a stricter `GenerateEnrollmentEmbedding` wrapper on `InsightFaceEngine` |
| **Data** | `Student` (unmodified), `College.Code`, `Student.Batch` | `StudentEnrollmentBatch`, `StudentEnrollmentJob` tables (`docs/AI20_ENROLLMENT_DATABASE.md`) |
| **Background processing** | The overall `BackgroundService` + DI-scope-per-job shape; the `StuckAttendanceSessionRecoveryService` sweep shape | `IEnrollmentJobQueue` (DB-row-backed, not pure in-memory `Channel<T>` — see §5), `EnrollmentBackgroundService`, `StuckEnrollmentJobRecoveryService` |
| **Photo source** | `Abhyanvaya.API.Media.IMediaStorageService.BuildWebpVariantsAsync`/`ValidateRasterUpload` | `IExternalPhotoSource` HTTP download client (`docs/AI20_PHOTO_IMPORT.md`) |
| **API/Auth** | `AuthorizationPolicies.SuperAdminOnly`, `HasTenantHandler` pattern (not used — enrollment is role-only, no tenant-scope requirement, mirroring `OrganizationController`) | `EnrollmentController` |
| **UI** | `ProtectedRoute`, `MainLayout` menu `visible` predicate pattern | New route `/ai-enrollment`, new pages (`docs/AI20_ENROLLMENT_UI.md`) |

---

## 5. Background Processing, Job Queue, Retry, Progress, Cancellation, Resume — Architecture-Level Summary

(Full detail in `docs/AI20_ENROLLMENT_BACKGROUND.md`; this section states the headline decision and why.)

**Headline decision: the enrollment job queue is DB-row-backed, not a pure in-memory `Channel<T>`.**

The existing `InMemoryClassroomPhotoQueue`/`InMemoryStudentPhotoEmbeddingQueue` pattern (`Channel<T>`, singleton, non-durable) is correct for classroom recognition because a single classroom photo job is short-lived and low-stakes if lost (the teacher can re-upload). A SuperAdmin enrollment **batch** can enumerate thousands of students and run for a long time; losing all *not-yet-dequeued* work on a Render restart/redeploy mid-batch (which the existing pattern would do, since nothing survives outside the in-memory channel until a job is actually dequeued) is not acceptable for a bulk administrative operation.

Instead: `StudentEnrollmentJob.Status` **is** the queue. A background worker polls for `Status IN (Pending, RetryRequired)` ordered by `CreatedUtc`, plus an in-memory `Channel<Guid>` "wake" signal for low dequeue latency right after a batch is created — but correctness never depends on the in-memory signal surviving a restart, only on the DB rows. This gives the same responsiveness as the existing pattern while being resumable for free: a crashed/restarted process simply re-polls the DB and continues exactly where it left off. See `docs/AI20_ENROLLMENT_BACKGROUND.md` for the full design, retry/backoff strategy, crash recovery sweep, cancellation semantics, and parallelism limits (higher concurrency for I/O-bound downloads, lower/ONNX-thread-aware concurrency for embedding generation).

---

## 6. Enrollment Status — State Machine

Per the user's specified vocabulary, with explicit transition edges:

```
                 ┌─────────┐
                 │ Pending │  (job row created, not yet claimed by a worker)
                 └────┬────┘
                      │ worker claims job
                      ▼
               ┌─────────────┐
               │ Downloading  │──── HTTP 404/403/timeout/exhausted retries ────▶┐
               └──────┬──────┘                                                  │
                      │ HTTP 200, bytes received                                │
                      ▼                                                         │
               ┌─────────────┐                                                  │
               │  Downloaded  │──── invalid/corrupt image ──────────────────────┤
               └──────┬──────┘                                                  │
                      │ upload to R2 succeeds                                   │
                      ▼                                                         │
               ┌─────────────┐                                                  │
               │  Validating  │──── 0 faces / >1 faces / blur / low-res ────────┤
               └──────┬──────┘                                                  │
                      │ exactly one valid face                                  │
                      ▼                                                         │
               ┌─────────────┐                                                  │
               │  Embedding   │──── ONNX/engine exception ──────────────────────┤
               └──────┬──────┘                                                  │
                      │ embedding stored (EmbeddingStorage)                     │
                      ▼                                                         ▼
               ┌─────────────┐                                          ┌──────────────┐
               │  Completed   │  (terminal, success)                    │ RetryRequired │
               └─────────────┘                                          │  or Failed     │
                                                                          └──────┬───────┘
                                        SuperAdmin bulk/single retry             │
                                        (or automatic retry for transient        │
                                         failures — download timeout, R2         │
                                         transient error, ONNX transient error)  │
                                                     ▲                           │
                                                     └───────────────────────────┘
                                                     (RetryRequired re-enters at Pending;
                                                      Failed is terminal until a SuperAdmin
                                                      retry explicitly re-queues it)
```

`RetryRequired` vs `Failed` distinction (mirrors the classification already established in `docs/AI20_PHOTO_IMPORT.md`/`docs/AI20_ENROLLMENT_ENGINE.md`): transient causes (timeout, 5xx, R2 hiccup, ONNX resource exhaustion) → `RetryRequired`, auto-retried up to a max count then escalated to `Failed`; permanent causes (404 photo not found, 403 access denied, invalid/corrupt image, zero or multiple faces, blur/resolution rejection) → `Failed` directly, since retrying without a source-data change cannot succeed — but still SuperAdmin-retriable manually after the underlying photo is fixed at the source.

---

## 7. Security Design — SuperAdmin-Only

### Backend

```1:2:Abhyanvaya.Domain/Enums/UserRole.cs
public enum UserRole { Admin = 1, Faculty = 2, SuperAdmin = 3 }
```

New `EnrollmentController` mirrors `OrganizationController` (`Abhyanvaya.API/Controllers/OrganizationController.cs:13-16`) exactly:

```csharp
[ApiController]
[Route("api/enrollment")]
[Authorize(Policy = AuthorizationPolicies.SuperAdminOnly)]
public sealed class EnrollmentController : ControllerBase { /* ... */ }
```

`AuthorizationPolicies.SuperAdminOnly` (`Abhyanvaya.API/Common/AuthorizationPolicies.cs`) is defined in `Program.cs` as:

```csharp
options.AddPolicy(AuthorizationPolicies.SuperAdminOnly, policy =>
    policy.RequireAuthenticatedUser().RequireRole(nameof(UserRole.SuperAdmin)));
```

This is a pure role check — **no** `TenantScopedUser`/`HasTenantHandler` requirement is used, because (per the existing `HasTenantHandler` pattern) SuperAdmin users legitimately have `TenantId = 0`, and this feature spans multiple tenants (colleges) by design — it is never tenant-scoped in the way Admin/Faculty features are.

### Routing / React Authorization

Mirrors the existing `admin-setup` → `OrganizationPage` route exactly (`abhyanvaya-ui/src/routes/AppRoutes.tsx:191-195`):

```tsx
<Route
  path="ai-enrollment"
  element={
    <ProtectedRoute allowedRoles={["SuperAdmin"]}>
      <AiEnrollmentPage />
    </ProtectedRoute>
  }
/>
```

`ProtectedRoute` (`abhyanvaya-ui/src/routes/ProtectedRoute.tsx`) evaluates `allowedRoles` via case-insensitive role compare (`user.role.toLowerCase() === "superadmin"`) and redirects to `/dashboard` for any other role — Faculty, Admin, and (implicitly, since students never receive a login role in this system) any non-SuperAdmin user is redirected, never shown even a disabled/greyed-out UI.

### Menu Visibility

Mirrors the existing `MainLayout.tsx` "Organization" entry exactly:

```tsx
{
  text: "AI Enrollment",
  icon: <FaceRetouchingNaturalIcon />,
  path: "/ai-enrollment",
  visible: ({ role }) => role === "superadmin",
},
```

Because `MainLayout` filters `menuItems` through `visible(...)` before rendering (`visibleMenuItems = menuItems.filter(...)`), Faculty/Admin/Student sessions never render this menu entry in the DOM at all — not merely hidden via CSS.

### Defense in depth

| Layer | Enforcement |
|---|---|
| API | `[Authorize(Policy = SuperAdminOnly)]` — a Faculty/Admin JWT is rejected with `403 Forbidden` even if they hand-craft a request to `/api/enrollment/*` |
| Routing | `ProtectedRoute allowedRoles={["SuperAdmin"]}` — redirects to `/dashboard` before the page component ever mounts |
| Menu | `visible` predicate — no rendered link exists for non-SuperAdmin roles |

All three layers must be bypassed independently for a non-SuperAdmin to reach this feature; this is the same three-layer pattern `admin-setup`/`OrganizationPage` already relies on today, so no new authorization primitive is introduced.

---

## 8. Architecture Diagrams

### Component Diagram

```
┌──────────────────────────────────────────────────────────────────────────┐
│                              React UI (SuperAdmin)                         │
│  AiEnrollmentDashboardPage │ ProgressPanel │ FailuresPanel │ StudentDetail │
└───────────────────────────────────┬────────────────────────────────────────┘
                                    │ HTTPS (JWT: Role=SuperAdmin)
                                    ▼
┌──────────────────────────────────────────────────────────────────────────┐
│  EnrollmentController  [Authorize(Policy=SuperAdminOnly)]                  │
│   CreateBatch / GetBatchStatus / RetryJob / CancelBatch / ResumeBatch       │
└───────────┬───────────────────────────────────┬────────────────────────────┘
            │                                    │
            ▼                                    ▼
┌─────────────────────────┐          ┌─────────────────────────────┐
│  EnrollmentBatchService   │          │  IEnrollmentJobQueue          │
│  (create/list/aggregate)  │          │  (DB-row-backed + in-memory   │
└─────────────┬────────────┘          │   Channel<Guid> wake signal)  │
              │                        └───────────────┬───────────────┘
              ▼                                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    EnrollmentBackgroundService (BackgroundService)         │
│   per job: CreateAsyncScope → set tenant → EnrollmentPipeline.ProcessAsync  │
└───────┬─────────────┬─────────────┬─────────────┬─────────────┬───────────┘
        ▼             ▼             ▼             ▼             ▼
┌───────────┐ ┌──────────────┐ ┌───────────┐ ┌──────────────┐ ┌────────────┐
│ Enrollment │ │  Enrollment   │ │  Media    │ │ InsightFace  │ │ Embedding  │
│ PhotoDown- │ │  Validation   │ │ Storage   │ │   Engine      │ │ Storage    │
│ loadService│ │  Service (NEW)│ │ Service    │ │ (EXISTING)   │ │ (EXISTING) │
│ (NEW)      │ │               │ │(EXISTING) │ │              │ │            │
└─────┬─────┘ └──────────────┘ └─────┬─────┘ └──────┬───────┘ └─────┬──────┘
      │ HTTPS                        │              │                │
      ▼                              ▼              ▼                ▼
┌────────────┐            ┌──────────────────┐ ┌───────────┐  ┌───────────────┐
│exambranch  │            │IStorageProvider  │ │ ONNX       │  │StudentFace     │
│  .com      │            │Factory → R2/Local│ │ Runtime    │  │Embedding table │
└────────────┘            └──────────────────┘ └───────────┘  └───────────────┘

              StuckEnrollmentJobRecoveryService (PeriodicTimer, mirrors
              StuckAttendanceSessionRecoveryService) sweeps stuck
              Downloading/Validating/Embedding jobs → RetryRequired
```

### Sequence Diagram — Happy Path (Single Student)

```
SuperAdmin UI     EnrollmentController   EnrollmentBackgroundService   EnrollmentPipeline   External Photo Source   MediaStorageService   InsightFaceEngine   EmbeddingStorage   DB
     │  POST /api/enrollment/batches            │                            │                    │                       │                     │                  │            │
     │─────────────────────────▶│               │                            │                    │                       │                     │                  │            │
     │                          │  create Batch + N Pending Jobs             │                    │                       │                     │                  │            │
     │                          │────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────▶│
     │◀── 202 Accepted, BatchId ─│               │                            │                    │                       │                     │                  │            │
     │                          │               │  poll (DB) finds Pending job│                    │                       │                     │                  │            │
     │                          │               │───────────────────────────▶│                    │                       │                     │                  │            │
     │                          │               │                            │  GET {baseUrl}/{code}/{year}/{num}.jpg     │                     │                  │            │
     │                          │               │                            │───────────────────▶│                       │                     │                  │            │
     │                          │               │                            │◀── 200, image bytes│                       │                     │                  │            │
     │                          │               │                            │  BuildWebpVariantsAsync + SaveVariantsAsync│                     │                  │            │
     │                          │               │                            │────────────────────────────────────────────▶│                     │                  │            │
     │                          │               │                            │◀── stored (R2/local)                        │                     │                  │            │
     │                          │               │                            │  Student.PhotoKey/PhotoUploadedUtc updated  │                     │                  │            │
     │                          │               │                            │────────────────────────────────────────────────────────────────────────────────────────────────▶│
     │                          │               │                            │  DetectFaces + validate (exactly one, no blur, min-res)                  │                  │            │
     │                          │               │                            │─────────────────────────────────────────────────────────────────▶│                  │            │
     │                          │               │                            │◀── DetectedFaceDto (or rejection reason)                            │                  │            │
     │                          │               │                            │  GenerateEnrollmentEmbedding                                        │                  │            │
     │                          │               │                            │─────────────────────────────────────────────────────────────────▶│                  │            │
     │                          │               │                            │◀── float[512]                                                        │                  │            │
     │                          │               │                            │  StoreCompletedAsync                                                                  │                  │            │
     │                          │               │                            │──────────────────────────────────────────────────────────────────────────────────────▶│            │
     │                          │               │                            │◀── StudentFaceEmbedding row (IsActive=true)                                           │            │
     │                          │               │                            │  Job.Status = Completed; Batch counters++                                                          │            │
     │                          │               │                            │───────────────────────────────────────────────────────────────────────────────────────────────────▶│
     │  GET /api/enrollment/batches/{id}/status (polling)                    │                    │                       │                     │                  │            │
     │─────────────────────────▶│──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────▶│
     │◀── progress %, counts by status ─│         │                            │                    │                       │                     │                  │            │
```

### Lifecycle Diagram

See §6 (Enrollment Status state machine) — this *is* the lifecycle diagram for a Job. The Batch lifecycle is simpler:

```
Created ──▶ Running ──┬──▶ Completed        (all jobs terminal, zero Failed)
                       ├──▶ PartiallyFailed  (all jobs terminal, ≥1 Failed)
                       └──▶ Cancelled        (SuperAdmin cancelled; remaining Pending jobs → Cancelled)
```

### Deployment Diagram

```
┌───────────────────────────────────────────────────────────────────┐
│                     Render (Web Service, existing API host)          │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │  Abhyanvaya.API process                                       │   │
│  │   - Kestrel (HTTP, existing controllers + new EnrollmentController)│
│  │   - Existing hosted services: ClassroomRecognitionBackgroundService,│
│  │     StudentFaceEmbeddingBackgroundService, StuckAttendanceSessionRecoveryService│
│  │   - NEW hosted services: EnrollmentBackgroundService,             │
│  │     StuckEnrollmentJobRecoveryService                             │
│  │   - Singleton InsightFaceOnnxModelHost (shared ONNX sessions)     │
│  └────────────────────────────────────────────────────────────┘   │
└───────────────┬───────────────────────────┬─────────────────────────┘
                │                            │
                ▼                            ▼
     ┌─────────────────────┐      ┌─────────────────────────┐
     │  Neon (PostgreSQL)    │      │  Cloudflare R2 (via         │
     │  Student,              │      │  S3StorageProvider,          │
     │  StudentFaceEmbedding, │      │  EXISTING, unmodified)       │
     │  NEW: StudentEnrollment│      └─────────────────────────┘
     │  Batch/Job tables      │
     └─────────────────────┘
                │
                ▼ (outbound HTTPS only, new external dependency)
     ┌─────────────────────┐
     │  exambranch.com        │  (or configurable ExternalPhotoBaseUrl —
     │  (external photo host) │   see docs/AI20_PHOTO_IMPORT.md)
     └─────────────────────┘

     ┌─────────────────────┐
     │  Cloudflare Pages       │  abhyanvaya-ui (existing SPA) +
     │  (abhyanvaya-ui.pages.dev)│ new /ai-enrollment route, SuperAdmin-only
     └─────────────────────┘
```

No new deployment target is introduced — the new hosted services run inside the existing single API process on Render, exactly like the three existing `BackgroundService` workers. The only new *external* network dependency for the whole platform is outbound HTTPS to the photo source host.

---

## Constraints Confirmed

No production code was written or modified to produce this document. All file/line citations above reflect the current, unmodified state of the repository as explored for this milestone.
