# AI29.1D — UI Integration

How the React UI integrates with AI29.1D academic, attendance, and allocation contracts without owning business authority.

## Shared academic context

| Piece | Path / role |
|-------|-------------|
| `AcademicUiContext` | Canonical selection state + cascading option loads |
| `academicCascade.ts` | Client-side filter helpers aligned with server hierarchy |
| `AcademicContextBreadcrumb` | Trail from `GET .../breadcrumb/context` |
| `AcademicOperationalPageShell` / toolbar / panels | Prompt 17 chrome (AI31 tokens — no new design system) |

**Section is an operational student grouping and is not part of Subject Master.**  
Subject options stay keyed by Course + Group + Semester; Section changes must not reset Subject.

## Program feature flag (UI)

- Read from academic configuration (`EnablePrograms`).
- When enabled: Program selector active; Course list fail-closed until Program selected.
- When disabled: Program omitted from cascade and breadcrumb.
- Attendance screens must work with Program omitted.

## API contracts consumed

### Academic structure (v1)

| Method | Path | Use |
|--------|------|-----|
| GET/PUT | `/api/v1/academic-structure/configuration` | Program flag / tenant academic config |
| GET | `/api/v1/academic-structure/tree` (and related catalog) | Hierarchy options |
| GET | `/api/v1/academic-structure/breadcrumb/context` | Operational breadcrumb (Prompt 16/16A) |
| GET | `/api/v1/academic-structure/architecture/ai29-1d-report` | Architecture compliance (Prompt 21/21A) |

### Attendance

| Method | Path | Use |
|--------|------|-----|
| GET | `/api/attendance-resolution/current` | Timetable/manual session resolution |
| GET | `/api/attendance/students-for-marking` | Roster (+ optional section scope) |
| POST | `/api/attendance/mark` | Mark (optional `sectionId` / `sectionIds`) |
| PUT | `/api/attendance/edit` | Edit (same optional scope) |

### Sections / faculty / combined

| Method | Path | Use |
|--------|------|-----|
| CRUD | `/api/sections`, capacity helpers | Section admin |
| GET/POST | `/api/faculty-sections` | Faculty ↔ section assignments |
| GET | `/api/section-groups` | Combined membership display |
| GET/PUT | `/api/timetable/{id}/sections` | TimetableSections bridge |

### Allocation

| Family | Prefix | UI surfaces |
|--------|--------|-------------|
| Platform / engine / governance | `/api/allocation/*` | Enterprise Allocation Workspace, Allocation Context, Operations |
| Live membership | `/api/student-sections`, transfer, auto-allocate | Explicit live ops only |

## Additive APIs (AI29.1D)

Mostly composition of frozen backends. Notable additives:

1. Breadcrumb operational context endpoint (consumer OR-permissions).
2. Architecture compliance report endpoint + status fields (`FULLY_VERIFIED` / …).
3. Additive roster envelope fields for combined class (`isCombinedClass`, `operationalClassLabel`, …).
4. Optional section fields on mark/edit DTOs + server save-scope authorization (15A).
5. Allocation `populationSelection` / scope hardening on existing run/simulate contracts (10A).

## Backward compatibility

- Omit section → legacy full Course/Group/Semester cohort.
- Timetable never required; Section never required; Program never required for attendance.
- Unknown JSON fields ignored; prefer UI composition over new endpoints.
- Architecture: `Passed=true` for both `FULLY_VERIFIED` and `PARTIALLY_VERIFIED` — CI should gate on `Status`.

## Security (UI)

- `PermissionAwareButton` / JWT claims disable controls; server remains authoritative.
- 401 → re-auth; 403 → clear “not authorized” copy (Prompt 18).
- Breadcrumb uses `CanViewAcademicOperationalContext` — not Program write.

## Performance

- Cascading loads with AbortSignal; reuse options when scope unchanged.
- Debounced search; paginated staff / roster windows; short roster cache.
- Allocation: send filter criteria to server — do not dump full student catalogs into the browser.
- Avoid N+1 (e.g. one faculty-sections call; no per-row version fetches for display).

## Responsive behavior

| Breakpoint | Priority |
|------------|----------|
| Desktop | Sticky scope toolbars, wide tables in scroll hosts |
| Tablet | No page horizontal overflow; touch targets ≥ 44px |
| Mobile | Attendance + faculty operational actions; sticky save bars retained |

Reuse AI31 `enterpriseTokens` / `dashboardLayoutTokens` — no parallel theme.

## Primary UI routes

| Route / surface | Integration |
|-----------------|-------------|
| `/attendance` (`AttendanceMarking`) | Resolution + marking + combined banner |
| `/setup/sections` | Sections, Student Allocation wizard, Faculty Allocation panel |
| `/setup/academic/allocation-context` | Allocation context |
| `/setup/academic/allocation/operations` | Governance operations |
| Faculty workspace | Navigate to `/attendance` with timetable context |
