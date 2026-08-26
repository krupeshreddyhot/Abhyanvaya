# AI29.1D Prompt 4A — Program → Course Fail-Closed Hardening

## Behavior

| Condition | Course options |
|-----------|----------------|
| `EnablePrograms = false` | All authorized courses (legacy) |
| Programs enabled + no Program selected | **Empty** |
| Program selected | Hierarchy members ∪ explicit `Course.ProgramId` only |
| Program with zero mapped courses | **Empty** + UI: “No courses are assigned to this program.” |
| Hierarchy GET failed | **Empty** + error/retry via `refreshCatalogs` |

No fallback to the full course catalog when Programs are enabled.

## Unchanged

- Attendance / Scheduling / Timetable / Allocation Engine / Section domain / Subject Master / APIs / DB
- Subject = Course + Group + Semester (Section is not a Subject Master dependency)
- Legacy path when Programs disabled: Course → Group → Semester → Subject
