# AI29.1D Prompt 18 — Permissions & Tenant Isolation

## Principle

- **Server authorization is authoritative.** UI permission checks only enable/disable controls and explain denied states.
- **Do not** invent client-side rules that conflict with API policies.
- **Do not** treat hidden buttons as security — every mutating call still goes through server policies + tenant scope.

## Permission catalog (existing keys)

| Capability | Permission keys |
|------------|-----------------|
| Programs / academic structure | `Program.View` (+ Create/Edit/Delete/Manage) |
| Sections | `Section.View/Create/Edit/Delete/AssignStudents/AssignFaculty` |
| Section lifecycle | `SectionLifecycle.View/Edit` |
| Section capacity | `Section.Capacity` |
| Merge / Split | `Section.Merge` / `Section.Split` |
| Faculty allocation | `Section.AssignFaculty` (+ `Section.View` for list) |
| Allocation | `Allocation.Run/Approve/Reject/Export`, `Allocation.Operations.View` |
| Allocation scenarios | `Allocation.Scenario.View/Create/Compare/Replay/Review/Archive` |
| Attendance | `Attendance.View` / `Attendance.Manage` |

Catalog source: `abhyanvaya-ui/src/auth/academicPermissionAccess.ts` (mirrors JWT claim keys already enforced by API policies).

## HTTP 401 / 403 UX

`getApiErrorMessage` (`utils/apiErrorMessage.ts`):

1. Prefer server response body (`message` / `title` / `detail` / string).
2. Empty **401** → session / re-auth copy.
3. Empty **403** → not-authorized copy (optional `forbiddenFallback` for domain context only).

Wired into: Sections, Allocation Context/Operations/Workspace, Faculty allocation + staff selector, Attendance mark/load students, AcademicUi hierarchy load, Academic context breadcrumb.

## UI components

| Component | Role |
|-----------|------|
| `PermissionAwareButton` | Disable action when JWT lacks key; tooltip notes server still enforces |
| `PermissionDeniedAlert` | Tab/panel denied state |

## Routes expanded

- `/setup/sections` — includes lifecycle, capacity, merge/split, readiness, allocation ops view.
- `/setup/academic/allocation-context` — Section.View **or** Allocation ops/run/scenario view.
- `/setup/academic/allocation/operations` — same family of allocation keys.

Attendance / Faculty Workspace routes remain gated by `Attendance.Manage` (unchanged server policy).

## Tenant isolation

No client-side tenant switching. APIs continue to use tenant-scoped JWT + existing middleware; UI surfaces 401/403 from those boundaries without local bypass.
