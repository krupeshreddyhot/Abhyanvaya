# AI13.DOMAIN.1 — Database Attendance Uniqueness Constraint

**Role:** Chief Architect review — enterprise SaaS defense-in-depth for the Abhyanvaya attendance system.

**Status:** Complete. Build succeeded (0 errors). Migration generated, applied, and verified against the live
PostgreSQL database.

---

## 1. Business Rule

> For a given **Tenant + Student + Subject + Attendance Day**, there must never be more than one `Attendance`
> record.

Application-level validation remains in place (see §5). The database must be the final safeguard — this document
covers exactly that layer.

---

## 2. Current Schema — Investigation Findings

### 2.1 The `Attendance` entity

```1:29:Abhyanvaya.Domain/Entities/Attendance.cs
public class Attendance : BaseEntity
{
    public required int StudentId { get; set; }
    public required int SubjectId { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public bool IsLocked { get; set; }
    public Guid? AttendanceSessionId { get; set; }
    ...
}
```

`BaseEntity` contributes `Id`, `TenantId`, audit columns, and `IsDeleted`. `Date` is mapped as `timestamp with time
zone` (a precise UTC **instant**, not a pure calendar date).

### 2.2 EF Core mapping — the unique index already exists

```25:26:Abhyanvaya.Infrastructure/Persistence/Configurations/AttendanceConfiguration.cs
        builder.HasIndex(a => new { a.TenantId, a.StudentId, a.SubjectId, a.Date })
            .IsUnique();
```

**Key finding:** this composite unique index is **not new**. It was created by migration
`20260415184732_TenantScopedIndexes` (2026-04-15), which explicitly drops the older non-tenant-scoped unique index
(`IX_Attendance_StudentId_SubjectId_Date`) and creates:

```22:26:Abhyanvaya.Infrastructure/Migrations/20260415184732_TenantScopedIndexes.cs
            migrationBuilder.CreateIndex(
                name: "IX_Attendance_TenantId_StudentId_SubjectId_Date",
                table: "Attendance",
                columns: new[] { "TenantId", "StudentId", "SubjectId", "Date" },
                unique: true);
```

### 2.3 Verified against the live PostgreSQL schema

```sql
SELECT indexname, indexdef FROM pg_indexes WHERE tablename = 'Attendance';
```

| indexname | definition |
|---|---|
| `IX_Attendance_TenantId_StudentId_SubjectId_Date` | `CREATE UNIQUE INDEX ... ON "Attendance" USING btree ("TenantId", "StudentId", "SubjectId", "Date")` |
| `IX_Attendance_Tenant_Subject_Date` | non-unique, query-performance index |
| `IX_Attendance_AttendanceSessionId`, `IX_Attendance_Tenant_AttendanceSession`, `IX_Attendance_SubjectId` | supporting indexes |
| `PK_Attendance` | primary key on `Id` |

**Confirmed: the required unique constraint already exists in production**, matches the EF model exactly, and has
existed since 2026-04-15 (41 migrations of history, never dropped or altered since).

---

## 3. Why Duplicates Still Occurred (Root Cause, Tied to the Prior Incident)

If the unique index already existed, how did the "Student Attendance Load Failure" incident produce 480 rows for
240 students on one subject/day (documented in the prior fix session)? Because **the index is unique on the exact
`Date` instant, not on the logical calendar day**:

- Manual attendance (`AttendanceController.MarkAttendance`) stored `Date` = reporting-zone (IST) midnight, expressed
  in UTC → e.g. `2026-07-04T18:30:00Z`.
- Photo/AI attendance (`AttendanceBuilder`, before the prior fix) stored `Date` = `session.GetAttendanceDateUtc()`,
  which treats the session's stored calendar date as **UTC midnight** → `2026-07-05T00:00:00Z`.

Both instants represent "5 July 2026" in India Standard Time, but they are **different physical `DateTime` values**,
so the unique index did not consider them duplicates — it did exactly what it was told, on data that violated the
implicit business assumption that both paths write the same instant.

This was already fixed earlier in this engagement (`IAttendanceCalendar` / `AttendanceCalendar`, introduced to give
`AttendanceBuilder` and `AttendanceSessionQueryService` the same reporting-day anchor that
`AttendanceController.MarkAttendance` uses). The 240 pre-existing duplicate rows were also cleaned up (manual-source
duplicates removed, AI-source rows retained since they carry the recognition detail records).

### 3.1 Preferred key decision: use `Date` as-is (no schema change)

Per the requirement:

> If `Date` includes time, verify that `AttendanceBuilder` and Manual Attendance now store the identical canonical
> UTC attendance date. If yes, use `Date`. Otherwise explain why not.

**Verified yes** — both paths now resolve the day boundary through the same `IAttendanceCalendar` implementation:

- `AttendanceBuilder.BuildAsync` → `_attendanceCalendar.GetDayRangeUtc(session.GetAttendanceDateUtc())`, and stores
  `attendanceDate = dayStartUtc`.
- `AttendanceController.MarkAttendance` → `ReportingDayRange(request.Date)` → stores `Date = dayStartUtc`.
- `AttendanceSessionQueryService` reads via the same day-range for its existing-attendance lookup.

Both ultimately compute `TimeZoneInfo.ConvertTimeToUtc(localMidnight, reportingZone)` for the same reporting
timezone (`Dashboard:ReportingTimeZoneId`, default `Asia/Kolkata`) — a **pure, deterministic function** of
(calendar date, timezone). Given the same calendar day, both paths now produce bit-identical `DateTime` values.
Therefore the existing `(TenantId, StudentId, SubjectId, Date)` unique index is the correct and sufficient key.
**No new column (e.g. a persisted `AttendanceDay` date-only column) is required.**

---

## 4. Migration Strategy

### 4.1 What the migration does — and deliberately does NOT do

File: `Abhyanvaya.Infrastructure/Migrations/20260705140348_EnforceAttendanceUniquenessGuard.cs`

Because the unique index already exists and already matches the EF model bit-for-bit, `dotnet ef migrations add`
produced an **empty schema diff** (confirming there is no drift between the model and the database). Rather than
ship a no-op migration, this migration was hand-authored to serve as an explicit, auditable **defense-in-depth
guard**:

1. **Pre-flight duplicate check** (`DO $$ ... $$` block): scans `Attendance` for any `(TenantId, StudentId,
   SubjectId, Date)` group with `count(*) > 1`. If any exist, it `RAISE EXCEPTION`s with the duplicate-group count
   and up to 20 offending tuples, and the migration **aborts** — Postgres rolls back the whole migration
   transaction. **No data is ever deleted or modified by this migration.**
2. **Idempotent index assertion**: `CREATE UNIQUE INDEX IF NOT EXISTS "IX_Attendance_TenantId_StudentId_SubjectId_Date" ...`
   — a no-op in the current production database (the index already exists), but makes the migration
   self-sufficient: if it were ever run against a database where the original 2026-04-15 index migration was
   skipped (e.g. a restored older dump, a divergent environment), this migration would still leave the database in
   the correct, protected state instead of silently assuming history.

### 4.2 Duplicate handling policy

Per the constraint **"If duplicates exist: DO NOT silently delete. Instead, abort migration OR generate SQL
identifying duplicates with documentation"** — this migration implements **abort with identification**: the
`RAISE EXCEPTION` message itself lists the offending tuples (tenant/student/subject/date/row-count), and this
document provides the same query (§6.1) for operators to run independently, review, and resolve manually before
re-attempting `dotnet ef database update`.

### 4.3 Rollback plan

```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    // Intentionally a no-op...
}
```

`Down()` is deliberately a no-op. This migration does not *own* the unique index's lifecycle — the index predates
this migration by several months and other migrations/entities depend on it remaining in place. Rolling back this
specific migration must never remove the uniqueness safeguard from the database. If the index itself ever needs to
be removed, that must be a separate, explicit, reviewed migration.

### 4.4 Verified execution against the live database

```
Applying migration '20260705140348_EnforceAttendanceUniquenessGuard'.
Done.
```

The guard ran the duplicate scan (found 0 groups, since the prior cleanup already removed the 240 duplicate manual
rows), then asserted the index (already present) — completing successfully with zero data changes.

**Guard behavior was also independently proven** in an isolated, rolled-back transaction:
- Against real (clean) data: the guard block executes with no exception. ✅
- Against a synthetic duplicate pair (in a `TEMP TABLE` shadow, rolled back, never touching real rows): the guard
  correctly raised `AI13.DOMAIN.1: Attendance uniqueness guard aborted migration. Found 1 duplicate group(s).
  First offenders: (Tenant=1, Student=999, Subject=17, ...)`. ✅

---

## 5. Insert Path Review

| Path | File | Duplicate protection today | DB constraint as final safeguard |
|---|---|---|---|
| Manual attendance | `AttendanceController.MarkAttendance` (`POST /api/attendance/mark`) | Rejects the whole request with `BadRequest("Attendance already marked")` if any row exists for the subject/day; additionally filters `existingSet` per student before building insert list. | ✅ protected |
| AI / Photo attendance | `AttendanceBuilder.BuildAsync` (via `AttendanceSessionFinalizer`) | `GetExistingAttendanceStudentIdsAsync` (day-range match) excludes students who already have a row for that reporting day; session-level `Approved`/`Completed` status guard prevents re-running finalize on an already-finalized session. | ✅ protected |
| Edit attendance | `AttendanceController.EditAttendance` (`PUT /api/attendance/edit`) | Updates `Status` on existing rows only — **never inserts** new `Attendance` rows. | N/A (no insert) |
| Bulk import | — | **Not present in the current codebase.** Searched for `BulkImport`/`ImportAttendance` and any bulk insert call sites; none exist today. Documented here so this becomes a checklist item if/when such a feature is added — it must reuse `AddAttendances` + the same `IAttendanceCalendar` day anchor. | N/A (feature doesn't exist yet) |

All insert paths continue to work unchanged — this task made **no business logic changes**, consistent with the
constraint. `dotnet build` succeeds with 0 errors after the migration was added.

### 5.1 Known limitation (documented, not fixed — out of scope for DOMAIN.1)

Neither insert path currently catches a raw Postgres unique-violation (`SqlState 23505`) as a distinct exception
type; `ConcurrencyExceptionHelper` only maps `DbUpdateConcurrencyException` (optimistic-concurrency/rowversion
conflicts), not `DbUpdateException` wrapping a unique-index violation. In practice this is a narrow race window
(e.g., a double-submitted "Save Attendance" click, or two finalization requests racing on the same session) that
existing app-level guards (`alreadyExists` check, session status guard) already close in all normal flows. If it
were ever hit, the caller would see a generic 500 instead of a friendly "Attendance already marked" message. This
is flagged as a **recommended follow-up**, not addressed here, since fixing it would touch business-logic error
handling — outside the "database integrity only, no business logic changes" scope of this task.

---

## 6. Verification SQL

### 6.1 Find duplicates (safe, read-only — run any time)

```sql
SELECT "TenantId", "StudentId", "SubjectId", "Date", count(*) AS row_count
FROM "Attendance"
GROUP BY "TenantId", "StudentId", "SubjectId", "Date"
HAVING count(*) > 1
ORDER BY row_count DESC;
```

Current result: **0 rows** (clean).

### 6.2 Verify the unique index exists

```sql
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'Attendance'
  AND indexname = 'IX_Attendance_TenantId_StudentId_SubjectId_Date';
```

Result:

```
indexname: IX_Attendance_TenantId_StudentId_SubjectId_Date
indexdef:  CREATE UNIQUE INDEX "IX_Attendance_TenantId_StudentId_SubjectId_Date"
           ON public."Attendance" USING btree ("TenantId", "StudentId", "SubjectId", "Date")
```

### 6.3 Verify inserts (positive + negative case)

Positive — a normal insert for a new (tenant, student, subject, date) tuple succeeds as always (unchanged app
behavior; proven by the existing manual/AI attendance flows and integration coverage).

Negative — attempting to insert a second row for an existing (tenant, student, subject, date) tuple is rejected by
Postgres at the constraint level:

```sql
-- Inside a transaction, safe to run and roll back:
BEGIN;
INSERT INTO "Attendance" ("TenantId","StudentId","SubjectId","Date","Status","IsLocked","CreatedDate")
SELECT "TenantId","StudentId","SubjectId","Date", 0, false, now()
FROM "Attendance" LIMIT 1;
-- Expected: ERROR: duplicate key value violates unique constraint
--           "IX_Attendance_TenantId_StudentId_SubjectId_Date"
ROLLBACK;
```

### 6.4 Applied migration history (tail)

```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 3;
```

```
20260705140348_EnforceAttendanceUniquenessGuard
20260703062537_AddClassroomImageUploadColumns
20260702175554_AddClassScheduleAndTimetableIntegration
```

---

## 7. Architecture Impact

- **Defense-in-depth achieved.** Application-level validation (existing-row checks, session status guards) remains
  the first line of defense for UX (friendly error messages, no wasted work). The database unique index is now an
  explicit, documented, auditable **last line of defense** — any future code path that forgets to check for
  existing attendance will fail loudly with a constraint violation rather than silently corrupting reporting data.
- **No schema change, no data change, no behavior change.** The migration is a guard/assertion, not a structural
  change. This keeps the blast radius minimal and rollback-safe (`Down()` is a no-op by design).
- **No `IgnoreQueryFilters()` used anywhere** in this change, per constraint.
- **Soft-delete note:** `Attendance` inherits `IsDeleted` from `BaseEntity`, but nothing in the codebase currently
  soft-deletes `Attendance` rows (verified via search). If that ever changes, the unique index would need to become
  a **partial unique index** (`WHERE "IsDeleted" = false`) to allow a new row to replace a soft-deleted one for the
  same tenant/student/subject/day. Not needed today; documented for future awareness.
- **Enterprise SaaS quality:** the guard is idempotent, non-destructive, and safe to run repeatedly across
  environments (dev/staging/prod) regardless of migration history divergence.

---

## 8. Files Created / Modified

| File | Change |
|---|---|
| `Abhyanvaya.Infrastructure/Migrations/20260705140348_EnforceAttendanceUniquenessGuard.cs` | **Created.** Hand-authored guard migration (duplicate abort-check + idempotent unique index assertion). |
| `Abhyanvaya.Infrastructure/Migrations/20260705140348_EnforceAttendanceUniquenessGuard.Designer.cs` | **Created.** EF-scaffolded model snapshot for this migration (auto-generated, unmodified). |
| `Abhyanvaya.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs` | **Modified.** EF-regenerated current model snapshot (auto-generated; reflects no structural attendance changes). |
| `docs/AI13_DOMAIN1_DATABASE_UNIQUENESS.md` | **Created.** This document. |

No entity, DTO, controller, service, or UI files were modified for AI13.DOMAIN.1 — the constraint already existed
in application code (`AttendanceConfiguration.cs`) and required no change.
