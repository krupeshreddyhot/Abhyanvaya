# AI11 UI Architecture Review

**Date:** 28 Jun 2026  
**Scope:** Attendance page AI Photo mode (AI11.1)  
**Verdict:** **Approved for AI11.2** (upload and session creation)

---

## Overview

The AI11.1 milestone establishes a frontend-only foundation for AI Photo Attendance on the existing Attendance page. Class context remains immutable; runtime workflow state is isolated in `AiAttendanceState`. Shared workflow types prevent duplication across future AI pages.

---

## Component Architecture

```
AttendanceMarking
├── AttendanceContext (immutable props via useMemo)
└── AiAttendancePanel
    ├── AiAttendanceState (React useState)
    ├── SessionDashboardCard × 4
    ├── SessionTimer
    ├── AttendanceContextCard
    ├── AiWorkflowStepper
    ├── WorkflowPlaceholderSection × 4
    └── RecognitionProgressSummary
```

### Shared Components (reusable in AI11.2+)

| Component | Location | Purpose |
|-----------|----------|---------|
| `AIStatusChip` | `components/common/` | Unified status rendering |
| `SessionTimer` | `components/common/` | Elapsed time during processing |
| `SessionDashboardCard` | `components/attendance/` | Compact session metric cards |
| `RecognitionProgressSummary` | `components/attendance/` | Face detection/match/review stats |
| `AiWorkflowStepper` | `components/attendance/` | Responsive workflow visualization |
| `WorkflowPlaceholderSection` | `components/attendance/` | Reserved workflow areas |
| `AttendanceContextCard` | `components/attendance/` | Two-column class context |

---

## Type Separation

### AttendanceContext (immutable class context)

- Course, Group, Semester, Subject, Period, Attendance Date
- Display names derived from dropdown data in `AttendanceMarking`
- No runtime workflow fields

### AiAttendanceState (mutable runtime state)

- Session IDs, progress, workflow step, status, image URL
- Face counts (detected, matched, reviewed)
- Managed locally in `AiAttendancePanel` via `useState(createInitialAiAttendanceState)`

### aiWorkflow.ts (shared enums)

- `AIWorkflowStep`: Upload, Detect, Match, Review, Finalize
- `AIStatus`: Ready, Uploading, Processing, Matching, AwaitingReview, Completed, Failed, Cancelled, Pending, NotStarted, NotCreated
- `AI_STATUS_LABELS`, `AI_WORKFLOW_STEP_SEQUENCE`, `getWorkflowStepIndex`

---

## Constants

`attendanceConstants.ts` contains UI-only configuration:

- `PERIOD_OPTIONS`
- `ATTENDANCE_METHOD_OPTIONS`
- `AI_ATTENDANCE_WORKFLOW_STEPS`
- `AI_WORKFLOW_PLACEHOLDER_SECTIONS`

Status labels and workflow step types live in `aiWorkflow.ts` — not duplicated in constants.

---

## Review Checklist

| Check | Result |
|-------|--------|
| No duplicated constants | Pass — single source in `aiWorkflow.ts` and `attendanceConstants.ts` |
| No duplicated interfaces | Pass — `AttendanceContext` vs `AiAttendanceState` separated |
| No dead components | Pass — `SessionInfoRow` removed; logic extracted to dedicated components |
| No unnecessary props | Pass — `AiAttendancePanel` accepts single `context` prop |
| No hardcoded colors | Pass — theme tokens (`primary.main`, `success.main`, `text.disabled`, Chip colors) |
| No duplicated icons | Pass — icons defined once per component; dashboard icons scoped to panel |
| No duplicate status rendering | Pass — all statuses via `AIStatusChip` |
| No React anti-patterns | Pass — state initialized via factory; `useMemo` for derived accuracy |
| Proper component separation | Pass — panel orchestrates; presentation in child components |

---

## Responsive Behavior

| Breakpoint | Session Cards | Context | Stepper |
|------------|---------------|---------|---------|
| Desktop (md+) | 4 × 1 row | 2 columns | Horizontal |
| Tablet (sm) | 2 × 2 | 2 columns | Horizontal |
| Mobile (xs) | 1 column | Stacked | Vertical |

---

## Current Defaults (AI11.1)

| Field | Value |
|-------|-------|
| Workflow step | Upload (active, blue) |
| Status | Ready |
| Attendance session | Not Created |
| Recognition session | Not Started |
| Session timer | 00:00:00 (no auto-start) |
| Face counts | 0 / 0 / 0 |

---

## AI11.2 Readiness

The UI is prepared for:

1. **Upload Area** — replace upload placeholder; wire `uploadProgress`, `uploadedImageUrl`
2. **Session creation** — populate `attendanceSessionId`, `recognitionSessionId`; update dashboard chips
3. **Processing** — set `AIStatus.Processing`, pass `startTime` to `SessionTimer`
4. **Recognition** — update `RecognitionProgressSummary` from API responses
5. **Stepper progression** — advance `aiState.workflowStep` as pipeline stages complete

No backend APIs are called in AI11.1. No behavior changes required before AI11.2 implementation.

---

## Files in AI11.1 Module

```
src/types/attendanceContext.ts
src/types/aiWorkflow.ts
src/types/aiAttendanceState.ts
src/constants/attendanceConstants.ts
src/utils/authDisplay.ts
src/pages/AttendanceMarking.tsx
src/components/common/AIStatusChip.tsx
src/components/common/SessionTimer.tsx
src/components/attendance/AiAttendancePanel.tsx
src/components/attendance/SessionDashboardCard.tsx
src/components/attendance/AttendanceContextCard.tsx
src/components/attendance/AiWorkflowStepper.tsx
src/components/attendance/WorkflowPlaceholderSection.tsx
src/components/attendance/RecognitionProgressSummary.tsx
```

---

## Approval

The AI11 UI architecture is clean, extensible, and ready for AI11.2 upload and session integration.

**Status: APPROVED**
