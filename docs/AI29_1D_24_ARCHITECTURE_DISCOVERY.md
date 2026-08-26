# AI29.1D.24 — Architecture Discovery (Course → Program Assignment UI)

**Prompt:** 1 (discovery only — no business logic in this document)  
**Date:** 2026-08-09  
**Constraint:** Reuse existing `Course.ProgramId → Program.Id`. No new Program entity, CourseProgram join, second assignment service, or UI-only relationship.

---

## 1. Existing authoritative model

| Element | Location | Notes |
|---------|----------|--------|
| `Program` | `Abhyanvaya.Domain/Entities/Academic/Program.cs` | Tenant-scoped; `Status` Active \| Inactive \| Archived |
| `Course.ProgramId` | `Abhyanvaya.Domain/Entities/Course.cs` | Optional FK; **sole** Course↔Program authority |
| `EnablePrograms` | `TenantAcademicConfiguration` | Feature gate; when false, assignments cleared / ignored |
| Hierarchy projection | `AcademicTreeService` | Nests courses under programs by `Course.ProgramId`; unassigned under synthetic node |

**Authority rule (ADL):** `Course.ProgramId` wins over any hierarchy projection. UI cascade treats hierarchy mismatch as diagnostic only (`academicCascade.ts`).

---

## 2. Existing APIs (smallest reuse surface)

### Preferred write: Program assign-course

| Item | Value |
|------|--------|
| Route | `POST /api/programs/assign-course` |
| Body | `AssignCourseProgramRequest` `{ courseId, programId? }` (`null` = unlink) |
| Policy (today) | `CanManagePrograms` / `Program.Manage` |
| Service | `IAcademicCatalogService.AssignCourseToProgramAsync` via `IAcademicStructureService` |
| Side effects | Domain events `CourseAssigned` / `CourseRemoved`; hierarchy + statistics cache invalidation |
| UI client | `assignCourseToProgram` in `abhyanvaya-ui/src/services/programService.ts` (**unused by pages**) |

### Secondary write: Course CRUD (optional ProgramId)

| Item | Value |
|------|--------|
| Routes | `POST /api/course`, `PUT /api/course` |
| DTOs | `CreateCourseRequest.ProgramId?`, `UpdateCourseRequest.ProgramId?` |
| Policy | `CanManageCourses` / `Setup.Courses.Manage` |
| Validation | `ResolveProgramIdAsync` — tenant Program, not Archived/inactive; forced null when Programs disabled |
| Gap | Update only applies ProgramId when `HasValue` (cannot unlink via `null`); **does not** invalidate hierarchy/statistics caches |

### Reads

- `GET /api/programs`, `GET /api/programs/{id}/course-count`, `GET /api/programs/statistics`
- `GET /api/course` returns `programId`
- `GET /api/academic-structure/configuration` → `enablePrograms`

---

## 3. Services

| Interface | Assigns Course→Program? |
|-----------|-------------------------|
| `IAcademicCatalogService` | **Yes** — `AssignCourseToProgramAsync` |
| `IAcademicStructureService` | **Yes** — facade to catalog |
| `IAcademicHierarchyService` | **No** — tree / counts / stats only |

---

## 4. Permissions

| Key | Policy | Role in assignment |
|-----|--------|--------------------|
| `Program.View` | `CanViewPrograms` | List programs for selector |
| `Program.Manage` | `CanManagePrograms` | Current gate on `assign-course` |
| `Setup.Courses.Manage` | `CanManageCourses` | Course Master page + Course CRUD ProgramId |

**Discovery finding:** Course Master users often have Courses.Manage but not Program.Manage. Prompt 4 should allow assignment via a policy that accepts **either** permission while keeping server validation in the catalog service.

---

## 5. Existing UI

| Component | Path | Gap |
|-----------|------|-----|
| Courses Master | `abhyanvaya-ui/src/pages/setup/CoursesPage.tsx` | Code/Name only — **no Program selector** |
| Programs setup | `pages/setup/ProgramsPage.tsx` | EnablePrograms toggle; no course assign UI |
| `AcademicUiContext` / `AcademicScopeSelector` | operational scope filters | Consume `Course.ProgramId`; not assignment |
| `setupService.createCourse` / `updateCourse` | omit `programId` | Need payload extension |

---

## 6. Validation, audit, cache

| Concern | Existing behavior |
|---------|-------------------|
| FluentValidation | `AssignCourseProgramRequestValidator` — CourseId &gt; 0; ProgramId null or &gt; 0 |
| Runtime | Tenant Course; EnablePrograms gate; Program exists & Active; reject Archived |
| Audit / events | `CourseAssigned` / `CourseRemoved` (log + metrics handlers) |
| Cache on assign-course | `IAcademicHierarchyCache.InvalidateHierarchyAsync` + `IAcademicStatisticsCache.InvalidateAsync` |
| Cache on Course CRUD | Master courses key only — **stale hierarchy risk** |

---

## 7. Files requiring modification (Prompts 2–4)

| File | Prompt | Change |
|------|--------|--------|
| `docs/AI29_1D_24_ARCHITECTURE_DISCOVERY.md` | 1 | This document |
| `abhyanvaya-ui/src/pages/setup/CoursesPage.tsx` | 2–3 | Program selector + persist |
| `abhyanvaya-ui/src/services/setupService.ts` | 3 | Optional `programId` on create/update |
| `Abhyanvaya.API/Common/AuthorizationPolicies.cs` | 4 | Assign policy (Courses **or** Program manage) |
| `Abhyanvaya.API/Program.cs` | 4 | Register policy |
| `Abhyanvaya.API/Controllers/ProgramsController.cs` | 4 | Use assign policy |
| `Abhyanvaya.API/Controllers/CourseController.cs` | 4 | Invalidate hierarchy/stats when ProgramId changes |
| Unit / UI tests | 2–4 | Regression coverage |

---

## 8. Are API changes necessary?

| Question | Answer |
|----------|--------|
| New assignment endpoint? | **No** — reuse `POST /api/programs/assign-course` |
| New entity / join table? | **No** |
| New service? | **No** |
| API hardening useful? | **Yes (additive)** — authorization OR for Course managers; CourseController cache invalidation when ProgramId set via Course CRUD |

**Smallest authoritative contract for UI save:**  
1. Persist Code/Name via `POST/PUT /api/course`.  
2. Persist Program via `POST /api/programs/assign-course` (server remains authoritative; UI never assumes success until 204).

---

## 9. Out of scope (hard constraints)

- Do not alter Course → Group → Semester → Subject Master shape.
- Do not introduce Section into Course Master.
- Do not duplicate Program resolver or hierarchy builder.
