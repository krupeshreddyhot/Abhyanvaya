# AI29 — Implementation Summary

## Delivered

| Area | Status |
|------|--------|
| Section Master CRUD | Done |
| Student–Section allocation + history + transfer | Done |
| Configurable capacity | Done |
| Faculty–Section assignment | Done |
| Subject untouched | Done |
| TimetableSections combined classes | Done |
| Attendance optional section filter | Done |
| AttendanceSessionResolver additive sections | Done |
| Student import Section column | Done |
| Auto-allocation strategies | Done |
| Permissions + policies | Done |
| UI Academic Setup → Sections | Done |
| Dashboard prep APIs | Done |
| Reports endpoints | Done |
| Schema script (non-destructive) | Done |
| Unit tests AI29_SectionManagementTests | Done |
| Docs | Done |

## Key files

- Domain: `Abhyanvaya.Domain/Entities/Academic/*`
- Service: `Abhyanvaya.Application/Academic/SectionManagementService.cs`
- API: `Abhyanvaya.API/Controllers/SectionsController.cs`
- SQL: `scripts/Apply_AI29_SectionSchema.sql`
- UI: `abhyanvaya-ui/src/pages/setup/SectionsPage.tsx`
- Resolver (additive only): `AttendanceSessionResolver.cs`
- Attendance filter: `AttendanceController.GetStudentsForMarking`

## Verification

- Existing Legacy and Timetable attendance flows preserved
- Subject Master not modified
- AI31.x dashboard tests should continue to pass (no engine changes)
- Apply SQL before first use in an environment

## Deploy order

1. Apply `Apply_AI29_SectionSchema.sql`
2. Deploy API + UI
3. Grant Section.* permissions (seeded for Admin role 100)
4. Create sections per semester; optionally map timetable entries to sections
