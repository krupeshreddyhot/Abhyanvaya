# Scheduling Configuration Guide

Enterprise guide for configuring Abhyanvaya scheduling. Content is markdown-driven and may be exported to PDF from the UI.

## Purpose

Help colleges configure Academic Calendar, Campus, Framework, Faculty Planning, Timetable Design, Governance, Validation, and Optimization in dependency order — without changing timetable generation or attendance APIs.

## Dependencies

- **Catalog Departments** remain the single source of truth (AI30 AC1). Scheduling never owns a second Department master.
- **Faculty / Subjects / Courses / Groups / Semesters** are Catalog masters used by Subject Allocation and Timetable Designer.
- Attendance continues via **AttendanceSessionResolver**:
  - With timetable → timetable-driven attendance
  - Without timetable → Course → Group → Semester → Subject → Period → Attendance

## Configuration order

1. Academic Calendar — [Academic Years](/setup/scheduling/academic-years), [Working Days](/setup/scheduling/working-days), [Holiday Calendar](/setup/scheduling/holidays), [Holiday Types](/setup/scheduling/holiday-types)
2. Campus & Infrastructure — [Campus Facilities](/setup/scheduling/campuses), [Rooms](/setup/scheduling/rooms), [Room Features](/setup/scheduling/room-features), [Room Availability](/setup/scheduling/room-availability)
3. Scheduling Framework — [Time Slots](/setup/scheduling/time-slots), [Time Slot Templates](/setup/scheduling/time-slot-templates), [Subject Categories](/setup/scheduling/subject-categories), [Subject Delivery](/setup/scheduling/subject-delivery), [Room Rules](/setup/scheduling/room-rules)
4. Faculty Planning — [Faculty Availability](/setup/scheduling/faculty-availability), [Faculty Preferences](/setup/scheduling/faculty-preferences), [Faculty Workloads](/setup/scheduling/faculty-workloads), [Subject Allocation](/setup/scheduling/subject-allocations)
5. Timetable Design — [Schedule Versions](/setup/scheduling/governance/versions), [Timetable Designer](/setup/scheduling/timetables), Faculty/Student/Room timetable views
6. Governance — Approval Queue, Publishing, Clone Wizard, Change History, Governance Dashboard
7. Validation — Conflict Dashboard, Workspace, Analytics, Rule Thresholds
8. Optimization — Preview, Workspace, Dashboard

## Required vs Optional

**Required (minimum):** Academic Year, Working Days, Campus, Rooms, Time Slots, Faculty (Catalog), Subject Allocation, Schedule Version, Timetable Designer.

**Optional:** Holiday Types, Room Features, Availability windows, Preferences, Workloads, Governance extras, Conflicts, Optimization.

## Expected outputs

- Draft timetable ready for validation and publishing
- Configuration readiness % and next recommended step on the Scheduling hub and dashboard
- No automatic attendance finalization

## Typical users

- College administrators configuring a new academic year
- Scheduling officers maintaining rooms and slots
- HODs reviewing subject allocation before design

## Quick Start

Use the [Quick Start Wizard](/setup/scheduling/quick-start) for a step-by-step minimum path.
