# AI29.1D Prompt 16A — Academic Context Breadcrumb Authorization & Consistency Hardening

Hardens Prompt 16 without changing the academic hierarchy model, Subject Master, Section domain, Attendance, Timetable, or Allocation engines.

## OBJECTIVE 1 — Permission

| Item | Detail |
|------|--------|
| Policy | `AuthorizationPolicies.CanViewAcademicOperationalContext` |
| Endpoint | `GET /api/v1/academic-structure/breadcrumb/context` |
| Controller | `AcademicOperationalContextController` (not class-level `CanViewPrograms`) |

**Allowed via existing consumer permissions (OR):**

- `Attendance.View` / `Attendance.Manage`
- `Section.View` / `Section.AssignFaculty` / `SectionLifecycle.View`
- `Scheduling.Timetable.View` / `Scheduling.Timetable.Manage` / `Scheduling.View` / `Scheduling.Manage`
- `Allocation.Run` / `Allocation.Operations.View` / `Allocation.Scenario.View`
- `Program.View` (still allowed, **not required**)

**Not granted:** `Program.Create` / `Program.Edit` / `Program.Delete` / `Program.Manage`.

Catalog helper: `AcademicOperationalContextAccess` (Application) + UI `AcademicPermissionAccess.operationalContext`.

### Verify

| Actor | Result |
|-------|--------|
| Faculty with `Attendance.View`, no `Program.View` | **200** when context valid |
| User with none of the allowed keys | **403** |

Server remains authoritative.

## OBJECTIVE 2 — Context consistency

Before compose, `AcademicOperationalContextValidator` checks IDs against **`IAcademicTreeService` only**:

| Rule | Check |
|------|--------|
| Program → Course | Course path contains Program |
| Course → Group | Group path contains Course |
| Course + Group → Semester | Semester path contains Course and Group when supplied |
| Semester → Section | Each section path contains Semester (+ Course/Group when supplied) |
| Course + Group + Semester → Subject | Subject path contains those ancestors when supplied |
| Combined sections | All section IDs share the same Course\|Group\|Semester scope key |

On failure:

- do **not** compose a misleading trail;
- API returns **400** `{ message: "..." }`;
- service logs a warning via existing academic breadcrumb logger/telemetry.

## OBJECTIVE 3 — Legacy behavior

Unchanged: Subject Master, Section domain, Attendance write/read rules, Timetable, Allocation, hierarchy structure. Existing node breadcrumbs under `AcademicStructureV1Controller` still use `CanViewPrograms`.

## Tests

`AI29_1D_Prompt16A_AcademicContextBreadcrumbHardeningTests` — cases 1–10 (+ catalog / empty invalid outcome).

Filter: `dotnet test --filter FullyQualifiedName~AI29_1D_Prompt16A`
