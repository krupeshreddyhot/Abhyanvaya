# AI29.1B — Operational Readiness

## Principle

`ISectionReadinessService` is **advisory only**. It never:

- allocates faculty
- moves students
- creates rooms
- modifies timetables

## Checks

| Area | Ready | Warning | Blocked |
|------|-------|---------|---------|
| Capacity | Ok | Soft/under/near | Hard breach |
| Faculty | ≥1 current | — | None |
| Room | Timetable mapped | No mapping | — |
| Subjects | Subjects in scope | None | — |
| Timetable | Mapped | Unmapped | — |
| Students | ≥1 | None | — |
| Lifecycle | Active/Open/Locked | Draft/Planning/Closed | Merged/Split/Archived |

Overall = Blocked if any check Blocked, else Warning if any Warning, else Ready.

## APIs

- `GET /api/sections/readiness/{id}`
- `GET /api/sections/readiness`
- `GET /api/sections/capacity/health`

Permission: `Section.Readiness`
