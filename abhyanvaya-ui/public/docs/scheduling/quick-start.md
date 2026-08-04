# Quick Start — Minimum Configuration

## Academic Year
path: /setup/scheduling/academic-years

Create the academic year and mark it **current**. Later modules (working days, versions, allocations) attach to this year.

## Working Days
path: /setup/scheduling/working-days

Enable the teaching week (e.g. Mon–Sat). Time slots and designer rely on working days.

## Campus
path: /setup/scheduling/campuses

Define campus → buildings → floors. Rooms hang off this hierarchy.

## Rooms
path: /setup/scheduling/rooms

Add classrooms/labs with capacity. Required before timetable placement.

## Time Slots
path: /setup/scheduling/time-slots

Create period sets (lectures, breaks, lunch). Required for allocations and designer.

## Faculty
path: /setup/staff

Ensure Catalog **Staff** exist (and Departments/Subjects). Scheduling does not duplicate faculty master data.

## Subject Allocation
path: /setup/scheduling/subject-allocations

Assign staff to subjects with weekly hours. Blocked until Faculty, Subjects, Departments, and Time Slots exist.

## Schedule Version
path: /setup/scheduling/governance/versions

Create a schedule version for the academic year/term before drafting timetables.

## Timetable Designer
path: /setup/scheduling/timetables

Build the draft timetable. This step does not publish or finalize attendance.

## Publish
path: /setup/scheduling/governance/publishing

When ready, publish through Governance. Faculty with timetables then use timetable-driven attendance; faculty without timetables keep Course → Group → Semester → Subject → Period → Attendance.
