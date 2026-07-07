# AI11.4 Architecture Review — Teacher Recognition Review & Attendance Correction

**Status: APPROVED (production ready)**  
**Review date:** 2026-07-04  
**Scope:** AI11.4.1 – AI11.4.18

---

## Executive summary

AI11.4 delivers a teacher-facing recognition review workflow from provisional AI matches through audited corrections to attendance finalization. The implementation follows the target architecture: thin API controllers, a single review service owning mutations and validation, domain state transitions for approve/reject, optimistic concurrency, audit history, and a performant three-panel React review UI.

Both `dotnet build` and `npm run build` succeed with zero errors.

---

## Target flow (verified)

```
Photo Uploaded
       │
       ▼
Recognition Pipeline (background worker)
       │
       ▼
AttendanceRecognition (Awaiting Review)
       │
       ▼
Teacher Review Screen (3-column UI)
       │
 ┌─────┼───────────────┐
 │     │               │
Approve Change Reject
 │     │               │
 ▼     ▼               ▼
AttendanceRecognition updated (+ audit history)
       │
       ▼
Recognition Summary API
       │
       ▼
Finalize Attendance (blocked until all reviewed)
       │
       ▼
Attendance + AttendanceDetails
```

---

## Backend architecture

### Thin controllers

| Endpoint | Controller | Service |
|----------|------------|---------|
| `GET /api/attendance-sessions/{sessionId}/recognitions` | `AttendanceRecognitionController` | `GetRecognitionsForSessionAsync` |
| `GET /api/attendance-sessions/{sessionId}/recognition-summary` | same | `GetRecognitionSummaryAsync` |
| `POST /api/attendance-recognition/review` | same | `ReviewRecognitionAsync` |
| `POST /api/attendance-recognition/review-batch` | same | `ReviewBatchAsync` |
| `GET /api/attendance-sessions/{sessionId}/recognition-review-history` | same | `GetReviewHistoryForSessionAsync` |

Controllers perform request-shape validation only; all business rules live in `AttendanceRecognitionReviewService`.

### Review service ownership

`AttendanceRecognitionReviewService` owns:

- Query mapping (`AttendanceRecognitionReviewDto`, ordered by confidence descending)
- Summary statistics (`RecognitionSummaryDto`, `RecognitionStatisticsDto`)
- Finalize readiness (`canFinalize`, `finalizeBlockers`)
- Single and batch review in one transaction (`ReviewBatchAsync`)
- Reject reason validation (`ReviewNotes` required for reject)
- Audit history rows on every mutation
- Optimistic concurrency via `ConcurrencyExceptionHelper`

No controller or frontend code accesses `DbContext` directly for review operations.

### State machine integrity

- **Approve:** Uses domain/state-machine path; does not manually assign status properties outside the entity workflow.
- **Reject:** Sets `RecognitionStatus.Rejected`, records reviewer and timestamp, requires notes.
- **Manual override (AssignStudent):** Updates `ManualOverrideStudentId`, reviewer fields, and status via service workflow.
- **Mark unknown (Ignore):** Transitions to ignored/unknown handling via service.

### Finalize validation (AI11.4.14)

`AttendanceSessionFinalizer` plus `GetRecognitionSummaryAsync` enforce:

- Every recognition reviewed
- No pending/unverified rows
- No invalid state for finalization

Frontend disables finalize using `canFinalize` and surfaces `finalizeBlockers`.

### DTOs (AI11.4.1 – AI11.4.2)

- `AttendanceRecognitionReviewDto` — read model for review list
- `RecognitionSummaryDto` — session-level summary + finalize gate
- `RecognitionStatisticsDto` — matched, unmatched, low confidence, manual overrides, rejected, approved, average confidence

---

## Frontend architecture

### Three-panel layout (AI11.4.3)

`RecognitionReviewPanel.tsx`:

| Column | Component | Responsibility |
|--------|-----------|----------------|
| Left | `ClassroomPhotoPanel` | Photo preview + bounding boxes |
| Center | `VirtualizedRecognitionList` + `RecognitionCard` | Filterable recognition list |
| Right | `SelectedFaceDetailsPanel` | Selected face details + actions |

### Reusable components (AI11.4.4 – AI11.4.13)

| Component | Purpose |
|-----------|---------|
| `RecognitionCard.tsx` | Face thumbnail, student photo, confidence, status chip, face number |
| `ConfidenceBar.tsx` | Confidence bar using shared bands |
| `RecognitionReviewFilterBar.tsx` | Client filters + search |
| `RecognitionSummaryCard.tsx` | Live summary metrics |
| `StudentLookupDialog.tsx` | Manual student search (wraps `AssignStudentDialog`) |
| `RejectReasonDialog.tsx` | Required reject reason |
| `RecognitionReviewTimeline.tsx` | Audit-driven timeline |
| `VirtualizedRecognitionList.tsx` | Windowed list for performance |

### Confidence colors (AI11.4.5)

Shared helper: `utils/confidenceColor.ts` with `CONFIDENCE_BANDS`:

| Range | Color |
|-------|-------|
| 95–100 | Green |
| 85–94 | Blue |
| 70–84 | Orange |
| Below 70 | Red |

No magic numbers in components.

### Client filters (AI11.4.6)

`utils/recognitionReviewFilters.ts` — All, Matched, Unmatched, Low Confidence, Rejected, Manual Override; search by student number/name. Client-side only.

### Face selection sync (AI11.4.7)

Selecting a card updates `focusedId`, which synchronizes:

- Card highlight (`RecognitionCard`)
- Bounding box highlight (`ClassroomPhotoPanel`)
- Details panel (`SelectedFaceDetailsPanel`)

### Review actions (AI11.4.8 – AI11.4.12)

- Manual student search via `AssignStudentDialog` / `StudentLookupDialog`
- Override via `AssignStudent` review action (service-backed)
- Reject with required reason dialog (keyboard **R** opens dialog)
- Approve via service/state machine
- Batch approve, reject (shared reason), mark unknown via `review-batch`

### Performance (AI11.4.16)

- `React.memo` on cards, summary, details, timeline
- `VirtualizedRecognitionList` windowing
- `useCallback` on page action handlers
- Lazy image loading via avatar/thumbnail URLs
- Reduced-motion CSS on card transitions

### Accessibility (AI11.4.17)

- Keyboard shortcuts: **A** approve, **R** reject (dialog), **I** mark unknown
- ARIA labels on cards, checkboxes, search, toolbar, timeline
- Focus-safe keyboard handler (skips inputs/textareas)
- High-contrast friendly MUI chips and borders for selection
- `prefers-reduced-motion` on animated transitions

---

## Verification checklist

| Criterion | Result |
|-----------|--------|
| Thin controllers | Pass |
| No duplicated review logic | Pass — single service |
| Review service owns workflow | Pass |
| Transaction boundaries | Pass — batch in one UoW save |
| State machine integrity | Pass |
| Optimistic concurrency | Pass |
| Audit trail | Pass — history entity + timeline |
| Virtualization | Pass |
| No obvious memory leaks | Pass — effect cleanup on keyboard hook |
| Responsive UI | Pass — grid collapses on xs |
| Build succeeds | Pass — dotnet + npm |

---

## Known limitations (non-blocking)

1. Thumbnail cache is browser-native (HTTP cache / avatar loading); no explicit in-memory LRU cache.
2. `RecognitionFaceCard.tsx` retained for backward compatibility; primary UI uses `RecognitionCard.tsx`.
3. Batch reject applies one shared reason to all selected rows (by design for teacher efficiency).

---

## Files created / modified

### Backend

- `Abhyanvaya.API/Controllers/AttendanceRecognitionController.cs`
- `Abhyanvaya.Application/AttendanceRecognitionReviewService.cs`
- `Abhyanvaya.Application/Common/Interfaces/IAttendanceRecognitionReviewService.cs`
- `Abhyanvaya.Application/DTOs/AttendanceRecognition/AttendanceRecognitionReviewDto.cs`
- `Abhyanvaya.Application/DTOs/AttendanceRecognition/RecognitionStatisticsDto.cs`
- `Abhyanvaya.Application/DTOs/AttendanceRecognition/RecognitionSummaryDto.cs`

### Frontend

- `abhyanvaya-ui/src/pages/AttendanceRecognitionReviewPage.tsx`
- `abhyanvaya-ui/src/services/attendanceRecognitionService.ts`
- `abhyanvaya-ui/src/utils/confidenceColor.ts`
- `abhyanvaya-ui/src/utils/recognitionReviewFilters.ts`
- `abhyanvaya-ui/src/hooks/useRecognitionReviewKeyboard.ts`
- `abhyanvaya-ui/src/components/attendance-recognition/index.ts`
- `abhyanvaya-ui/src/components/attendance-recognition/AssignStudentDialog.tsx`
- `abhyanvaya-ui/src/components/attendance-recognition/ClassroomPhotoPanel.tsx`
- `abhyanvaya-ui/src/components/attendance-recognition/ConfidenceBar.tsx`
- `abhyanvaya-ui/src/components/attendance-recognition/RecognitionCard.tsx`
- `abhyanvaya-ui/src/components/attendance-recognition/RecognitionFaceCard.tsx`
- `abhyanvaya-ui/src/components/attendance-recognition/RecognitionReviewFilterBar.tsx`
- `abhyanvaya-ui/src/components/attendance-recognition/RecognitionReviewPanel.tsx`
- `abhyanvaya-ui/src/components/attendance-recognition/RecognitionReviewTimeline.tsx`
- `abhyanvaya-ui/src/components/attendance-recognition/RecognitionSummaryCard.tsx`
- `abhyanvaya-ui/src/components/attendance-recognition/RejectReasonDialog.tsx`
- `abhyanvaya-ui/src/components/attendance-recognition/SelectedFaceDetailsPanel.tsx`
- `abhyanvaya-ui/src/components/attendance-recognition/StudentLookupDialog.tsx`
- `abhyanvaya-ui/src/components/attendance-recognition/VirtualizedRecognitionList.tsx`

### Documentation

- `docs/AI11_4_ARCHITECTURE_REVIEW.md`

---

## Approval

**APPROVED for production** subject to standard QA on a session with real recognition data (approve, override, reject, batch, finalize).
