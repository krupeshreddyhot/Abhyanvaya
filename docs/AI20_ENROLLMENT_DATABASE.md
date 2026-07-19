# AI20.ENROLLMENT.2 — AI Enrollment Database Design

**Type:** Design only. No migrations were created and no database was modified to produce this document.

---

## 1. Review of Existing Tables

| Table | Key columns relevant to enrollment | Citation |
|---|---|---|
| `Student` | `Id`, `TenantId`, `StudentNumber`, `Batch` (`int?`), `PhotoKey` (`varchar(500)?`), `PhotoUploadedUtc` (`timestamptz?`), `PhotoVerified` (`bool`) | `Abhyanvaya.Domain/Entities/Student.cs`; photo columns added by `Abhyanvaya.Infrastructure/Migrations/20260628113951_AddStudentPhotoColumns.cs:14-32` |
| `StudentFaceEmbedding` | `Id` (`uuid`), `TenantId`, `StudentId`, `EmbeddingVector` (`real[]`), `EmbeddingModel`, `EmbeddingVersion`, `EmbeddingStatus` (int enum), `EmbeddingQuality` (int enum), `EmbeddingDimension`, `PhotoVersion` (`bigint`), `RetryCount`, `LastFailureUtc`, `LastFailureReason`, `PhotoKey`, `GeneratedUtc`, `GeneratedBy`, `IsActive` | `Abhyanvaya.Domain/Entities/StudentFaceEmbedding.cs:10-54`; config: `Abhyanvaya.Infrastructure/Persistence/Configurations/StudentFaceEmbeddingConfiguration.cs` |
| `College` | `Id`, `TenantId`, `Code` (`string`, required, unique per `(UniversityId, Code)`), `UniversityId` | `Abhyanvaya.Domain/Entities/College.cs` |
| `AttendanceRecognition` (Media/Recognition reference point) | `FaceImageKey`, `EmbeddingDistance` (`decimal(10,6)?`) — **no embedding vector stored here**, confirms the precedent that raw vectors live only in `StudentFaceEmbedding` | `Abhyanvaya.Domain/Entities/AttendanceRecognition.cs` |

**Key existing fact that shapes this design:** `StudentFaceEmbedding` already has `EmbeddingStatus`, `EmbeddingQuality`, `RetryCount`, `LastFailureUtc`, `LastFailureReason`, `PhotoKey`, `PhotoVersion`, and `IsActive`. This is deliberate — it already tracks the *embedding's own* lifecycle (pending/processing/completed/failed for the vector-generation step alone). AI Enrollment needs to track a **superset** of that lifecycle (download, storage upload, *and* embedding), for potentially thousands of students in a single administrative operation, grouped by batch. Reusing/overloading `StudentFaceEmbedding` for this would conflate "the current active embedding record" with "an administrative bulk-import job," which have different cardinalities and different retention/audit needs (a job's failed-download history should be inspectable even after a later retry succeeds; `StudentFaceEmbedding`'s unique-filtered-active-index design is explicitly *not* built for that).

---

## 2. Are New Tables Required?

**Yes — two new tables, no modification to any existing table.**

### Decision: `StudentEnrollmentJob` (new) vs. extending `Student` vs. extending `StudentFaceEmbedding`

| Option | Verdict | Reasoning |
|---|---|---|
| Extend `Student` with enrollment columns | **Rejected** | `Student` is a shared, high-traffic entity referenced by auth, attendance, reports, and setup screens. Bulk-job bookkeeping (download URL, HTTP status, retry count *for the download specifically*, batch membership) is operationally unrelated to what `Student` represents and would bloat every `Student` query across the app for a concern only SuperAdmin ever touches. This mirrors why `StudentFaceEmbedding` was already split out from `Student` rather than adding embedding columns to `Student` directly. |
| Extend `StudentFaceEmbedding` with download/batch columns | **Rejected** | `StudentFaceEmbedding` has a unique filtered index enforcing **one active row per student** (`IX_StudentFaceEmbedding_Student_Active` on `(StudentId, IsActive)` where `IsActive = true`). A job needs a row *per enrollment attempt per batch*, including failed download attempts that never even reach the embedding stage — forcing that history through a table designed around "the current vector" would either violate that invariant or require creating throwaway/inactive embedding rows for pure download failures, which is semantically wrong (there is no embedding to speak of yet). |
| **New `StudentEnrollmentJob` (+ `StudentEnrollmentBatch`)** | **Adopted** | Mirrors the existing, proven `AttendanceSession` (job/batch) ↔ `AttendanceRecognition` (per-item result) relationship shape exactly — `StudentEnrollmentBatch` is the "session," `StudentEnrollmentJob` is the "per-student result," and on success the job's terminal effect is to update the two *existing* tables (`Student.PhotoKey`/`PhotoUploadedUtc` and `StudentFaceEmbedding`), exactly like `ClassroomRecognitionPipeline` updates `AttendanceRecognition` without owning the embedding data itself. |

---

## 3. Table Design

### 3.1 `StudentEnrollmentBatch`

One row per SuperAdmin-initiated enrollment run, scoped to University + College + Batch(year).

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `uuid` | No (PK) | `Guid.NewGuid()`, matches `AttendanceSession.Id`/`AttendanceRecognition.Id` convention |
| `TenantId` | `integer` | No | College tenant this batch targets (SuperAdmin operates across tenants but each batch is single-tenant, matching how a single Excel import targets one tenant) |
| `UniversityId` | `integer` | No | Denormalized for filter/reporting without a join, mirrors how `College.UniversityId` already exists |
| `CollegeId` | `integer` | No | FK → `College.Id` |
| `AcademicYear` | `integer` | No | The `{year}` value used to build source URLs — **not** a new `Student`/`College` field; captured per-batch since `Student.Batch` is nullable and a SuperAdmin may need to run/re-run a specific year explicitly (see §5) |
| `Status` | `integer` (enum) | No | `Created=0, Running=1, Completed=2, PartiallyFailed=3, Cancelled=4` |
| `TotalStudents` | `integer` | No | Snapshot count at creation time |
| `PendingCount` / `DownloadingCount` / `ValidatingCount` / `EmbeddingCount` / `CompletedCount` / `FailedCount` / `RetryRequiredCount` / `CancelledCount` | `integer` | No, default 0 | Denormalized live counters, updated transactionally alongside each `StudentEnrollmentJob.Status` transition — avoids a `COUNT(*) ... GROUP BY` over potentially thousands of job rows on every dashboard poll (mirrors why `AttendanceSession` keeps `DetectedFaces`/similar counters rather than always aggregating `AttendanceRecognition`) |
| `CancellationRequestedUtc` | `timestamptz?` | Yes | Set when SuperAdmin clicks Cancel; background workers check this before claiming a new `Pending` job in this batch (see `docs/AI20_ENROLLMENT_BACKGROUND.md`) |
| `CreatedUtc` | `timestamptz` | No | |
| `CreatedBy` | `integer` | No | SuperAdmin `User.Id` — always populated (unlike `BaseEntity.CreatedBy` which is nullable), since this table has no anonymous/system-initiated path |
| `StartedUtc` | `timestamptz?` | Yes | First job claimed |
| `CompletedUtc` | `timestamptz?` | Yes | All jobs reached a terminal status |
| `RowVersion` | `bytea` | No | EF Core concurrency token, mirrors `AttendanceRecognition.RowVersion` — multiple workers increment counters concurrently |

**Indexes:**
- `IX_StudentEnrollmentBatch_Tenant_Status` on `(TenantId, Status)` — dashboard "active batches for this college" queries
- `IX_StudentEnrollmentBatch_University_College_Year` on `(UniversityId, CollegeId, AcademicYear)` — dashboard filter by University/College/Academic Year (per AI20.3's required filters)

**Foreign keys:**
- `CollegeId` → `College.Id` (Restrict — a batch is historical audit data; deleting a college should not cascade-delete enrollment history silently)
- `CreatedBy` → `User.Id` (Restrict)

---

### 3.2 `StudentEnrollmentJob`

One row per student per batch attempt.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `uuid` | No (PK) | |
| `TenantId` | `integer` | No | Denormalized from batch for direct tenant-scoped queries/RLS-style filters consistent with every other tenant-scoped table |
| `BatchId` | `uuid` | No | FK → `StudentEnrollmentBatch.Id` |
| `StudentId` | `integer` | No | FK → `Student.Id` |
| `Status` | `integer` (enum) | No | `Pending=0, Downloading=1, Downloaded=2, Validating=3, Embedding=4, Completed=5, Failed=6, RetryRequired=7, Cancelled=8` — exact vocabulary requested, plus `Cancelled` for batch-cancel propagation (§ AI20.1 lifecycle) |
| `FailureCategory` | `integer` (enum)? | Yes | `PhotoNotFound=1, AccessDenied=2, InvalidImage=3, CorruptImage=4, NoFaceDetected=5, MultipleFacesDetected=6, BlurRejected=7, LowResolutionRejected=8, StorageUploadFailed=9, EmbeddingEngineFailed=10, Timeout=11, Unknown=99` — mirrors `docs/AI20_PHOTO_IMPORT.md`'s failure taxonomy and `docs/AI20_ENROLLMENT_ENGINE.md`'s validation rejections; set only when `Status IN (Failed, RetryRequired)` |
| **Photo metadata** | | | |
| `SourceUrl` | `varchar(1000)` | No | Fully resolved download URL (audit trail — the base URL is configurable, see `docs/AI20_PHOTO_IMPORT.md`) |
| `PhotoKey` | `varchar(500)?` | Yes | Set once uploaded — same value as `Student.PhotoKey` on success (`students/{tenantId}/{studentId}`) |
| `ContentType` | `varchar(100)?` | Yes | e.g. `image/jpeg` as returned by the source, prior to WebP re-encoding |
| `ByteSize` | `integer?` | Yes | Downloaded payload size |
| `Checksum` | `varchar(64)?` | Yes | SHA-256 hex digest of the downloaded bytes — used for duplicate-detection/idempotency (see §4 and `docs/AI20_PHOTO_IMPORT.md`) and to detect "source photo changed since last successful enrollment" on a re-run |
| `ImageWidth` / `ImageHeight` | `integer?` | Yes | Decoded dimensions, post file-level validation |
| **Embedding metadata** | | | |
| `EmbeddingVersion` | `varchar(64)?` | Yes | Copy of the value written to `StudentFaceEmbedding.EmbeddingVersion` on success — lets a job row be correlated to the exact embedding it produced even if a later re-enrollment supersedes it (`StudentFaceEmbedding.IsActive` moves on, this job row remains historical truth) |
| `QualityScore` | `real?` | Yes | Numeric 0.0–1.0 composite score from `docs/AI20_ENROLLMENT_ENGINE.md`'s validation stage (detection confidence × resolution factor × blur-sharpness factor); **distinct from** `StudentFaceEmbedding.EmbeddingQuality` (which is the coarser `Unknown/Poor/Fair/Good/Excellent` enum) — this column is the finer-grained number the UI's "Quality Score" column/sort (AI20.3) needs |
| `StudentFaceEmbeddingId` | `uuid?` | Yes | FK → `StudentFaceEmbedding.Id`, set on success — direct traceability from a job to the exact embedding row it created, without relying on timestamp correlation |
| **Retry/failure** | | | |
| `RetryCount` | `integer` | No, default 0 | Automatic-retry attempts consumed (transient failures only, per `docs/AI20_ENROLLMENT_BACKGROUND.md`'s retry policy) |
| `LastError` | `varchar(1000)?` | Yes | Human-readable last failure message (exception message or a specific rejection reason string, e.g. `"Detected 3 faces; enrollment requires exactly 1."`) |
| `LastAttemptUtc` | `timestamptz?` | Yes | Updated on every transition, including retries — drives the recovery sweep's staleness check (`docs/AI20_ENROLLMENT_BACKGROUND.md`) |
| **Timestamps (per stage — supports the UI's per-student timeline, AI20.3 "Student Detail Screen")** | | | |
| `CreatedUtc` | `timestamptz` | No | Job row created (batch creation time) |
| `DownloadStartedUtc` | `timestamptz?` | Yes | |
| `DownloadedUtc` | `timestamptz?` | Yes | |
| `ValidationStartedUtc` | `timestamptz?` | Yes | |
| `ValidatedUtc` | `timestamptz?` | Yes | |
| `EmbeddingStartedUtc` | `timestamptz?` | Yes | |
| `CompletedUtc` | `timestamptz?` | Yes | Terminal (`Completed`, `Failed`, or `Cancelled`) timestamp |
| `RowVersion` | `bytea` | No | EF Core concurrency token — a single worker claims a job via an optimistic-concurrency `WHERE RowVersion = @expected` update (see `docs/AI20_ENROLLMENT_BACKGROUND.md` for the claim protocol) |

**Indexes:**
- `IX_StudentEnrollmentJob_Batch_Status` on `(BatchId, Status)` — the queue-poll query (`WHERE Status IN (Pending, RetryRequired) ORDER BY CreatedUtc`) and the dashboard's per-batch status breakdown
- `UX_StudentEnrollmentJob_Batch_Student` **unique** on `(BatchId, StudentId)` — one job per student per batch; a retry re-uses/updates the same row rather than inserting a duplicate (see §5)
- `IX_StudentEnrollmentJob_Tenant_Student` on `(TenantId, StudentId)` — "show this student's enrollment history across all batches" (Student Detail Screen, AI20.3)
- `IX_StudentEnrollmentJob_Status_LastAttempt` on `(Status, LastAttemptUtc)` — the stuck-job recovery sweep's query shape (`WHERE Status IN (Downloading, Validating, Embedding) AND LastAttemptUtc < cutoff`), mirroring `StuckAttendanceSessionRecoveryService`'s `(Status, StartedUtc)` query pattern

**Foreign keys:**
- `BatchId` → `StudentEnrollmentBatch.Id` (**Cascade** — deleting a batch's audit trail, if ever administratively purged, should remove its job rows; mirrors `AttendanceSession` → `AttendanceRecognition` cascade)
- `StudentId` → `Student.Id` (**Restrict** — a job row must never be orphaned by a student hard-delete without an explicit decision; `Student` uses soft-delete (`IsDeleted`) in practice, so this should not normally trigger)
- `StudentFaceEmbeddingId` → `StudentFaceEmbedding.Id` (**SetNull** — if the embedding row is ever superseded/deleted, the job's historical record should survive with a null pointer rather than being deleted itself)

---

## 4. Checksum / Duplicate Detection Design

`Checksum` (SHA-256 of downloaded bytes) serves two purposes, both **read-only decisions made by the pipeline, not enforced by a DB constraint** (a DB-level uniqueness on checksum would be wrong — two different students could coincidentally have visually different photos that hash differently, and re-running the *same* student's *same* photo should be idempotent, not rejected):

1. **Idempotency on re-run:** before re-downloading, the pipeline can compare the freshly computed checksum against the `Checksum` of the student's most recent `Completed` job (if any); if identical, skip the download+embedding work entirely and mark the new job `Completed` immediately (with `LastError` unset), avoiding redundant ONNX inference for students who haven't changed their photo since the last successful enrollment.
2. **Change detection:** if a SuperAdmin explicitly triggers "Regenerate" (AI20.3 "Bulk Regenerate") on an already-enrolled student, a differing checksum confirms the source photo genuinely changed and a fresh embedding is warranted; an identical checksum lets the UI surface "no change detected — regeneration skipped" instead of silently redoing unnecessary work.

This mirrors the *purpose* `StudentFaceEmbedding.PhotoVersion` (`bigint`) already serves for manual photo uploads (`EmbeddingPipeline` compares `PhotoVersion` to decide whether re-embedding is needed) — `StudentEnrollmentJob.Checksum` is the enrollment-side equivalent, using a content hash instead of a monotonic counter because the source is external and has no version number of its own.

---

## 5. Re-run / Retry Semantics (why `(BatchId, StudentId)` is unique, not just indexed)

A SuperAdmin retrying a failed student **updates the existing job row** (increment `RetryCount`, reset stage timestamps for the stage being retried, clear `LastError` on success) rather than inserting a new row, because:

- The unique index `UX_StudentEnrollmentJob_Batch_Student` on `(BatchId, StudentId)` makes "one job per student per batch" a database-enforced invariant, not just an application convention — a bug in the batch-creation or bulk-retry code cannot silently create duplicate/competing job rows for the same student within one batch.
- If a SuperAdmin wants a **fresh, separate history entry** (e.g., running the same College/Year batch again next month after new source photos are available), that is modeled as a **new `StudentEnrollmentBatch`**, not a new job row in the same batch — this keeps `(BatchId, StudentId)` uniqueness simple and keeps each batch's counters (`CompletedCount` etc.) meaningful without needing to filter out "superseded" job rows.

---

## 6. Audit Requirements

| Requirement | How satisfied |
|---|---|
| Who initiated a batch | `StudentEnrollmentBatch.CreatedBy` (FK → `User.Id`, always populated — SuperAdmin action, never anonymous) |
| When each stage happened for a given student | `StudentEnrollmentJob`'s per-stage timestamp columns (§3.2) |
| Why a job failed | `StudentEnrollmentJob.FailureCategory` + `LastError` (structured category for filtering/reporting, free-text for the exact message) |
| What was downloaded | `SourceUrl`, `ContentType`, `ByteSize`, `Checksum`, `ImageWidth`/`ImageHeight` — enough to answer "what exactly did we fetch and when" without re-downloading |
| Which embedding a job produced | `StudentFaceEmbeddingId` FK + `EmbeddingVersion` copy |
| Retry history | `RetryCount` + `LastAttemptUtc`; the *sequence* of individual retry attempts (not just the count) is expected to be covered by application-level structured logging (`docs/AI20_ENROLLMENT_BACKGROUND.md`'s telemetry/logging design), not a separate DB audit-log table — this mirrors how `RecognitionMediaService`'s upload attempts are logged (structured logs), not persisted as their own table rows, keeping the schema focused on *current-and-historical state* rather than a full event log |
| General platform audit trail | The existing `AuditEntry` table (`Abhyanvaya.Infrastructure` `AuditEntries` DbSet) is available if a cross-cutting audit hook already fires on entity changes; this design does not require adding `StudentEnrollmentBatch`/`StudentEnrollmentJob` to that mechanism specifically, since their own timestamp/status columns already provide the audit trail this feature needs — extending `AuditEntry` coverage to these two tables (if the existing mechanism is interceptor-based and applies uniformly) is a zero-cost bonus, not a hard requirement, and is left to implementation-time discovery of how `AuditEntry` is currently wired (out of scope for this document, which is schema design only) |

Neither table needs `IsDeleted` (`BaseEntity`-style soft delete): job/batch rows are immutable historical facts once terminal, and there's no user-facing "delete a batch" feature in this design — only Cancel (a status, not a deletion).

---

## 7. ER Diagram

```
┌─────────────────────────┐
│        College             │
│  Id (PK)                    │
│  TenantId                   │
│  Code                       │
│  UniversityId                │
└──────────────┬───────────┘
               │ 1
               │
               │ *
┌──────────────▼───────────────────────────┐
│         StudentEnrollmentBatch               │
│  Id (PK, uuid)                                │
│  TenantId                                     │
│  UniversityId                                  │
│  CollegeId (FK → College.Id, Restrict)         │
│  AcademicYear                                  │
│  Status                                        │
│  TotalStudents, PendingCount, ... (counters)   │
│  CancellationRequestedUtc                      │
│  CreatedUtc, CreatedBy (FK → User.Id, Restrict)│
│  StartedUtc, CompletedUtc                      │
│  RowVersion                                    │
└──────────────┬────────────────────────────────┘
               │ 1
               │
               │ *
┌──────────────▼────────────────────────────────────────┐        ┌────────────────────────┐
│              StudentEnrollmentJob                        │        │        Student            │
│  Id (PK, uuid)                                            │  *   1 │  Id (PK)                   │
│  TenantId                                                  │───────▶│  TenantId                  │
│  BatchId (FK → StudentEnrollmentBatch.Id, Cascade)          │        │  StudentNumber              │
│  StudentId (FK → Student.Id, Restrict)  ────────────────────────────▶│  Batch (int?)              │
│  Status, FailureCategory                                    │        │  PhotoKey, PhotoUploadedUtc │
│  SourceUrl, PhotoKey, ContentType, ByteSize, Checksum,        │        │  PhotoVerified              │
│    ImageWidth, ImageHeight                                    │        └────────────────────────┘
│  EmbeddingVersion, QualityScore,                                │
│    StudentFaceEmbeddingId (FK → StudentFaceEmbedding.Id, SetNull)│──┐
│  RetryCount, LastError, LastAttemptUtc                           │  │
│  CreatedUtc, DownloadStartedUtc, DownloadedUtc,                   │  │
│    ValidationStartedUtc, ValidatedUtc, EmbeddingStartedUtc,        │  │
│    CompletedUtc                                                    │  │
│  RowVersion                                                          │  │
│  UNIQUE (BatchId, StudentId)                                          │  │
└─────────────────────────────────────────────────────────────────────┘  │
                                                                            │ 0..1
                                                                            ▼
                                                          ┌────────────────────────────┐
                                                          │   StudentFaceEmbedding (EXISTING, unmodified)│
                                                          │  Id (PK, uuid)                │
                                                          │  TenantId, StudentId (FK)      │
                                                          │  EmbeddingVector (real[])      │
                                                          │  EmbeddingModel, EmbeddingVersion│
                                                          │  EmbeddingStatus, EmbeddingQuality│
                                                          │  EmbeddingDimension, PhotoVersion │
                                                          │  RetryCount, LastFailureUtc/Reason│
                                                          │  PhotoKey, GeneratedUtc, GeneratedBy│
                                                          │  IsActive                          │
                                                          │  UNIQUE FILTERED (StudentId, IsActive) WHERE IsActive│
                                                          └────────────────────────────┘
```

---

## 8. Full Column Summary Tables (for migration authoring reference)

### `StudentEnrollmentBatch`

```
Id                          uuid            PK
TenantId                    integer         NOT NULL
UniversityId                integer         NOT NULL
CollegeId                   integer         NOT NULL   FK -> College.Id (Restrict)
AcademicYear                integer         NOT NULL
Status                      integer         NOT NULL   default 0
TotalStudents                integer         NOT NULL   default 0
PendingCount                 integer         NOT NULL   default 0
DownloadingCount              integer         NOT NULL   default 0
ValidatingCount               integer         NOT NULL   default 0
EmbeddingCount                integer         NOT NULL   default 0
CompletedCount                integer         NOT NULL   default 0
FailedCount                   integer         NOT NULL   default 0
RetryRequiredCount            integer         NOT NULL   default 0
CancelledCount                integer         NOT NULL   default 0
CancellationRequestedUtc      timestamptz     NULL
CreatedUtc                    timestamptz     NOT NULL
CreatedBy                     integer         NOT NULL   FK -> User.Id (Restrict)
StartedUtc                    timestamptz     NULL
CompletedUtc                   timestamptz     NULL
RowVersion                     bytea           NOT NULL

INDEX IX_StudentEnrollmentBatch_Tenant_Status (TenantId, Status)
INDEX IX_StudentEnrollmentBatch_University_College_Year (UniversityId, CollegeId, AcademicYear)
```

### `StudentEnrollmentJob`

```
Id                        uuid            PK
TenantId                  integer         NOT NULL
BatchId                   uuid            NOT NULL   FK -> StudentEnrollmentBatch.Id (Cascade)
StudentId                 integer         NOT NULL   FK -> Student.Id (Restrict)
Status                    integer         NOT NULL   default 0
FailureCategory           integer         NULL
SourceUrl                 varchar(1000)   NOT NULL
PhotoKey                  varchar(500)    NULL
ContentType               varchar(100)    NULL
ByteSize                  integer         NULL
Checksum                  varchar(64)     NULL
ImageWidth                integer         NULL
ImageHeight               integer         NULL
EmbeddingVersion          varchar(64)     NULL
QualityScore              real            NULL
StudentFaceEmbeddingId    uuid            NULL       FK -> StudentFaceEmbedding.Id (SetNull)
RetryCount                integer         NOT NULL   default 0
LastError                 varchar(1000)   NULL
LastAttemptUtc            timestamptz     NULL
CreatedUtc                timestamptz     NOT NULL
DownloadStartedUtc        timestamptz     NULL
DownloadedUtc             timestamptz     NULL
ValidationStartedUtc      timestamptz     NULL
ValidatedUtc              timestamptz     NULL
EmbeddingStartedUtc       timestamptz     NULL
CompletedUtc              timestamptz     NULL
RowVersion                bytea           NOT NULL

UNIQUE INDEX UX_StudentEnrollmentJob_Batch_Student (BatchId, StudentId)
INDEX IX_StudentEnrollmentJob_Batch_Status (BatchId, Status)
INDEX IX_StudentEnrollmentJob_Tenant_Student (TenantId, StudentId)
INDEX IX_StudentEnrollmentJob_Status_LastAttempt (Status, LastAttemptUtc)
```

---

## Constraints Confirmed

No migrations were generated, and no database schema was modified to produce this document. All table/column designs above are proposals only.
