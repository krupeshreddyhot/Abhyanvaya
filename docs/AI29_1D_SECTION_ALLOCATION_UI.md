# AI29.1D — Section Allocation UI

UI for student section allocation (enterprise wizard) and faculty ↔ section assignment.  
**Section is an operational student grouping and is not part of Subject Master.**

## Surfaces

| Surface | Location |
|---------|----------|
| Sections admin | `/setup/sections` |
| Student Allocation tab | `EnterpriseAllocationWorkspace` |
| Faculty Section Allocation | `FacultySectionAllocationPanel` |
| Allocation Context | `/setup/academic/allocation-context` |
| Allocation Operations / governance | `/setup/academic/allocation/operations` |

## Allocation workflow (UI)

Guided steps over existing server APIs:

1. **Scope** — Academic Year + Course + Group + Semester (+ Program when enabled).
2. **Population** — Filters against `SectionAllocationContext` / `populationSelection` (server criteria, not browser id dumps).
3. **Strategy** — Catalog from engine pipeline config (range, last-3 digits, alphabetical, gender, merit, …).
4. **Capacity** — Consume `/api/sections/capacity/*`; UI does not invent authoritative capacity math.
5. **Preview** — Engine scenario/result display (draft).
6. **Simulation** — Simulate lifecycle / audit.
7. **Scenario** — Create/manage draft scenario.
8. **Review → Approve / Reject / Archive** — Governance lifecycle; server `canApprove` and stale/checksum/concurrency flags are authoritative.

Approve = **scenario/draft approval**, not an implicit live rewrite of all `StudentSection` rows. Live membership uses explicit ops (`/api/student-sections`, transfer, auto-allocate) when intended.

## Governance workflow

| Action | Contract family |
|--------|-----------------|
| Review | `/api/allocation/scenarios/{id}/review` |
| Approve / Reject | `.../approve`, `.../reject` |
| Archive | `.../archive` |
| Replay / Compare | existing compare/replay endpoints |
| Stale / checksum / concurrency | Server flags — UI surfaces only |

Lifecycle transitions are implemented only in `AllocationScenarioLifecycleService` / governance services — not in React.

## Faculty section allocation UI

- Lists existing `FacultySectionAssignment` via `/api/faculty-sections`.
- Enriches subject names from Subject Allocations and combined labels from SectionGroups.
- Staff selection via enterprise `FacultyStaffSelector` (not raw Staff Id entry).
- Combined teaching shows as one operational class label where SectionGroup applies (15A Prompt 8).
- Create assignment authorized server-side (`FacultySectionAssignmentAuthorization`).

Timetable attendance still uses StaffId + TimetableSections; faculty-section panel does not invent a second timetable model.

## API contracts consumed

| Area | Endpoints |
|------|-----------|
| Platform | `/api/allocation/context`, readiness, health, snapshot, validation |
| Engine | `/api/allocation/run`, `simulate`, `approve` (engine), compare, history, sandbox, catalogs |
| Governance | scenarios review/approve/reject/archive/replay, operations, audit |
| Sections / capacity | `/api/sections`, `/api/sections/capacity/*` |
| Live ops | `/api/student-sections`, transfer, auto-allocate |
| Faculty | `/api/faculty-sections`, staff search |
| Combined | `/api/section-groups` |

## Additive / hardened contracts

- `populationSelection` and allocation scope hardening (Prompt 10A).
- Faculty assign authorization (15A Prompt 7).
- Combined faculty display composition (15A Prompt 8) — no new FacultySection entity.

## Backward compatibility

- Existing allocation engine and governance APIs remain the source of truth.
- UI is additive composition; no parallel scoring/capacity/governance engines.
- Subject Master cascade unchanged by Section allocation UI.

## Security

- Permissions: `Allocation.Run/Approve/Reject/Export`, scenario ops, `Section.*`, faculty assign keys.
- UI buttons disable on missing JWT claims; server re-validates every mutation.

## Performance

- Windowed population / preview row caps.
- Debounced staff search with AbortSignal.
- Single faculty-sections fetch for the panel (avoid N+1).
- Cascading academic options via `AcademicUiContext`.

## Responsive behavior

- Stepper becomes scrollable on xs (Prompt 17).
- Scope toolbar sticky; tables scroll inside panels.
- Touch-friendly assign controls on tablet/mobile.
