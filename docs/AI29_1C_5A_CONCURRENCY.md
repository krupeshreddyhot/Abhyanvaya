# AI29.1C.5A — Optimistic Concurrency

`AllocationEngineScenario.RowVersion` (`bytea`) is an EF Core concurrency token, matching the AttendanceSession/Enrollment pattern (not xmin custom locking).

On conflict, APIs return a controlled conflict with message:

> This allocation scenario has changed since you opened it. Refresh the scenario before continuing.

Silent overwrite is forbidden.
