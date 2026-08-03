# AI30 Phase 2 Prerequisites — Why Phase 1A Enables the Timetable Designer

| Field | Value |
|-------|-------|
| **Document ID** | AI30-Phase2-Prerequisites |
| **Status** | Guidance |
| **Date** | August 2026 |

---

## Purpose

Phase 2 (Timetable Designer) places subjects into rooms and periods. Without Phase 1A, the designer would lack organizational scope, availability constraints, and reusable period structures.

## Prerequisite mapping

| Phase 1A capability | Why Phase 2 needs it |
|---------------------|----------------------|
| **Department on Subject Allocation** | Designer filters by department; prevents cross-department mis-assignment |
| **Faculty Availability Calendar** | Designer must avoid Preferred/Unavailable/Leave windows when proposing slots (hard rules land in Phase 3; data must exist first) |
| **Room Availability Calendar** | Designer must skip Maintenance/Reserved/Blocked rooms |
| **Subject Categories + RequiresRoomType** | Designer defaults Theory→classroom, Lab→lab rooms |
| **Time Slot Templates** | Designer starts from Regular/Friday/HalfDay/Exam grids instead of ad-hoc periods |
| **Dashboard health** | Ops readiness: missing categories, unused templates, department coverage |

## Explicitly deferred

- Timetable generation UI/engine
- Conflict detection engine
- Optimization / AI scheduling
- Attendance automation

Phase 1A stores **master data and validations only**.
