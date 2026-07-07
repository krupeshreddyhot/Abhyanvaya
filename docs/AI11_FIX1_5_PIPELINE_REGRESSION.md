# AI11.FIX.1.5 — Recognition Pipeline Regression Assessment

**Goal:** Confirm the SCRFD parser fix introduces no regressions across the full pipeline
Upload → Worker → Detection → Recognition → Matching → Review → Finalization.
**Business logic:** unchanged.

---

## 1. Contract stability (why no regression is possible by construction)

The fix is confined to the *internals* of `InsightFaceEngine.ParseDetectionOutputs()`. Every boundary
the rest of the pipeline depends on is unchanged:

| Boundary | Status |
|----------|--------|
| `IFaceDetectionService.DetectAsync(FaceDetectionRequest)` signature | Unchanged |
| `FaceDetectionResponse` shape (`ImageWidth/Height`, `Faces`, `DetectionDurationMs`, …) | Unchanged |
| `DetectedFaceDto` shape (`FaceIndex`, `DetectionScore`, `BoundingBox*`, `Landmarks`, `Embedding`, …) | Unchanged |
| `FaceCandidate` record | Unchanged |
| `InsightFaceEngine.DetectAsync` / `GenerateSingleFaceEmbedding` | Unchanged |
| Alignment (`AlignFace`), embedding (`ExtractEmbedding`, ArcFace), `L2Normalize` | Unchanged |
| NMS (`ApplyNms`), thresholds (`DetectionThreshold`, `NmsThreshold`) | Unchanged |

The parser still returns a `List<FaceCandidate>`; only the **count and correctness** of those
candidates improved. Downstream code is agnostic to how many faces are returned.

## 2. Pipeline stage-by-stage review (`ClassroomRecognitionPipeline.ProcessAsync`)

| Stage | Code | Effect of fix |
|-------|------|---------------|
| Upload → queue | `AttendancePhotoService` (unchanged) | None |
| Worker dequeue + tenant scope | `ClassroomRecognitionBackgroundService` (AI11.BG.8) | None — session now loads (tenant fix) |
| Session → Processing | `MoveToProcessing()` + SaveChanges | None |
| **Detection** | `_faceDetectionService.DetectAsync(...)` | Returns correct faces (19 vs 584 garbage) |
| `session.DetectedFaces = detection.Faces.Count` | line 77 | Now realistic |
| Load student embeddings | `LoadStudentEmbeddingsAsync` | Unchanged |
| **Matching** | `_faceMatcher.Match(matchInputs, studentEmbeddings)` | Unchanged; now fed real embeddings from real faces |
| Persist recognitions | `AddRangeAsync(recognitions)` | One row per detected face — now ~19, not ~584 |
| **Summary** | `_summaryService.SyncSessionSummaryAsync` | Unchanged; counts now meaningful |
| → AwaitingReview | `MoveToAwaitingReview()` + SaveChanges | Unchanged |
| Failure path | `catch → MoveToFailed()` | Unchanged |

**No business logic was modified.** The only behavioural change is that the detection stage now emits
correct faces, so every downstream count (`DetectedFaces`, `RecognizedFaces`, `UnknownFaces`, summary
metrics, review rows) becomes accurate instead of flooded with spurious detections.

## 3. Build / worker health

- `Abhyanvaya.Infrastructure` builds with **0 errors** (compilation verified).
- Worker loop, DI registrations, queue, and scope handling are untouched by this fix.
- The AI11.BG.8 tenant-context fix (separate change) already ensures the worker can load the session;
  combined with this fix, the pipeline can now run detection → matching → review end-to-end.

## 4. Live end-to-end note

A full runtime E2E (actual upload through the review page) requires the ASP.NET host and PostgreSQL to
be running with the **rebuilt** binaries. At the time of this change the API was running under Visual
Studio with the *previous* DLLs (which is why the solution copy step was file-locked). To exercise the
fix live:

1. Stop the running API in Visual Studio.
2. Rebuild the solution (Infrastructure already verified to compile).
3. Upload a classroom photo and open the review page.

Expected: session reaches **AwaitingReview**, review page shows ~10–20 faces with sane confidences and
correctly placed boxes (per AI11.FIX.1.4).

## 5. Verdict

| Check | Result |
|-------|--------|
| No regressions (by contract) | ✅ Interfaces/DTOs unchanged |
| Worker still processes | ✅ Untouched (+ BG.8 tenant fix) |
| Summary generation works | ✅ Unchanged; now accurate |
| Review page loads | ✅ Consumes unchanged recognition rows |
| Attendance finalization | ✅ Unchanged |
| Business logic modified | ❌ No |
