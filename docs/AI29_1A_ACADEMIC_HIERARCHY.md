# AI29.1A — Academic Hierarchy & Program Management

## Objective

Introduce **Program** as an optional organizational layer above Course, without breaking Course → Group → Semester → Subject, Sections (AI29), Attendance, Scheduling, or Dashboards.

## Target hierarchy

**Programs enabled**

```
College → Program → Course → Group → Semester → Subjects / Sections
```

**Programs disabled (default)**

```
College → Course → Group → Semester → Subjects / Sections
```

## Configuration

Tenant setting `EnablePrograms` in `TenantAcademicConfigurations` (default `false`).

## Long-term ADR note — Academic Organizational Unit

Today the implemented unit is **Program**. Architecturally, treat it as the first concrete form of a future **Academic Organizational Unit** that may represent Faculty / School / Division / Academic Unit / Program without redesigning the platform. AI29.1A implements Program only.

## Non-goals

- No AttendanceSessionResolver changes
- No Attendance API changes
- No Timetable / Scheduling engine changes
- No AI31 Dashboard changes
- No Subject Master redesign

## Attendance compatibility

Unchanged: Legacy Course→Group→Semester→Subject→Period and Timetable-driven attendance.
