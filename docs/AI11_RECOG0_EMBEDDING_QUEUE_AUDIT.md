# AI11.RECOG.0 — Student Face Embedding Queue Audit

**Type:** Read-only investigation. No code modified.
**Data source:** live PostgreSQL database `abhyanvaya_db` (localhost:5432), queried directly against
the `Student` and `StudentFaceEmbedding` tables.
**Date:** 2026-07-05

---

## Headline result

> **No student profile photo has been processed by `StudentFaceEmbeddingBackgroundService`.**
> The `StudentFaceEmbedding` table contains **0 rows**. Of 300 active students, only **3** even have a
> profile photo, and none of them has an embedding job in any state.

| Metric | Value |
|--------|-------|
| Active (non-deleted) students | **300** |
| Students with a profile photo (`PhotoKey` populated) | **3** |
| Students without a photo | 297 |
| `StudentFaceEmbedding` rows (total) | **0** |
| Jobs started (Processing) | 0 |
| Jobs completed | 0 |
| Embeddings generated | 0 |
| Embeddings stored (active) | 0 |
| Failed jobs | 0 |

---

## How each audit column is determined

The embedding queue (`InMemoryStudentPhotoEmbeddingQueue`) is **in-memory and not persisted**, so
"queued" has no durable record. The only durable evidence of processing is the `StudentFaceEmbedding`
table, whose lifecycle is:

| Audit column | Durable source of truth |
|--------------|-------------------------|
| Photo Exists | `Student.PhotoKey` is non-null / non-empty |
| Embedding Job Queued | *Not persisted* (in-memory queue). Inferred from row existence — a row is only ever created once the worker **dequeues** a job |
| Embedding Job Started | A `StudentFaceEmbedding` row exists — `MarkProcessingAsync` (or `MarkPendingAsync`) inserts the row the moment the worker starts the job |
| Embedding Job Completed | Row reached a terminal state: `Completed (2)`, `Failed (3)`, or `Inactive (4)` |
| Embedding Generated | `array_length(EmbeddingVector) > 0` |
| Embedding Stored | `EmbeddingStatus = Completed (2)` with `IsActive = true` and a stored vector |
| Any Failed Jobs | `EmbeddingStatus = Failed (3)` or `RetryCount > 0` or `LastFailureUtc` set |

Because the table is empty, **every** derived column is `No` for **every** student — the worker never
even reached the `MarkProcessing` insert for anyone.

---

## Per-student audit

### Students that have a photo (3)

| Student Number | Photo Exists | Job Queued | Job Started | Job Completed | Embedding Generated | Embedding Stored | Failed Jobs |
|---|---|---|---|---|---|---|---|
| 105325405001 | Yes | No | No | No | No | No | No |
| 105325405002 | Yes | No | No | No | No | No | No |
| 105325405009 | Yes | No | No | No | No | No | No |

> These 3 students have a `PhotoKey` on record but **no `StudentFaceEmbedding` row exists** for them —
> their photos were never processed by the background service.

### All other students (297)

Every remaining active student has the uniform state below (no photo, therefore nothing downstream):

| Student Numbers | Photo Exists | Job Queued | Job Started | Job Completed | Embedding Generated | Embedding Stored | Failed Jobs |
|---|---|---|---|---|---|---|---|
| All 297 (e.g. `105325405003`–`105325405008`, `105325405010`–`105325405240`, `105325413001`–`105325413060`) | No | No | No | No | No | No | No |

*(Full per-student rows were verified for all 300 students; 297 are identical to the row above, so
they are collapsed here for readability.)*

### Failed jobs detail

**None.** No row has `EmbeddingStatus = Failed`, `RetryCount > 0`, or a `LastFailureUtc` — because no
job ever ran.

---

## Interpretation

1. **Coverage is effectively zero.** Only 3/300 students have a photo, and 0/300 have an embedding.
   The recognition matcher therefore has **no enrolled embeddings** to compare against.
2. **The worker never processed the 3 existing photos.** `MarkProcessingAsync` inserts a `Processing`
   row the instant a job is dequeued; the absence of *any* row proves no job for these students was
   ever dequeued/started. (If a provider had been missing, `MarkPendingAsync` would still have created
   a `Pending` row — there are none, so nothing ran at all.)

### Likely explanations (not fixed here — investigation only)

- **Photos set outside the upload path:** the 3 `PhotoKey` values were most likely written via
  seed/bulk import rather than the student-photo upload endpoint that enqueues an embedding job, so no
  job was ever queued.
- **Volatile in-memory queue:** `InMemoryStudentPhotoEmbeddingQueue` holds jobs only in process
  memory. Any job enqueued before an API restart is lost with no database trace.
- **Enrollment simply not run:** the face-enrollment step for this dataset has not been executed.

### Impact on recognition

With the SCRFD detection fix (AI11.FIX.1) in place, detection now finds faces correctly — but with
**0 stored student embeddings**, `ClassroomRecognitionPipeline` → `FaceMatcher` has nothing to match
against, so every detected face will resolve to **Unknown**. Populating student embeddings (upload
photos through the enqueuing path and let the worker run) is a prerequisite for recognition to produce
matches.

---

## Verification queries (read-only)

```sql
-- counts
select count(*) from "Student" where "IsDeleted" = false;                         -- 300
select count(*) from "Student"
  where "IsDeleted" = false and "PhotoKey" is not null and btrim("PhotoKey") <> ''; -- 3
select count(*) from "StudentFaceEmbedding";                                        -- 0

-- per-student roll-up
select s."StudentNumber",
       (s."PhotoKey" is not null and btrim(s."PhotoKey") <> '') as photo_exists,
       count(e."Id")                                            as embedding_rows,
       count(*) filter (where e."EmbeddingStatus" = 2)          as completed,
       count(*) filter (where e."EmbeddingStatus" = 3)          as failed,
       coalesce(max(array_length(e."EmbeddingVector", 1)), 0)   as max_vector_len
from "Student" s
left join "StudentFaceEmbedding" e on e."StudentId" = s."Id"
where s."IsDeleted" = false
group by s."StudentNumber", photo_exists
order by s."StudentNumber";
```

## Conclusion

`StudentFaceEmbeddingBackgroundService` has processed **zero** student photos. The embedding store is
empty, so face recognition currently has no enrollment data. This is an **enrollment/data-population
gap**, not a failure of the embedding jobs (there were none).
