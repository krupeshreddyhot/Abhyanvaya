# AI29 EF Migration Baseline

## Migration

`Abhyanvaya.Infrastructure/Persistence/Migrations/20260806180000_AI29_AcademicHierarchyBaseline.cs`

Consolidates schema previously delivered only via:

| Script | Covered |
|--------|---------|
| `Apply_AI29_SectionSchema.sql` | Sections + related tables, Section permissions |
| `Apply_AI29_1A_ProgramSchema.sql` | Programs, TenantAcademicConfigurations, Course.ProgramId, Program permissions |
| `Apply_AI29_1A5_EnterpriseHardening.sql` | Program Icon/ThemeColor/AcademicCalendarId, DisplayOrder columns, ProgramPolicies |
| `Apply_AI29_1A6_PerformanceGuard.sql` | AcademicHierarchySnapshots |

## New database

```bash
dotnet ef database update --project Abhyanvaya.Infrastructure --startup-project Abhyanvaya.API
```

The baseline migration creates all AI29 hierarchy tables.

## Existing database (scripts already applied)

**Do not** run `Up()` for this migration (tables already exist).

```bash
# Apply history stamp only:
psql -f scripts/MarkApplied_AI29_AcademicHierarchyBaseline.sql
```

Or insert manually:

```sql
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260806180000_AI29_AcademicHierarchyBaseline', '8.0.0')
ON CONFLICT DO NOTHING;
```

## Old Apply_*.sql scripts

Keep as historical/idempotent fallback. Prefer the EF migration for new environments going forward.
