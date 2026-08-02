# AI30 Phase 2B.5.8 — Attendance Compatibility

## Requirement

Attendance MUST remain backward compatible. Phase 2B.5 adds conflict intelligence only — **no attendance API or workflow changes**.

## Modes (unchanged)

### Legacy Mode (faculty WITHOUT timetable)

Faculty → Course → Group → Semester → Subject → Period → Attendance

### Timetable Mode (faculty WITH timetable)

Faculty → Today's Timetable → Current Class → Attendance

## Resolver

`AttendanceSessionResolver` continues to choose the appropriate mode via `GET /api/attendance-resolution/current`.

Existing attendance controllers and marking APIs are **not modified** by Phase 2B.5.

## Validation performed

- Conflict intelligence services do not call attendance write APIs
- Impact analysis only reports an Attendance signal node (advisory)
- Soft prefill behavior from Phase 2B remains optional and non-breaking

## Explicit non-changes

- No attendance endpoint removed
- No request/response contract changes
- No forced timetable mode for faculty without schedules
