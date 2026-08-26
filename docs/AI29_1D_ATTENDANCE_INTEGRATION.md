# AI29.1D — Attendance Integration

Mark Attendance integrates with the academic hierarchy and optional Section population while keeping Subject Master and session resolution on the server.

## Hard rules

1. **Section is an operational student grouping and is not part of Subject Master.**
2. Timetable is never mandatory.
3. Section is never mandatory on mark/edit/roster.
4. Program is never required for attendance.
5. Single `AttendanceSessionResolver` — UI consumes; does not reimplement.
6. Unauthorized section or student on write ⇒ **atomic reject** of the entire request.

## Timetable-driven attendance

1. Faculty / page calls `GET /api/attendance-resolution/current`.
2. When `Mode=Timetable` / `HasTimetable`, UI prefills Course, Group, Semester, Subject, Period, Room, and participating `sectionIds` / `sectionCodes` from TimetableSections.
3. Cascade remains editable (user may leave timetable path for manual adjustments where permitted).
4. Operational panel (`OperationalTimetableContextPanel`) displays session context from the resolution contract only.

## Manual attendance fallback

When resolution is `Legacy` / no timetable:

| Field | Required? |
|-------|-----------|
| Course → Group → Semester → Subject → Period | Yes |
| Section | Optional |
| Program | Optional (only if Programs enabled for cascade) |

Roster and save without section use the full Course/Group/Semester cohort (legacy).

## Section population behavior

| Selection | Roster / save scope |
|-----------|---------------------|
| None | Full C/G/S cohort |
| Single section | Students in that section (current membership) |
| Multi / TimetableSections | Union of participating sections (`AttendanceSectionScope`) |

Subject options stay Course+Group+Semester keyed — Section never redefines Subject Master.

## Combined sections

See `AI29_1D_COMBINED_SECTION_UI.md`.  
UI shows one operational class (`Section A + B`); server owns membership expansion.

## Save contract (additive)

`POST /api/attendance/mark` and `PUT /api/attendance/edit` accept optional:

- `sectionId`
- `sectionIds[]`

Server applies `AttendanceSaveScope`:

- Validates sections against academic year / C/G/S scope.
- Every submitted student must belong to the authorized population.
- Fail closed — no partial writes.

UI builders: `attendanceMarkingScope.ts` / Prompt 15A write-scope helpers.  
Mark/edit do **not** re-call the session resolver.

## API contracts consumed

| Endpoint | Role |
|----------|------|
| `GET /api/attendance-resolution/current` | Session resolution |
| `GET /api/attendance/students-for-marking` | Roster (+ combined envelope fields) |
| `POST /api/attendance/mark` | Atomic mark |
| `PUT /api/attendance/edit` | Atomic edit |
| Academic cascade / config APIs | Hierarchy options |
| `GET .../breadcrumb/context` | Shared operational trail |

## Security

- Permissions: `Attendance.View` / `Attendance.Manage` (and related).
- UI disable ≠ authorize; server enforces section + student integrity.
- Tenant isolation via JWT — no client tenant switch.

## Performance

- Reuse academic options when scope unchanged.
- Roster paging (e.g. pageSize 50 / fetch window 200) + short cache.
- Abort in-flight loads on cascade change.
- Mobile sticky save bar retained (Prompt 17).

## Backward compatibility

- Clients omitting section fields behave as pre–AI29.1D full-cohort marking.
- Additive response fields for combined class are ignore-safe for older clients.
- No second attendance session resolver or parallel eligibility engine.

## Primary UI

- `abhyanvaya-ui/src/pages/AttendanceMarking.tsx`
- `CombinedSectionClassBanner`, `OperationalTimetableContextPanel`
- Faculty workspace → `/attendance`
