# AI29.1D.24A Prompt 1 — Architecture Discovery

**Mode:** Discovery only — no production code changes in this prompt.  
**Out of scope:** APIs, database, Attendance, Scheduling, Sections, Allocation.

---

## 1. Flow A — Course Master Edit

| Concern | Location | Behavior today |
|---------|----------|----------------|
| Current Program source | `CourseRow.programId` from `GET /api/course` | Captured as `initialProgramId` on `openEdit` |
| Selected Program source | Local state `programId` (`UNASSIGNED_PROGRAM = 0` ⇒ null) | Select / **No Program** |
| Save handler | `save` → optional confirm → `persistCourse` | `CoursesPage.tsx` |
| API call | `updateCourse` / `createCourse` → `PUT/POST /api/course` | Server orchestrates Assign; **no** separate assign-course from UI |
| ProgramId contract | Presence-aware: value / null / omit | `buildCourseMasterSavePlan` + backend DTOs |
| Loading | `saving` | Disables Save; Confirm uses `confirming={saving}` |
| AcademicConfirmDialog | Already wired | Opens when `enablePrograms && editingId > 0 && initialProgramId > 0 && programId !== initialProgramId` |
| refreshCatalogs | **Not** called on Course save | `loadCourses` + `loadProgramContext` only |
| Cancel today | Closes confirm only | **Does not** restore `programId` to `initialProgramId` (gap for 24A) |

### Where confirmation should be inserted (Course Master)

**Gate:** immediately inside `save()`, after Code/Name validation, **before** `persistCourse()` / any HTTP call.

```
save()
  → validate code/name
  → shouldConfirmProgramReassignment(...) ? open AcademicConfirmDialog
  → else persistCourse() once

Confirm → persistCourse() once
Cancel  → close dialog; restore programId = initialProgramId; no API; no catalog refresh
```

---

## 2. Flow B — Program Master → Assign / Reassign Course

| Concern | Location | Behavior today |
|---------|----------|----------------|
| Target Program source | `viewRow` (Program being viewed) | Fixed for the View dialog |
| Selected Course source | `assignCourseId` + `allCourses` | Dropdown of courses not already on this Program |
| Current Program of selected Course | `CourseRow.programId` on selected course | Shown as “(reassign)” in menu when set |
| Save / assign handler | `doAssign` | Calls `assignCourseToProgram(courseId, viewRow.id)` immediately |
| Unassign | `unassignTarget` + AcademicConfirmDialog | Confirms unlink → `assignCourseToProgram(id, null)` |
| API | `POST /api/programs/assign-course` | Existing authoritative Assign; no new endpoint |
| Loading | `assigning` | Disables Assign / Unassign / Confirm |
| refreshCatalogs | After successful assign/unassign | `academicUi.refreshCatalogs()` |
| Reassign confirmation | **Missing** | `doAssign` does not confirm when moving B.Com from Commerce → Science |

### Where confirmation should be inserted (Program Master)

**Gate:** start of `doAssign()`, before `assignCourseToProgram`.

```
doAssign()
  → resolve selected course.currentProgramId
  → shouldConfirmProgramReassignment({
        currentProgramId: course.programId,
        requestedProgramId: viewRow.id,
        isExistingCourse: true,
        programsEnabled: true (this UI only when Programs enabled)
     })
  → if true: open “Change Course Program?” dialog; return
  → else: performAssign() once

Confirm → performAssign() once (existing API)
Cancel  → close dialog; no API; no refresh; no event
```

Same Program is already excluded from the available list (`coursesAvailableForProgramAssignment`) → no-op path without confirmation.

---

## 3. Shared building blocks (reuse)

| Asset | Path |
|-------|------|
| AcademicConfirmDialog | `abhyanvaya-ui/src/components/academic/AcademicConfirmDialog.tsx` |
| academicTouchButtonSx | `academicUiTokens` |
| assignCourseToProgram | `programService.ts` |
| Course save plan | `courseMasterPersistence.ts` |
| AcademicUiContext.refreshCatalogs | after Program Master assign only |

---

## 4. Decision matrix (to implement in Prompt 2)

| Case | Confirm? |
|------|----------|
| New Course | No |
| None → Commerce | No |
| Commerce → Commerce | No |
| Commerce → Science | **Yes** |
| Commerce → None | **Yes** |
| Science → Commerce | **Yes** |
| Science → None | **Yes** |
| None → None | No |
| Programs disabled | No |

---

## 5. Non-goals (confirmed)

- No API / DB / entity changes  
- No Attendance / Scheduling / Section / Allocation changes  
- No second assignment model  
- No `window.confirm` for Program reassignment (Archive/Delete on ProgramsPage still use `window.confirm` — out of 24A reassignment scope)

---

## 6. Files expected for later prompts

| Prompt | Artifact |
|--------|----------|
| 2 | Pure helper + unit tests (e.g. `programReassignmentConfirmation.ts`) |
| 3 | `CoursesPage.tsx` — helper + cancel restore |
| 4 | `ProgramsPage.tsx` — helper before assign |
| 5 | Dialog a11y polish if needed |
| 6 | Regression + `AI29_1D_24A_FINAL_VALIDATION.md` |
