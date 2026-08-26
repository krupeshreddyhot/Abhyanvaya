# AI29.1D — Architecture Discovery

**Phase:** Academic Structure & Section Operational UI Integration  
**Author role:** Senior UI Architect (under Chief Architect)  
**Status:** Discovery only — **no business logic implemented in this prompt**  
**Authority:** Architecture Documentation Library (ADL) + frozen AI29 → AI29.1C.5A platform

---

## 1. Purpose

AI29.1D is an **operational UI integration** phase. It must expose and compose existing academic, section, attendance, timetable, and allocation contracts in the React UI.

It must **not** invent parallel models, engines, resolvers, or governance.

Frozen base:

```
AI29 → AI29.1A → AI29.1A.5 → AI29.1A.6 → AI29.1A.7
  → AI29.1B → AI29.1B.5 → AI29.1B.7
  → AI29.1C → AI29.1C.5 → AI29.1C.5A  🔒 Allocation Platform
```

---

## 2. Explicit confirmation (mandatory)

### 2.1 Subject Master remains curriculum-only

**Confirmed:** Subject Master is **Course → Group → Semester** (plus TenantSubject / curriculum fields).

- `Subject` entity has `CourseId`, `GroupId`, `SemesterId` — **no `SectionId`**.
- Architecture Guard: Subject must not reference Section; Section must not own Subject.
- Docs: `docs/AI29_ACADEMIC_STRUCTURE_AND_SECTION_MANAGEMENT.md`, `AcademicArchitectureGuard.cs`.

### 2.2 Section is an operational grouping — not a curriculum level

**Confirmed:** Section is an **operational student grouping** under Course → Group → Semester.

- Used for student/faculty assignment, capacity/lifecycle, timetable combined classes, optional attendance roster filtering.
- It is **not** part of Subject Master and must never become a required curriculum cascade step for subjects.

---

## 3. Existing UI architecture

| Layer | Location / pattern |
|-------|-------------------|
| Router | `abhyanvaya-ui/src/routes/AppRoutes.tsx` + `ProtectedRoute.tsx` |
| Auth | JWT claims via `permissionKeys.ts` / `AuthContext` |
| Design system | MUI + `ThemeManager` / enterprise theme; AI31 dashboard tokens under `components/dashboards/` |
| Setup hub | `pages/setup/SetupHub.tsx` — Catalog entry for Programs, Courses, Groups, Semesters, Sections |
| Context shell | `ContextAwareLayout`, operational breadcrumbs (attendance / faculty) |

### Key routes today

| Path | Page | Notes |
|------|------|-------|
| `/attendance` | `AttendanceMarking.tsx` | Legacy cascade; soft timetable prefill |
| `/faculty` | `FacultyWorkspacePage.tsx` | Navigates to `/attendance` |
| `/setup/programs` | `ProgramsPage.tsx` | Optional Program CRUD + `enablePrograms` |
| `/setup/courses\|groups\|semesters` | Setup pages | Curriculum |
| `/setup/sections` | `SectionsPage.tsx` | Section ops (AI29/1B) |
| `/setup/academic/allocation-context` | `AllocationContextPage.tsx` | Context + run/simulate/approve |
| `/setup/academic/allocation/operations` | `AllocationOperationsPage.tsx` | Governance ops (1C.5/5A) |
| `/setup/scheduling/timetables*` | Timetable hub/designer | AI30 |

**Nav gap:** Main Catalog visibility does not always surface `Program.*` / `Section.*` alone; deep-link / Admin / SetupHub cards are primary entry.

---

## 4. Existing academic hierarchy

### Canonical shapes

| Programs | Hierarchy |
|----------|-----------|
| Disabled (default) | College → Course → Group → Semester → Subjects / Sections |
| Enabled | College → **Program** → Course → Group → Semester → Subjects / Sections |

Config: `TenantAcademicConfiguration.EnablePrograms` via `GET/PUT /api/academic-structure/configuration`.

Docs: `docs/AI29_1A_ACADEMIC_HIERARCHY.md`, `docs/AI29_1A_PROGRAM_MANAGEMENT.md`.

### Services (reuse)

- `IAcademicCatalogService`, `IAcademicTreeService`, `IAcademicHierarchyService`, `IAcademicStructureService`
- Versioned read model: `/api/v1/academic-structure/*`

---

## 5. Existing section model

| Entity | Role |
|--------|------|
| `Section` | Operational under AcademicYear/Course/Group/Semester |
| `StudentSection` | Assignment history (append; not overwrite) |
| `FacultySectionAssignment` | Faculty mapping |
| `TimetableSection` | Combined class bridge (many sections ↔ timetable entry) |
| `SectionGroup` / `SectionGroupMember` | First-class combined-section aggregate |
| `AttendanceSessionSection` | Additive attendance bridge (schema present; mark path largely unwired) |
| Lifecycle / capacity / merge / split / readiness / ops | AI29.1B / 1B.5 surfaces |

**UI today (`SectionsPage`):** list/CRUD, student/faculty assign, auto-allocate, transfer, lifecycle, capacity, merge/split, readiness, reports; link to Allocation Context.

**UI gap:** no dedicated SectionGroup management UI; ops hardening (`/api/sections/ops/*`) largely unused by UI.

---

## 6. Existing attendance flow

### Manual / Legacy (hard acceptance path)

```
Attendance → Course → Group → Semester → Subject → Period → students → mark
```

- No timetable required.
- Period is UI-local (`PERIOD_OPTIONS`); mark/edit APIs use `subjectId` + `date` + students.
- Master cascade: `/api/master/courses|groups|semesters|subjects`.
- Roster: `GET /api/attendance/students-for-marking` (optional `sectionId` / `sectionIds[]` on API; **UI does not send them**).
- Mark: `POST /api/attendance/mark` — **no required section**.

### Timetable-driven (soft prefill)

```
GET /api/attendance-resolution/current
  → AttendanceSessionResolver
  → Mode Legacy | Timetable
  → UI pre-fills Course/Group/Semester/Subject/Period (overridable)
```

- Resolver file: `Abhyanvaya.Application/Scheduling/Conflicts/AttendanceSessionResolver.cs`
- Additive Timetable enrichment: `SectionIds` / `SectionCodes` from `TimetableSections`.
- **UI type omits section fields; roster does not apply them** → combined class still loads full C/G/S cohort today.

### Faculty Workspace

- `GET /api/faculty/workspace/today|current-class|timetable` → navigate to `/attendance`.
- `FacultyClassDto` does **not** expose section IDs (dropped in mapping).

---

## 7. Existing timetable flow

- AI30 Timetable Designer / Hub / projections under `/setup/scheduling/timetables*`.
- Combined classes: `GET/PUT /api/timetable/{timetableId}/sections` (`TimetableSection`).
- Resolver reads published/locked timetable entries + time slots; failures degrade to Legacy.
- **Do not create a parallel timetable resolver.**

---

## 8. Existing allocation flow

Frozen platform contracts:

```
Academic scope → SectionAllocationContext → AllocationEngine
  → AllocationScenario → Simulate / Compare → Review → Approve (draft only)
```

| Surface | Routes (reuse) | UI |
|---------|----------------|-----|
| Platform 1B.7 | `/api/allocation/context\|readiness\|health\|validation\|snapshot\|…` | Allocation Context page |
| Engine 1C | `/api/allocation/run\|simulate\|approve\|compare\|history\|sandbox\|…` | Partial (context page) |
| Ops / governance 1C.5/5A | `/api/allocation/operations`, `/scenarios/*`, analytics, audit | Operations page |

**Critical distinction**

| API family | Live `StudentSection` writes? |
|------------|-------------------------------|
| `/api/student-sections`, `/transfer`, `/sections/auto-allocate` | **Yes** |
| `/api/allocation/run\|simulate\|approve` + governance approve | **No** (draft/scenario only) |

UI must keep this distinction visible (Preview/Simulation/Scenario ≠ live Apply).

---

## 9. Existing Program support

| Capability | Status |
|------------|--------|
| Program CRUD + archive + assign course | API + `/setup/programs` |
| `enablePrograms` tenant flag | Configuration API + Programs page toggle |
| Program in attendance cascade | **Absent** |
| Program in Sections/Allocation scope selectors | **Absent** (scope is Year→Course→Group→Semester) |
| Architecture rule | Attendance must not **require** Program |

---

## 10. Existing combined-section support

| Mechanism | Used by resolver? | UI |
|-----------|-------------------|-----|
| `TimetableSection` (multi-section map) | **Yes** (SectionIds/Codes enrichment) | Timetable map APIs; limited setup UX |
| `SectionGroup` / members | **No** (separate `/api/section-groups`) | **No UI** |
| `AttendanceSessionSection` | Schema/guard; not mark-path write | — |

**AI29.1D rule:** Combined-section logic stays server-side; React must not invent merge logic. Prefer existing `TimetableSection` / `SectionGroup` APIs.

---

## 11. Existing API contracts (summary)

### Reusable without redesign

- Master cascade: `/api/master/courses|groups|semesters|subjects`
- Programs / academic-structure / v1 hierarchy
- Sections CRUD, student/faculty, lifecycle, capacity, readiness, merge/split, reports
- Section groups: `/api/section-groups`
- Timetable section map: `/api/timetable/{id}/sections`
- Attendance mark/edit/students-for-marking/lock (+ optional section filters)
- Attendance resolution: `/api/attendance-resolution/current`
- Faculty workspace today/current-class/timetable
- Allocation platform + engine + operations/governance (frozen)

### Permissions (respect server-side)

- `Section.*`, `SectionLifecycle.*`
- `Allocation.Run|Approve|Operations.View|Scenario.*|Reject|Export`
- `Program.*`
- `Attendance.Manage` (marking / faculty workspace)

---

## 12. UI / API gaps identified (integration targets)

| # | Gap | Recommended approach |
|---|-----|----------------------|
| G1 | Attendance has no optional Section selector | Additive UI; call existing `sectionId`/`sectionIds` on students-for-marking; omit = legacy |
| G2 | UI resolution DTO drops `sectionIds`/`sectionCodes` | Extend **client type** + optionally apply to roster when Timetable mode |
| G3 | Faculty workspace drops sections | Additive DTO/UI only if needed; do not break navigate-to-`/attendance` |
| G4 | Program selector missing from Sections/Allocation when Programs enabled | Additive cascading selector; never required for attendance |
| G5 | No enterprise Allocation wizard (Scope→…→Approve) | Compose existing allocation APIs into workflow UI; no algorithms in React |
| G6 | Allocation criteria / student-number range UX incomplete | Wire to existing engine config/strategy contracts; server-side only |
| G7 | SectionGroup UI missing | Consume `/api/section-groups`; no client-side combine logic |
| G8 | SetupHub lacks Allocation cards | Navigation/integration only |
| G9 | Many allocation ops APIs unused (explain, audit, sandbox, save, governance detail) | Progressive UI exposure |
| G10 | `AttendanceSessionSection` write path incomplete | Prefer optional roster filter first; bridge writes only if additive contract strictly required |

---

## 13. Recommended integration points

1. **Sections UI (`/setup/sections`)** — enhance tabs (list, students, faculty, transfer/auto-allocate, lifecycle, capacity, merge/split, readiness, history); add Program selector when enabled.
2. **Allocation workflow UI** — wizard/shell over `allocationPlatformService` + `allocationOperationsService` (frozen engine/governance).
3. **AttendanceMarking** — keep full legacy cascade; add **optional** Section step; Timetable prefill may set section multi-select from resolver.
4. **Faculty Workspace** — continue deep-link to `/attendance`; additive section context only.
5. **SetupHub / routes / permissions** — surface Program/Section/Allocation consistently; respect JWT claims.
6. **Performance** — cascading queries, pagination, server filters; never load full student catalogs into browser.

---

## 14. Contracts that must remain unchanged

1. `IAttendanceSessionResolver` / `AttendanceSessionResolver` behavior (Legacy vs Timetable; fail → Legacy).
2. Attendance mark/edit/lock APIs and `MarkAttendanceRequest` (section not required).
3. `students-for-marking` semantics when section filters **omitted** = full C/G/S cohort.
4. Subject Master (Course + Group + Semester; no Section).
5. Manual Course → Group → Semester → Subject → Period workflow (always available).
6. Scheduling / Timetable engines (section bridge only).
7. Frozen Allocation Platform: `SectionAllocationContext` as sole engine input; approve → draft only; no UI algorithms.
8. Architecture Guard boundaries (Subject↛Section, Attendance↛Program requirement, engine↛student repositories).
9. Faculty Workspace must not become a second attendance engine (`ModifiesAttendanceApis => false`).

**Do not modify `AttendanceSessionResolver` unless an additive API contract is strictly required** (prefer client consumption of existing `SectionIds`/`SectionCodes`).

---

## 15. APIs that can be reused (preferred)

- All `/api/sections*` operational surfaces listed above  
- `/api/section-groups`  
- `/api/timetable/{id}/sections`  
- `/api/master/*` cascade  
- `/api/programs`, `/api/academic-structure`, `/api/v1/academic-structure/*`  
- `/api/attendance/*`, `/api/attendance-resolution/current`  
- `/api/faculty/workspace/*`  
- `/api/allocation/*` platform + engine + operations  

---

## 16. APIs that may require additive extension (if any)

Only if reuse proves insufficient after UI wiring:

| Possible additive need | Constraint |
|------------------------|------------|
| Faculty workspace DTO fields for `SectionIds`/`SectionCodes` | Additive; must not break existing clients |
| Marking helper to list sections for C/G/S scope for cascade UX | Prefer existing `GET /api/sections` filtered list |
| Allocation scenario criteria DTO exposure for student-number range in UI | Prefer existing pipeline config / run request fields; extend only if missing |
| `AttendanceSessionSection` persistence on session create | Only if product requires persisted session–section link beyond roster filter |

Default stance: **prefer UI composition of existing contracts over new APIs**.

---

## 17. Hard acceptance criteria (for later prompts / QA)

1. Faculty **without** timetable: Attendance → Course → Group → Semester → Subject → Period → mark succeeds.  
2. Same path with optional Section selected.  
3. Timetable-driven prefill path still works.  
4. Combined Section A + Section B via existing TimetableSections / SectionGroup contracts (server-side).  

Do not change production business behavior merely to satisfy tests.

---

## 18. Documentation corpus reviewed (ADL / AI29 stack)

Representative sources:

- `docs/AI29_ACADEMIC_STRUCTURE_AND_SECTION_MANAGEMENT.md`
- `docs/AI29_1A_ACADEMIC_HIERARCHY.md`, `AI29_1A_PROGRAM_MANAGEMENT.md`, `AI29_1A6_ARCHITECTURE_GUARD.md`
- `docs/AI29_1B_*`, `AI29_1B5_*`, `AI29_1B7_*`
- `docs/AI29_1C_*`, `AI29_1C5_*`, `AI29_1C_5A_*`
- `docs/AI30_PHASE2B_ATTENDANCE_RESOLUTION.md` (attendance resolution contract)
- Implementation: Sections/Allocation/Attendance/Faculty controllers + `abhyanvaya-ui` routes/pages/services

---

## 19. Discovery conclusion

AI29.1D should **integrate**:

- existing Section operational APIs into a richer Sections UX,
- existing Allocation Platform into an enterprise workflow UX,
- optional Program/Section into Attendance **without** removing legacy cascade,
- combined classes via existing TimetableSection / SectionGroup contracts,

…while treating AI29 → AI29.1C.5A as **frozen** and Subject Master as **Course → Group → Semester**.

**No business logic was implemented in this discovery prompt.**
