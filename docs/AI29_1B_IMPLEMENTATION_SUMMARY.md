# AI29.1B — Implementation Summary

## Delivered

| Area | Artifacts |
|------|-----------|
| Lifecycle SM | `SectionLifecycleStateMachine`, `ISectionLifecycleService`, transition audit |
| Types | `SectionTypeCodes` (config strings) |
| Capacity | `ISectionCapacityEngine`, tenant policy, analytics |
| Merge/Split | Wizards + reversible transactions + lineage |
| Combined | `SectionGroup` + membership history |
| Readiness | Advisory `ISectionReadinessService` |
| Dashboard APIs | Capacity/health/analytics (AI31 UI unchanged) |
| Reports | CSV / Excel / PDF export |
| Permissions | Lifecycle/Merge/Split/Capacity/Readiness |
| UI | Sections page tabs |
| EF migration | `Persistence/Migrations/20260807180000_AI29_1B_SectionLifecycleCapacity.cs` |
| Tests | `AI29_1B_SectionLifecycleCapacityTests` |
| Allocation hooks | `ISectionAllocationRecommendationService` (null stub for 1C) |

## Explicit non-goals (preserved)

- Subject Master unchanged
- `AttendanceSessionResolver` unchanged
- Attendance APIs unchanged
- Scheduling / Timetable engines unchanged
- Faculty Workspace / AI31 dashboard business logic unchanged

## Apply schema (EF-only)

**Primary path**

- Development: API startup runs `Database.MigrateAsync()`
- Other environments:

```bash
dotnet ef database update --project Abhyanvaya.Infrastructure --startup-project Abhyanvaya.API
```

**Recovery only** (schema already present, history missing):

```bash
psql ... -f scripts/MarkApplied_AI29_1B_SectionLifecycleCapacity.sql
```

Do not use Apply-*.sql scripts for AI29.1B.

## Backward compatibility

- `MaximumStrength` remains maximum capacity column
- Manual attendance path Course→Group→Semester→Subject→Period→(Optional Section) unchanged
- Timetable attendance path unchanged
