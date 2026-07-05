# AI13.DOMAIN.5 — Remove Remaining ReportingCalendar Duplication

## Objective

Complete the `AttendanceDay` refactoring started in AI13.DOMAIN.2/.3 by eliminating every remaining
duplicated attendance-day calculation, so `AttendanceDay` is the **single** implementation of
attendance calendar logic:

```
AttendanceDay  →  AttendanceCalendar (IAttendanceCalendar)  →  Consumers
```

No behaviour changes, no timezone changes, no database changes, no DTO changes, no API changes, no
UI changes. Build must succeed with zero errors.

---

## 1. Solution-Wide Search & Classification

Searched the entire solution (`**/*.cs`) for every pattern named in the brief.

| Pattern | Matches found | Classification |
|---|---|---|
| `ReportingCalendar` (type usage) | `Abhyanvaya.API/Controllers/DashboardController.cs` (3 call sites) | **Needed replacement** |
| `NormalizeToUtc` | `ReportingCalendar.NormalizeToUtc` (1 definition + 1 caller in `DashboardController`); `AttendanceDay`'s own private `NormalizeToUtc` (2 internal call sites) | `ReportingCalendar`'s copy: **needed replacement**. `AttendanceDay`'s own copy: **should remain** (it *is* the canonical implementation). |
| `GetUtcRangeForReportingDayContainingUtc` | `ReportingCalendar.cs` (definition + internal use) + 1 caller in `DashboardController` | **Needed replacement** |
| `GetReportingTodayUtcRange` | *(no exact match — this name doesn't exist in the codebase)* | N/A. Closest equivalent, `ReportingCalendar.GetReportingDayUtcRangeForUtcNow`, is covered below. |
| `GetUtcRangeForReportingCalendarDate` | `ReportingCalendar.cs` (definition — already a thin `AttendanceDay.FromDate` wrapper since AI13.DOMAIN.3) + 2 callers in `DashboardController` | **Needed replacement** (callers moved to call the canonical source directly through `IAttendanceCalendar`, so the wrapper method itself could be deleted) |
| `TimeZoneInfo.ConvertTime*` | `AttendanceDay.cs` (2 call sites — canonical) + `ReportingCalendar.cs` (1 call site, `ConvertTimeFromUtc`) | `AttendanceDay`'s: **should remain**. `ReportingCalendar`'s: **removed with the type**. |
| `ConvertTimeToUtc` / `ConvertTimeFromUtc` | Same as above | Same as above |
| `TimeZoneInfo.FindSystemTimeZoneById` (tz *resolution*, not conversion) | `ReportingCalendar.ResolveReportingTimeZone` (public, API layer) + `AttendanceCalendar.ResolveReportingTimeZone` (private, Infrastructure layer) — **identical candidate list, duplicated logic** | **Needed replacement** — API layer's copy removed; `DashboardController` now resolves everything through `IAttendanceCalendar` instead of calling a tz-resolution helper itself. |

No other files in the solution (controllers, services, background workers, tests) reference
`ReportingCalendar`, `NormalizeToUtc`, or any of the `GetUtcRangeFor...` helpers — `DashboardController`
was the only remaining consumer of the old static helper class.

---

## 2. Root Cause: Two Time-Zone Resolution Implementations

Before this change, the exact same "resolve reporting IANA/Windows time zone id, falling back to
`Asia/Kolkata` → `India Standard Time` → UTC" logic existed **twice**:

```
Abhyanvaya.API.Common.ReportingCalendar.ResolveReportingTimeZone(string?)          — public static
Abhyanvaya.Infrastructure.Services.AttendanceCalendar.ResolveReportingTimeZone(string?) — private
```

`DashboardController` called the API-layer copy directly (reading `IConfiguration` itself on every
request), while every other attendance code path (via `IAttendanceCalendar`) used the
Infrastructure-layer copy (resolved once per singleton instance). This was the last structural
duplication left over from before AI13.DOMAIN.2/.3.

---

## 3. Refactor

### 3.1 `IAttendanceCalendar` — one new method, no leaked `TimeZoneInfo`

Rather than expose the raw `TimeZoneInfo` (which would leak an infrastructure-flavoured concept and
require every caller to know how to build an `AttendanceDay` from it), a small factory method was
added so callers can still express "give me the attendance day for this explicit calendar date"
without ever touching a `TimeZoneInfo`:

```csharp
public interface IAttendanceCalendar
{
    AttendanceDay GetAttendanceDay(DateTime date);
    AttendanceDay Today();
    AttendanceDay ForCalendarDate(int year, int month, int day); // new (AI13.DOMAIN.5)
}
```

```csharp
// Abhyanvaya.Infrastructure/Services/AttendanceCalendar.cs
public AttendanceDay ForCalendarDate(int year, int month, int day) =>
    AttendanceDay.FromDate(new DateOnly(year, month, day), _reportingZone);
```

This is additive only — `GetAttendanceDay` and `Today()` are untouched.

### 3.2 `DashboardController` — three call sites migrated

| Method | Before | After |
|---|---|---|
| `GetOverview` (today's present/absent) | `ReportingCalendar.ResolveReportingTimeZone(_configuration[...])` + `ReportingCalendar.GetReportingDayUtcRangeForUtcNow(DateTime.UtcNow, tz)` | `_attendanceCalendar.Today()` → `.UtcStart` / `.UtcEnd` |
| `GetMonthlyTrend` (month range + per-day grouping) | `ReportingCalendar.ResolveReportingTimeZone(...)` + 2× `ReportingCalendar.GetUtcRangeForReportingCalendarDate(...)` + `GroupBy(x => AttendanceDay.FromUtc(DateTime.SpecifyKind(x.Date, DateTimeKind.Utc), tz))` | `_attendanceCalendar.ForCalendarDate(year, month, 1).UtcStart` / `_attendanceCalendar.ForCalendarDate(year, month, lastDay).UtcEnd` + `GroupBy(x => _attendanceCalendar.GetAttendanceDay(x.Date))` |
| `GetClassDashboard` (single day range) | `ReportingCalendar.ResolveReportingTimeZone(...)` + `ReportingCalendar.NormalizeToUtc(date)` + `ReportingCalendar.GetUtcRangeForReportingDayContainingUtc(utc, tz)` | `_attendanceCalendar.GetAttendanceDay(date)` → `.UtcStart` / `.UtcEnd` — the exact same one-liner already used by `AttendanceController.GetAttendance` |

`DashboardController` no longer injects `IConfiguration` at all (it had no other use for it) — it
now takes `IAttendanceCalendar` instead, matching the constructor shape already used by
`AttendanceController`.

### 3.3 `ReportingCalendar` — removed (unused, not just slimmed)

After the `DashboardController` migration, `ReportingCalendar` had **zero remaining callers anywhere
in the solution** (confirmed by a final solution-wide grep). Per the task's own instruction —
*"ReportingCalendar should become a thin compatibility wrapper OR be removed if unused"* — since it
was fully unused, `Abhyanvaya.API/Common/ReportingCalendar.cs` was deleted outright rather than kept
as a dead wrapper.

---

## 4. Before / After Dependency Diagrams

### Before (start of AI13.DOMAIN.5)

```
                     ┌───────────────────────────┐
                     │        AttendanceDay        │  (canonical day-boundary math)
                     └──────────────┬──────────────┘
                     ┌──────────────┴──────────────┐
                     ▼                             ▼
        ┌─────────────────────────┐   ┌─────────────────────────────┐
        │  AttendanceCalendar      │   │  ReportingCalendar            │
        │  (Infrastructure)        │   │  (API.Common, static)         │
        │  - own ResolveReporting-  │   │  - own ResolveReportingTimeZone│
        │    TimeZone (private)    │   │    (DUPLICATE candidate list) │
        │  - GetAttendanceDay      │   │  - GetUtcRangeForReporting*    │
        │  - Today()               │   │    (delegates to AttendanceDay │
        └──────────┬───────────────┘   │    since AI13.DOMAIN.3)       │
                   │                   │  - NormalizeToUtc (OWN COPY,  │
                   │                   │    subtly different Unspecified│
                   │                   │    handling — documented in   │
                   │                   │    AI13.DOMAIN.3)              │
                   │                   └──────────────┬────────────────┘
                   ▼                                  ▼
        AttendanceController                 DashboardController
        AttendanceBuilder                     (only remaining caller)
        AttendanceSessionQueryService
        AttendanceSessionFinalizer
```

### After (end of AI13.DOMAIN.5)

```
                     ┌───────────────────────────┐
                     │        AttendanceDay        │  (canonical day-boundary math —
                     └──────────────┬──────────────┘   ONLY implementation left)
                                    │
                                    ▼
                     ┌───────────────────────────┐
                     │  AttendanceCalendar         │  (Infrastructure — ONLY tz-resolution
                     │  (IAttendanceCalendar)      │   implementation left)
                     │  - ResolveReportingTimeZone  │
                     │  - GetAttendanceDay          │
                     │  - Today()                   │
                     │  - ForCalendarDate() (new)    │
                     └──────────────┬──────────────┘
                                    │
        ┌────────────┬─────────────┼──────────────┬──────────────────┐
        ▼            ▼             ▼               ▼                  ▼
AttendanceController  AttendanceBuilder  AttendanceSessionQueryService  AttendanceSessionFinalizer  DashboardController
                                                                                                    (migrated, AI13.DOMAIN.5)

ReportingCalendar.cs — DELETED (zero remaining callers)
```

Every attendance-adjacent consumer now depends on exactly one abstraction
(`IAttendanceCalendar`), which in turn depends on exactly one value object (`AttendanceDay`) for all
day-boundary and time-zone-conversion math.

---

## 5. Architecture Review

- **Single source of truth achieved.** `AttendanceDay` is now the only place `TimeZoneInfo.ConvertTimeToUtc`/
  `ConvertTimeFromUtc` are called for attendance purposes. `AttendanceCalendar` is now the only place
  the reporting time zone is resolved from configuration (`ResolveReportingTimeZone`). No other type
  in the solution computes attendance-day UTC ranges independently.
- **Clean Architecture preserved.** `IAttendanceCalendar` remains the Application-layer port;
  `AttendanceCalendar` remains its Infrastructure-layer adapter; `AttendanceDay` remains a pure
  Domain value object with no framework dependencies. `DashboardController` (API layer) now depends
  only on the same Application-layer abstraction every other attendance controller already used —
  it no longer reaches past Application into a bespoke API-layer static helper.
- **Encapsulation improved.** `IAttendanceCalendar.ForCalendarDate(y, m, d)` lets `DashboardController`
  build month-boundary `AttendanceDay`s without ever obtaining a raw `TimeZoneInfo` — the reporting
  zone stays fully encapsulated inside `AttendanceCalendar`, closing the one gap that previously
  forced a caller (`DashboardController`) to ask a static helper to resolve a zone itself.
- **Consistency, not new behaviour.** `GetClassDashboard`'s day-range calculation now uses the exact
  same `_attendanceCalendar.GetAttendanceDay(date)` call already used by
  `AttendanceController.GetAttendance(int subjectId, DateTime date)` — an endpoint with an identical
  `DateTime date` query-parameter shape. This removes the last place attendance-day math could ever
  silently drift from the canonical implementation.

---

## 6. Regression Analysis

| Call site | Old computation | New computation | Behavioural equivalence |
|---|---|---|---|
| `GetOverview` "today" range | `ReportingCalendar.GetReportingDayUtcRangeForUtcNow(DateTime.UtcNow, tz)` → `GetUtcRangeForReportingDayContainingUtc` → `NormalizeToUtc` (Utc-kind passthrough, no-op for `DateTime.UtcNow`) → `ConvertTimeFromUtc` → `AttendanceDay.FromDate` | `AttendanceCalendar.Today()` → `AttendanceDay.Today(tz)` → `FromUtc(DateTime.UtcNow, tz)` → same `ConvertTimeFromUtc` → `AttendanceDay.FromDate` | **Identical.** Input is always `DateTimeKind.Utc` (`DateTime.UtcNow`), so both normalization paths are no-ops; every downstream step is the same `AttendanceDay` factory call. |
| `GetMonthlyTrend` month boundaries | `ReportingCalendar.GetUtcRangeForReportingCalendarDate(y, m, d, tz)` → `AttendanceDay.FromDate(new DateOnly(y, m, d), tz)` (already a thin wrapper since AI13.DOMAIN.3) | `AttendanceCalendar.ForCalendarDate(y, m, d)` → `AttendanceDay.FromDate(new DateOnly(y, m, d), tz)` | **Identical.** Byte-for-byte the same downstream call; only the call-site indirection changed. |
| `GetMonthlyTrend` per-row grouping key | `AttendanceDay.FromUtc(DateTime.SpecifyKind(x.Date, DateTimeKind.Utc), tz)` (in-memory `GroupBy`, `tz` from `ReportingCalendar.ResolveReportingTimeZone`) | `_attendanceCalendar.GetAttendanceDay(x.Date)` → `AttendanceDay.FromReportingCalendar(x.Date, tz)` → `FromUtc(NormalizeToUtc(x.Date), tz)`, where `AttendanceDay`'s own `NormalizeToUtc` treats `DateTimeKind.Unspecified` (what Npgsql returns for these columns) as `DateTime.SpecifyKind(value, DateTimeKind.Utc)` — **the same transformation** the old code performed explicitly via `DateTime.SpecifyKind(x.Date, DateTimeKind.Utc)` before calling `FromUtc`. | **Identical.** Both paths end up calling `TimeZoneInfo.ConvertTimeFromUtc` on the same UTC instant with the same `tz` (now sourced from the single `AttendanceCalendar` instance instead of a fresh `ReportingCalendar.ResolveReportingTimeZone(_configuration[...])` call — same configuration key, same fallback candidates, same result). |
| `GetClassDashboard` day range | `ReportingCalendar.NormalizeToUtc(date)` then `GetUtcRangeForReportingDayContainingUtc` | `_attendanceCalendar.GetAttendanceDay(date)` | **Identical for every input this endpoint receives in production.** See risk note below — the only theoretical divergence is for `DateTimeKind.Unspecified` inputs, which this endpoint's `DateTime date` query parameter never carries in practice (see §7). |

**No timezone id, fallback order, or configuration key changed.** Both old and new resolution paths
use `configuration["Dashboard:ReportingTimeZoneId"]` with the identical fallback chain
(`configuredId → "Asia/Kolkata" → "India Standard Time" → UTC`).

---

## 7. Risk Assessment

| Risk | Analysis | Verdict |
|---|---|---|
| `ReportingCalendar.NormalizeToUtc` vs. `AttendanceDay`'s private `NormalizeToUtc` diverge for `DateTimeKind.Unspecified` inputs (documented in AI13.DOMAIN.3: `ReportingCalendar`'s version applies `.ToUniversalTime()` — a server-local-time shift — before tagging UTC; `AttendanceDay`'s version does not shift, it treats the value as already representing the intended instant). `GetClassDashboard`'s `date` parameter now goes through `AttendanceDay`'s normalization instead of `ReportingCalendar`'s. | `GetClassDashboard(int subjectId, DateTime date)` binds `date` from the query string exactly like `AttendanceController.GetAttendance(int subjectId, DateTime date)`, which **already** used `_attendanceCalendar.GetAttendanceDay(date)` since AI13.DOMAIN.3 with no reported issues. The AI13.DOMAIN.3 analysis already established that the frontend always sends `'Z'`-suffixed ISO date-times for these query parameters, so `Kind` is always `Utc` in production traffic — the branch where the two implementations disagree (`Unspecified`) is not exercised today by either endpoint. | **Low risk, consistent with an already-adopted, already-verified pattern.** Flagged here transparently per the task's regression-analysis requirement. |
| Removing `ReportingCalendar.cs` outright instead of keeping it as a wrapper could break an external/undiscovered caller. | Confirmed via solution-wide `grep` (multiple passes, different patterns) that `DashboardController` was the only caller of any `ReportingCalendar` member, and no test project references it. The full solution build (`dotnet build`) would fail immediately if any reference remained — it succeeded with 0 errors. | **No risk — verified by build.** |
| `DashboardController` constructor signature changed (`IConfiguration` → `IAttendanceCalendar`). | No code anywhere constructs `DashboardController` directly (`grep "new DashboardController"` → no matches); it is only ever instantiated by the ASP.NET Core DI container, which already registers `IAttendanceCalendar` as a singleton (`Abhyanvaya.Infrastructure/DependencyInjection.cs`). | **No risk.** |
| Adding `ForCalendarDate` to `IAttendanceCalendar` is a breaking interface change for any other implementer. | `AttendanceCalendar` is the only implementation of `IAttendanceCalendar` in the solution (confirmed by grep); no mocks/fakes implement it in tests (attendance integration tests use the real DI-registered singleton). | **No risk.** |

---

## 8. Build Verification

```
dotnet build Abhyanvaya.sln
  ...
  Build succeeded.
      0 Error(s)
```

All pre-existing nullable-reference warnings are unchanged in nature and count from before this
task; no new warnings were introduced by these changes.

---

## 9. Files Created / Modified / Removed

**Created**

- `docs/AI13_DOMAIN5_REPORTING_CALENDAR_SIMPLIFICATION.md` — this document

**Modified**

- `Abhyanvaya.Application/Common/Interfaces/IAttendanceCalendar.cs` — added `ForCalendarDate(int year, int month, int day)`
- `Abhyanvaya.Infrastructure/Services/AttendanceCalendar.cs` — implemented `ForCalendarDate`
- `Abhyanvaya.API/Controllers/DashboardController.cs` — replaced all `ReportingCalendar` usage with `IAttendanceCalendar`; removed unused `IConfiguration` dependency and `Abhyanvaya.Domain.ValueObjects` import

**Removed**

- `Abhyanvaya.API/Common/ReportingCalendar.cs` — fully unused after the `DashboardController` migration; deleted rather than kept as a dead wrapper, per the task's instruction to remove it if unused
