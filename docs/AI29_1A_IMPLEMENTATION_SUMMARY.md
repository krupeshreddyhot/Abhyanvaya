# AI29.1A — Implementation Summary

## Delivered

| Item | Status |
|------|--------|
| Program master + soft delete/audit | Done |
| EnablePrograms configuration | Done |
| Course.ProgramId nullable | Done |
| IAcademicStructureService SSOT | Done |
| GET /api/academic-structure hierarchy | Done |
| Programs UI | Done |
| FluentValidation rules | Done |
| Dashboard prep APIs | Done |
| Permissions | Done |
| Tests AI29_1A_ProgramManagementTests | Done |
| Docs | Done |

## Unchanged (verified by non-modification)

- AttendanceSessionResolver
- Attendance APIs (students-for-marking not changed in this phase)
- Timetable / Scheduling engines
- Subject Master
- AI31 Dashboard

## Deploy

1. Apply `Apply_AI29_1A_ProgramSchema.sql`
2. Restart API
3. Catalog → Programs → enable Programs when ready
