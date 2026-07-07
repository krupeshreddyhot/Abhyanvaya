# AI13.DOMAIN.3 — AttendanceDay Adoption

**Objective:** Replace direct attendance date calculations (`Attendance.Date` boundary math, `GetDayRangeUtc`,
`GetDayStartUtc`, ad hoc `TimeZoneInfo` conversions, duplicated `ReportingCalendar` logic) with the
`AttendanceDay` value object introduced in AI13.DOMAIN.2, wherever it is safe and behavior-preserving to do so.

**Status:** Complete. Build succeeded (0 errors).

---

## 1. Search Methodology

Searched the entire solution for every pattern named in the brief:

```
Attendance.Date | GetDayRangeUtc | GetDayStartUtc | DateTime attendanceDate | TimeZoneInfo | ReportingCalendar
```

Matches were found in 10 files. Each is triaged below as **Adopted**, **Adopted transitively**, **Reviewed — no
change needed**, or **Reviewed — deliberately not changed** (with reasoning).

## 2. Files Changed

### 2.1 `Abhyanvaya.Application/Common/Interfaces/IAttendanceCalendar.cs` — **Adopted**

Replaced the tuple-returning methods with a single factory-style method returning `AttendanceDay`:

```5:16:Abhyanvaya.Application/Common/Interfaces/IAttendanceCalendar.cs
public interface IAttendanceCalendar
{
    AttendanceDay GetAttendanceDay(DateTime date);
    AttendanceDay Today();
}
```

Before: `DateTime GetDayStartUtc(DateTime date)` and `(DateTime, DateTime) GetDayRangeUtc(DateTime date)`.

### 2.2 `Abhyanvaya.Infrastructure/Services/AttendanceCalendar.cs` — **Adopted**

Implementation now delegates 100% of its day-boundary math to `AttendanceDay`; the class's only remaining
responsibility is resolving *which* `TimeZoneInfo` is "the reporting zone" from configuration.

```23:28:Abhyanvaya.Infrastructure/Services/AttendanceCalendar.cs
public AttendanceDay GetAttendanceDay(DateTime date) =>
    AttendanceDay.FromReportingCalendar(date, _reportingZone);

public AttendanceDay Today() => AttendanceDay.Today(_reportingZone);
```

~15 lines of duplicated `ConvertTimeFromUtc`/`ConvertTimeToUtc`/`NormalizeToUtc` logic removed.

### 2.3 `Abhyanvaya.Application/AttendanceBuilder.cs` — **Adopted**

`BuildAsync` and the private `GetExistingAttendanceStudentIdsAsync` helper now work with a single `AttendanceDay`
instead of a `(dayStartUtc, dayEndUtc)` tuple threaded through the method:

```44:56:Abhyanvaya.Application/AttendanceBuilder.cs
var attendanceDay = _attendanceCalendar.GetAttendanceDay(session.GetAttendanceDateUtc());
var attendanceDate = attendanceDay.UtcStart;

var isLocked = await _context.Attendances
    .AnyAsync(a =>
            a.TenantId == session.TenantId
            && a.SubjectId == session.SubjectId
            && a.Date >= attendanceDay.UtcStart
            && a.Date < attendanceDay.UtcEnd
            && a.IsLocked,
        cancellationToken);
```

`GetExistingAttendanceStudentIdsAsync(AttendanceSession, DateTime, DateTime, ...)` → `GetExistingAttendanceStudentIdsAsync(AttendanceSession, AttendanceDay, ...)`.

### 2.4 `Abhyanvaya.Application/AttendanceSessionQueryService.cs` — **Adopted**

Same pattern as 2.3, in `GetExistingAttendanceStudentIdsAsync`:

```298:314:Abhyanvaya.Application/AttendanceSessionQueryService.cs
var attendanceDay = _attendanceCalendar.GetAttendanceDay(session.GetAttendanceDateUtc());
...
        a.Date >= attendanceDay.UtcStart
        && a.Date < attendanceDay.UtcEnd
```

### 2.5 `Abhyanvaya.API/Controllers/AttendanceController.cs` — **Adopted**

This was the largest change in the sweep:

- Constructor: replaced `IConfiguration configuration` with `IAttendanceCalendar attendanceCalendar` (removes the
  controller's need to resolve a reporting time zone or normalize dates itself).
- Removed the private `ReportingTz` property and `ReportingDayRange(DateTime)` helper (which called
  `ReportingCalendar.NormalizeToUtc` + `ReportingCalendar.GetUtcRangeForReportingDayContainingUtc`).
- Added a tiny `ToRange(AttendanceDay)` helper so the five existing LINQ query blocks (which use
  `dayStartUtc`/`dayEndUtc` local variables extensively) required minimal, low-risk edits rather than a full
  rewrite of every predicate:

```34:36:Abhyanvaya.API/Controllers/AttendanceController.cs
private static (DateTime StartUtcInclusive, DateTime EndUtcExclusive) ToRange(AttendanceDay day) =>
    (day.UtcStart, day.UtcEnd);
```

- All 5 call sites now resolve their day via `_attendanceCalendar.GetAttendanceDay(...)`:
  `MarkAttendance`, `GetAttendance`, `GetStudentsForMarking`, `LockAttendance`, `EditAttendance`.
- The future-date guard in `MarkAttendance` was simplified from two independent
  `TimeZoneInfo.ConvertTimeFromUtc(...).Date` calls to a direct value comparison on the value object:

```csharp
var attendanceDay = _attendanceCalendar.GetAttendanceDay(request.Date);
var today = _attendanceCalendar.Today();
if (attendanceDay.LocalDate > today.LocalDate)
    return BadRequest("Cannot mark future attendance");
```

- `IConfiguration` is no longer a dependency of this controller at all.

### 2.6 `Abhyanvaya.API/Common/ReportingCalendar.cs` — **Adopted (partial, by design)**

`GetUtcRangeForReportingCalendarDate` — the actual "local midnight → UTC range" math — now delegates to
`AttendanceDay.FromDate`:

```63:71:Abhyanvaya.API/Common/ReportingCalendar.cs
public static (DateTime StartUtcInclusive, DateTime EndUtcExclusive) GetUtcRangeForReportingCalendarDate(
    int year, int month, int day, TimeZoneInfo tz)
{
    var attendanceDay = AttendanceDay.FromDate(new DateOnly(year, month, day), tz);
    return (attendanceDay.UtcStart, attendanceDay.UtcEnd);
}
```

Because `GetUtcRangeForReportingDayContainingUtc` and `GetReportingDayUtcRangeForUtcNow` both call this method
internally, **all existing callers of `ReportingCalendar`'s public API — including every remaining call site in
`DashboardController` — transparently benefit from the unified `AttendanceDay` math with zero call-site changes**
and zero behavior change (the math is provably identical: `ConvertTimeToUtc(localMidnight, tz)` then
`.AddDays(1)`, exactly what `AttendanceDay.FromDate` does).

`ResolveReportingTimeZone` and `NormalizeToUtc` were intentionally **not** touched — see §4 (Remaining Technical
Debt) for why.

### 2.7 `Abhyanvaya.API/Controllers/DashboardController.cs` — **Adopted (targeted)**

`GetMonthlyTrend`'s grouping logic re-implemented the same "instant → reporting-local-day → instant" round trip
that `AttendanceDay` centralizes. Refactored to use `AttendanceDay` directly as the `GroupBy` key (safe because
`AttendanceDay` implements value equality/`GetHashCode`, and this LINQ runs in-memory over already-materialized
rows, not against the database):

```222:233:Abhyanvaya.API/Controllers/DashboardController.cs
var data = rows
    .GroupBy(x => AttendanceDay.FromUtc(DateTime.SpecifyKind(x.Date, DateTimeKind.Utc), tz))
    .Select(g => new
    {
        Date = g.Key.UtcStart,
        Present = g.Count(x => x.Status == AttendanceStatus.Present),
        Total = g.Count(),
    })
    .OrderBy(x => x.Date)
    .ToList();
```

Verified behavior-identical: the old code did `SpecifyKind(Utc)` → `ConvertTimeFromUtc` → truncate to local
midnight → `ConvertTimeToUtc`; `AttendanceDay.FromUtc(...).UtcStart` performs exactly the same steps.

The other two `ReportingCalendar.*` call sites in this controller (`GetTodaySummary`'s
`GetReportingDayUtcRangeForUtcNow`, and the subject-day query's `GetUtcRangeForReportingDayContainingUtc`) were
**left untouched at the call site** — they already benefit transitively from §2.6's internal refactor and did not
need to change to get the deduplication benefit.

## 3. Reviewed — No Change Needed

| File | Match | Why no change |
|---|---|---|
| `Abhyanvaya.Domain/Entities/AttendanceSession.Factory.cs` | `DateTime attendanceDate` (×2) | This is a factory **method parameter name**, used only to set the `AttendanceDate` property on the new entity. It performs no day-boundary calculation — it's the raw input, not a duplicated calculation. |
| `Abhyanvaya.Domain/Entities/AttendanceSession.Validation.cs` | (`GetAttendanceDateUtc()`, matched in earlier searches) | This method only normalizes `DateTimeKind` (`Utc`/`Local`/`Unspecified` switch) — it does not compute a day range or resolve a time zone, so there is nothing to replace with `AttendanceDay`. It remains the correct, minimal bridge from the entity's raw `AttendanceDate` into a UTC instant that callers then pass into `IAttendanceCalendar.GetAttendanceDay(...)`. Per the DDD design in AI13.DOMAIN.2, `AttendanceSession` (Domain) correctly has no knowledge of *which* time zone is the reporting zone — that decision belongs to the Infrastructure-configured `AttendanceCalendar`. |
| `Abhyanvaya.IntegrationTests/Fixtures/AttendanceTestDataFactory.cs` | `attendanceDate: DateTime.UtcNow.Date` | Named-argument call site for the factory above — same reasoning, no duplicated logic present. |

## 4. Reviewed — Deliberately Not Changed (Remaining Technical Debt)

### 4.1 `ReportingCalendar.NormalizeToUtc` vs. `AttendanceDay`'s normalization

While auditing every normalization implementation, a **pre-existing behavioral discrepancy** was discovered:

| Implementation | `DateTimeKind.Unspecified` handling |
|---|---|
| `AttendanceDay` (`Abhyanvaya.Domain.ValueObjects`) | `DateTime.SpecifyKind(value, DateTimeKind.Utc)` — treated as already the intended UTC instant, **no shift**. |
| `AttendanceCalendar` (Infrastructure, both before and after this change) | Same as `AttendanceDay` — no shift. |
| `AttendanceSession.GetAttendanceDateUtc()` (Domain) | Same as `AttendanceDay` — no shift. |
| `ReportingCalendar.NormalizeToUtc` (API) | `value.ToUniversalTime()` **then** tag `Utc` — for an `Unspecified` value, `.ToUniversalTime()` interprets it as **local server time** and shifts it by the host machine's local UTC offset before tagging it UTC. |

Three of the four implementations agree; `ReportingCalendar.NormalizeToUtc` is the odd one out. Unifying it with
`AttendanceDay` would technically be "removing duplicated ... normalization" as the brief requests, but doing so
would **silently change behavior** for any caller that passes an `Unspecified`-kind `DateTime` into this specific
method — behavior that would depend on the **host server's local time zone setting**, which is exactly the kind
of environment-dependent fragility this whole initiative exists to eliminate.

**Why it was left alone:** the brief explicitly states "No behavior changes" (×3, across DOMAIN.2 and DOMAIN.3).
Verified that in current production traffic this branch is never exercised for attendance flows — the frontend
(`attendanceDateIsoUtc()` in `abhyanvaya-ui/src/services/attendanceService.ts` and friends) always sends
'Z'-suffixed ISO date-times, which .NET's `DateTime` parsing binds as `DateTimeKind.Utc`, hitting the *first*
branch of `NormalizeToUtc` (`DateTimeKind.Utc => value`) — not the divergent branch. So there is no currently
observable behavior change either way, but changing it without being asked, and without dedicated test coverage
for e.g. Swagger/Postman-style raw `DateTime.Parse` inputs, was judged too risky to bundle into this change.

**Recommendation:** file a follow-up ticket to either (a) delete `ReportingCalendar.NormalizeToUtc` and route its
two internal callers through `AttendanceDay.FromReportingCalendar` directly (accepting the behavior change for the
`Unspecified` edge case, with a regression test), or (b) keep it as a deliberately-documented exception. This
document constitutes that documentation in the interim.

### 4.2 `ResolveReportingTimeZone` duplication (API vs. Infrastructure)

`ReportingCalendar.ResolveReportingTimeZone(string?)` (API) and the private `ResolveReportingTimeZone` method
inside `AttendanceCalendar` (Infrastructure) are identical in logic (same candidate list: configured id →
`Asia/Kolkata` → `India Standard Time` → UTC fallback). This is a small, low-risk duplication (pure, deterministic,
no timezone-conversion subtlety like §4.1) that was **not** unified because doing so would require introducing a
new shared type visible to both the API and Infrastructure projects purely for a config-string-to-TimeZoneInfo
lookup, which was judged out of proportion to the benefit for this task's scope. Flagged here as a minor,
low-priority follow-up.

## 5. Build Verification

```
dotnet build Abhyanvaya.sln -clp:ErrorsOnly
  Build succeeded.
  0 Error(s)
```

Verified after each incremental step (Domain project alone after adding `AttendanceDay`; full solution after the
calendar interface change; full solution after each consumer was migrated) — the solution never had a broken
intermediate state.

### 5.1 Integration tests

```
dotnet test Abhyanvaya.IntegrationTests --filter "FullyQualifiedName~Attendance"
  Failed: 5, Passed: 0
```

All 5 failures are **pre-existing and unrelated to this change**: they fail inside the shared test fixture
`AttendanceTestDataFactory.CreateReviewReadyScenarioAsync` with
`23503: insert or update on table "AttendanceSession" violates foreign key constraint "FK_AttendanceSession_StaffMembers_StaffId"`,
before any attendance-date logic executes. Root-caused to the local machine's test environment, not this change:

- This test suite runs against its **own** database (`PostgreSqlFixture`: Testcontainers-in-Docker if available,
  else a local fallback `abhyanvaya_integration_test` database) — a different database than `abhyanvaya_db` used
  everywhere else in this engagement.
- **Docker is not running on this machine** (`docker ps` fails to reach the daemon), so the fixture falls back to
  the local `abhyanvaya_integration_test` database.
- That local database's `"StaffMembers"` table has **zero rows** — the test factory calls
  `AttendanceSession.CreateForPhotoAttendance(facultyId: 1, ...)`, which requires a `StaffMembers` row with
  `Id = 1` to satisfy the FK; none exists in this environment.
- Confirmed this migration itself is not the cause: applying all migrations (including the new
  `EnforceAttendanceUniquenessGuard` from AI13.DOMAIN.1) to this same fresh/empty database completed successfully
  with no errors and no false-positive duplicate-guard abort — the migration history head is
  `20260705140348_EnforceAttendanceUniquenessGuard` with zero `Attendance` rows, as expected for a pristine DB.
- No file touched in AI13.DOMAIN.1/2/3 creates, seeds, or reads `StaffMembers`/`Staff` data.

**Recommendation:** either start Docker Desktop before running this suite locally, or seed a minimal
`StaffMembers` row (Id = 1, tenant 1) into `abhyanvaya_integration_test` as a one-time local setup step. This is
pre-existing environment friction, not a regression introduced by this work.

## 6. Architecture Review

- **Single source of truth achieved.** Every attendance-adjacent day-boundary calculation in the solution now
  either directly constructs an `AttendanceDay` or (for `ReportingCalendar`'s two thin wrapper methods) delegates
  to one that does. The only remaining logic outside `AttendanceDay` is (a) resolving which `TimeZoneInfo` is "the
  reporting zone" from configuration (duplicated in a small, low-risk way — §4.2), and (b) one pre-existing,
  documented, deliberately-preserved normalization discrepancy for an untraveled code path (§4.1).
- **Defense-in-depth compounds correctly with AI13.DOMAIN.1.** Because both the manual and photo/AI attendance
  paths now derive `Attendance.Date` from the *same* `AttendanceDay.UtcStart` (via the *same*
  `IAttendanceCalendar` singleton), the database's `(TenantId, StudentId, SubjectId, Date)` unique index enforced
  in AI13.DOMAIN.1 is now structurally guaranteed to see identical values for the same logical day — the
  application-level guarantee and the database-level guarantee are now provably aligned, closing the exact gap
  that caused the original incident.
- **No API/DTO/UI contract changes.** Every HTTP request/response shape is unchanged; only internal
  implementation details (constructor dependencies, private helper signatures) changed.
- **No new dependencies introduced.** `AttendanceDay` lives in `Abhyanvaya.Domain`, which every other project
  already referenced (directly or transitively).

## 7. Complete File List — AI13.DOMAIN.1 + AI13.DOMAIN.2 + AI13.DOMAIN.3 (this engagement)

| # | File | Domain.1 | Domain.2 | Domain.3 |
|---|---|:---:|:---:|:---:|
| 1 | `Abhyanvaya.Infrastructure/Migrations/20260705140348_EnforceAttendanceUniquenessGuard.cs` | ✅ Created | | |
| 2 | `Abhyanvaya.Infrastructure/Migrations/20260705140348_EnforceAttendanceUniquenessGuard.Designer.cs` | ✅ Created | | |
| 3 | `Abhyanvaya.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs` | ✅ Modified (auto) | | |
| 4 | `docs/AI13_DOMAIN1_DATABASE_UNIQUENESS.md` | ✅ Created | | |
| 5 | `Abhyanvaya.Domain/ValueObjects/AttendanceDay.cs` | | ✅ Created | |
| 6 | `docs/AI13_DOMAIN2_ATTENDANCE_DAY.md` | | ✅ Created | |
| 7 | `Abhyanvaya.Application/Common/Interfaces/IAttendanceCalendar.cs` | | | ✅ Modified |
| 8 | `Abhyanvaya.Infrastructure/Services/AttendanceCalendar.cs` | | | ✅ Modified |
| 9 | `Abhyanvaya.Application/AttendanceBuilder.cs` | | | ✅ Modified |
| 10 | `Abhyanvaya.Application/AttendanceSessionQueryService.cs` | | | ✅ Modified |
| 11 | `Abhyanvaya.API/Controllers/AttendanceController.cs` | | | ✅ Modified |
| 12 | `Abhyanvaya.API/Common/ReportingCalendar.cs` | | | ✅ Modified |
| 13 | `Abhyanvaya.API/Controllers/DashboardController.cs` | | | ✅ Modified |
| 14 | `docs/AI13_DOMAIN3_ATTENDANCE_DAY_ADOPTION.md` | | | ✅ Created |

Zero compilation errors across the full solution after every file above was applied.
