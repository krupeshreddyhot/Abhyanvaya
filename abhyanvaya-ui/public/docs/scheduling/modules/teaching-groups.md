# Teaching Groups

Operational teaching cohorts under a **Subject Allocation** (lecture, lab, elective, capacity split, etc.).

## Rules

- One Subject Allocation may own **many** Teaching Groups.
- Teaching Groups are created only by explicit user action — never inferred or auto-created.
- Section membership is managed via **TeachingGroupSection** (source of truth). Timetable section rows are server projections.
- Membership editing is not available in the current UI foundation (read-only display).

## Permissions

- `Scheduling.TeachingGroup.View` — list and details
- `Scheduling.TeachingGroup.Manage` — create, update, archive, section changes
