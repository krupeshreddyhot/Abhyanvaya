# AI30 Phase 1B — Implementation Summary

| Field | Value |
|-------|-------|
| **Document ID** | AI30-Phase1B-Implementation-Summary |
| **Date** | August 2026 |
| **Migration** | `20260801162850_AI30_Phase1B_SchedulingFoundationExtensions` |

---

## Architecture decisions

1. **FacultyTeachingPreference** reuses Staff — no duplicate Faculty entity.
2. **RoomFeature / Assignment** parallel model — Room.FeatureFlags untouched.
3. **HolidayTypeCatalog** named separately from enum `HolidayType` to avoid clash; Holiday keeps enum + optional catalog FK.
4. **Subject delivery** additive columns; reuses 1A RequiresRoomType / RequiresLabEquipment.
5. Permissions **28–35** for FacultyPreferences, RoomFeatures, SubjectDelivery, HolidayTypes.

## Files created (high level)

- Domain entities/enums: FacultyTeachingPreference, RoomFeature, RoomFeatureAssignment, SubjectDeliveryType, HolidayTypeCatalog, PreferredTeachingMode
- Application services, DTOs, validators, repositories interfaces
- Infrastructure repos, EF configs, migration
- API `Phase1BControllers.cs`
- UI: FacultyPreferencesPage, RoomFeaturesPage, SubjectDeliveryPage, HolidayTypesPage
- Docs: `AI30_PHASE1B_*.md`

## Files modified (high level)

- Subject, Holiday, PermissionKeys, seed, policies, Program.cs
- SchedulingDashboardService / DTO
- AcademicCalendar holiday DTOs/service
- Hub, dashboard, holidays UI, routes, permissionKeys, schedulingService

## Test results

`dotnet test --filter FullyQualifiedName~Scheduling` → **57 passed**, 0 failed (Phase 1 + 1A + 1B).

## ADL compliance

See `AI30_PHASE1B_ARCHITECTURE_REVIEW.md`.

```powershell
dotnet ef database update --project Abhyanvaya.Infrastructure --startup-project Abhyanvaya.API
```
