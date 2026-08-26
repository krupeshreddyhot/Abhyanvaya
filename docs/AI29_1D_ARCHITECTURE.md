# AI29.1D — Architecture

Enterprise academic / attendance / allocation architecture for Abhyanvaya.  
UI consumes API and Application contracts; Domain services remain authoritative.

## Layering

```
UI
 ↓
API / Application Contracts
 ↓
Domain Services
```

**Hard rule:** The UI must not access EF Core, `DbContext`, database tables, or allocation / scheduling / attendance persistence entities. It must not calculate authoritative capacity or allocation scores, resolve timetable sessions, implement attendance eligibility, SectionGroup resolution, lifecycle transitions, or governance rules.

Compliance gate: `GET /api/v1/academic-structure/architecture/ai29-1d-report`  
Statuses: `FULLY_VERIFIED` | `PARTIALLY_VERIFIED` | `FAILED`  
Snapshot: `docs/architecture/AI29_1D_architecture_compliance.json`

## Academic hierarchy

| Mode | Shape |
|------|--------|
| Programs **disabled** (default) | College → Course → Group → Semester → Subjects / Sections |
| Programs **enabled** | College → **Program** → Course → Group → Semester → Subjects / Sections |

- **Subject Master** = Course → Group → Semester → Subject.
- **Section** is an operational student grouping under Academic Year + Course + Group + Semester.
- **Section is an operational student grouping and is not part of Subject Master.**
- Subjects have no `SectionId`. Changing Section must not clear or redefine Subject.

Supporting aggregates:

| Aggregate | Role |
|-----------|------|
| `StudentSection` | Student ↔ section membership |
| `FacultySectionAssignment` | Faculty ↔ section |
| `SectionGroup` / members | Combined-section membership (same C/G/S/AY) |
| `TimetableSection` | Many sections ↔ one timetable entry |

## Program feature flag

- Stored on `TenantAcademicConfiguration.EnablePrograms` (default `false`).
- API: `GET/PUT /api/v1/academic-structure/configuration`.
- When enabled, Program mode is active even with zero Programs; Course options fail-closed until a Program is selected (Prompts 4A/4B).
- Attendance must **not** require Program.

## Section semantics

| Concern | Owner |
|---------|--------|
| Curriculum / Subject Master | Course + Group + Semester (+ Subject) |
| Operational population | Section (+ optional multi-select / TimetableSections) |
| Combined class | Server `SectionGroup` / `TimetableSections` — no parallel UI model |

Section is **optional** for attendance mark/edit and roster queries.

## Subject Master semantics

- Cascade keys subjects by Course + Group + Semester only (`academicCascade.ts` / catalog APIs).
- Subject Master is independent of Section selection and of Program (Program only filters Courses when enabled).

## Attendance architecture

| Path | Authority |
|------|-----------|
| Timetable-driven | Single `AttendanceSessionResolver` via `GET /api/attendance-resolution/current` |
| Manual / legacy | Course → Group → Semester → Subject → Period; Section optional |
| Save scope | `AttendanceSaveScope` + `AttendanceSectionScope` — atomic server reject |

Timetable is never mandatory. Write path does not re-invoke the resolver.

## Allocation & governance

- Engine, capacity, scoring, lifecycle, and governance remain server-side (`AllocationEngine`, `ISectionCapacityEngine`, `AllocationScenarioLifecycleService`, `IAllocationGovernanceService`).
- UI wizard (`EnterpriseAllocationWorkspace`) guides Scope → Population → Strategy → Capacity → Preview → Simulation → Scenario → Review → Approve.
- Approve is **draft/scenario** approval, not a silent live `StudentSection` rewrite unless using explicit live ops APIs.

## Security

- JWT + permission policies; UI gating ≠ authorization.
- Attendance writes: unauthorized section/student ⇒ entire request rejected.
- Faculty assign: `FacultySectionAssignmentAuthorization`.
- Operational breadcrumb: `CanViewAcademicOperationalContext` (OR of consumer view permissions — not Program write).
- No client-side tenant switching (Prompt 18).

## Performance

- Cascading queries, scoped caches, AbortSignal cancellation, pagination/windowing.
- Allocation sends server `populationSelection` criteria — not browser-built full student id dumps.
- See `AI29_1D_PROMPT_19_UI_PERFORMANCE.md` and `AI29_1D_UI_INTEGRATION.md`.

## Related docs

| Doc | Focus |
|-----|--------|
| `AI29_1D_UI_INTEGRATION.md` | Shared UI context, APIs, responsive, security UX |
| `AI29_1D_ATTENDANCE_INTEGRATION.md` | Marking, save scope, timetable/manual |
| `AI29_1D_SECTION_ALLOCATION_UI.md` | Allocation + faculty allocation UI |
| `AI29_1D_COMBINED_SECTION_UI.md` | Combined operational class |
| `AI29_1D_TEST_STRATEGY.md` | Suites and regression inventory |
| `AI29_1D_IMPLEMENTATION_SUMMARY.md` | End-to-end delivery summary |
