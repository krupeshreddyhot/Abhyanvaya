# AI29.1C.5A — Architecture Review

Surgical hardening on AI29 → AI29.1C.5. No redesign of allocation algorithms, scoring, `SectionAllocationContext`, Attendance, Scheduling, or Faculty Workspace.

## Boundaries (Architecture Guard)

Allowed:

- Allocation Operations → Scenario / Lifecycle / Version / Governance services  
- Allocation Operations → Allocation Context builder (freshness only)

Forbidden:

- Operations → Student / Attendance / Timetable repositories  
- Direct production student-section mutation  
- Controller bypass of lifecycle/governance for Approve/Archive/Review  

## Checksum model

`AllocationCanonicalChecksum` builds a sorted JSON tree covering scenario data, context version/checksum, strategy/constraint config versions, score, trace, lifecycle, operation — then SHA-256. Reuses the platform crypto primitive; no second integrity framework.

## Permissions

`Allocation.Scenario.Archive` is separate from `Allocation.Scenario.Review` (policy `CanArchiveAllocationScenarios`).

## Attendance compatibility

`AttendanceSessionResolver` and Attendance APIs were not modified. Manual Course→Group→Semester→Subject→Period attendance remains protected.
