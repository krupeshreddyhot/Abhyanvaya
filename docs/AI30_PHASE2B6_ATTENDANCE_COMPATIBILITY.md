# AI30 Phase 2B.6 — Attendance Compatibility

## Requirement

Attendance MUST remain backward compatible. Phase 2B.6 adds optimization readiness architecture only — **no attendance API or workflow changes**.

## Modes (unchanged)

### Legacy Mode (faculty WITHOUT timetable)

Faculty → Course → Group → Semester → Subject → Period → Attendance

### Timetable Mode (faculty WITH timetable)

Faculty → Today's Timetable → Current Class → Attendance

## Resolver

`AttendanceSessionResolver` remains the single decision point via `GET /api/attendance-resolution/current`.

## Validation

- Optimization readiness services do not call attendance write APIs
- Simulation accept/reject never touches attendance sessions
- No existing attendance controllers modified in this phase

## Explicit non-changes

- No attendance endpoint removed or altered
- No forced timetable mode for faculty without schedules
