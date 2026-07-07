# AI13.DOMAIN.2 — AttendanceDay Domain Value Object

**Role:** Chief Architect review — DDD / Clean Architecture improvement for the Abhyanvaya attendance system.

**Status:** Complete. Build succeeded (0 errors). Value object created and adopted (see AI13.DOMAIN.3 for the
full adoption record).

---

## 1. Objective

Attendance is a business concept — "today's class" — not a raw `DateTime`. Before this change, the notion of "one
reporting day" was represented ad hoc, as a `DateTime` plus a `TimeZoneInfo` passed around independently, with the
actual "day boundary" math (local midnight → UTC instant range) duplicated in three different places:

1. `Abhyanvaya.API.Common.ReportingCalendar` (API layer, used by `AttendanceController` and `DashboardController`).
2. `Abhyanvaya.Infrastructure.Services.AttendanceCalendar` (Infrastructure layer, used by `AttendanceBuilder` and
   `AttendanceSessionQueryService` — itself introduced during the prior "Student Attendance Load Failure" fix,
   specifically *because* the API's version wasn't reachable from the Application/Infrastructure layers).
3. `Abhyanvaya.Domain.Entities.AttendanceSession.GetAttendanceDateUtc()` (Kind-normalization only).

Any drift between these implementations is exactly what caused the prior production incident: two attendance
capture paths anchored "the same calendar day" to two different UTC instants, producing duplicate `Attendance`
rows for the same student/subject/day.

## 2. Current Design (Before)

```
AttendanceController          AttendanceBuilder / AttendanceSessionQueryService
      │                                        │
      ▼                                        ▼
ReportingCalendar                     AttendanceCalendar (Infrastructure)
 .ResolveReportingTimeZone()           .GetDayRangeUtc(DateTime)
 .NormalizeToUtc()                       → NormalizeToUtc (own copy)
 .GetUtcRangeForReportingDayContainingUtc()  → ConvertTimeFromUtc/ToUtc (own copy)
      │                                        │
      ▼                                        ▼
 (DateTime dayStartUtc, DateTime dayEndUtc)   (DateTime dayStartUtc, DateTime dayEndUtc)
```

Two independent implementations of the same "local midnight in reporting zone → UTC instant" formula, each
returning a bare `(DateTime, DateTime)` tuple with no type-level guarantee they mean the same thing.

## 3. New Design (After)

### 3.1 `AttendanceDay` — immutable domain value object

Location: `Abhyanvaya.Domain/ValueObjects/AttendanceDay.cs` (Domain layer — zero dependency on EF Core, ASP.NET
Core, or configuration; only `DateOnly`/`DateTime`/`TimeZoneInfo` from the BCL).

```csharp
public sealed class AttendanceDay : IEquatable<AttendanceDay>
{
    public DateOnly LocalDate { get; }
    public TimeZoneInfo ReportingTimeZone { get; }
    public DateTime UtcStart { get; }   // canonical value for Attendance.Date
    public DateTime UtcEnd   { get; }   // UtcStart + 24h (exclusive)

    public static AttendanceDay FromDate(DateOnly localDate, TimeZoneInfo tz);
    public static AttendanceDay FromUtc(DateTime utcInstant, TimeZoneInfo tz);
    public static AttendanceDay FromReportingCalendar(DateTime value, TimeZoneInfo tz); // normalizes mixed-Kind input
    public static AttendanceDay Today(TimeZoneInfo tz);

    public bool Contains(DateTime utc);
    public bool Equals(AttendanceDay? other);   // value equality by (LocalDate, ReportingTimeZone.Id)
    public override int GetHashCode();          // usable as a Dictionary/GroupBy key
    public override string ToString();          // "2026-07-05 [Asia/Kolkata]"
}
```

### 3.2 Class diagram

```
┌───────────────────────────────┐
│  IAttendanceCalendar (App)    │
│  + GetAttendanceDay(DateTime) │───creates──▶ ┌────────────────────────┐
│  + Today()                    │              │   AttendanceDay (VO)   │
└───────────────┬────────────────┘              │ (Domain.ValueObjects) │
                │ implements                    │────────────────────────│
                ▼                                │ LocalDate: DateOnly    │
┌───────────────────────────────┐              │ ReportingTimeZone: Tz  │
│ AttendanceCalendar (Infra)    │──────uses────▶│ UtcStart / UtcEnd      │
│ - _reportingZone: TimeZoneInfo│              │ Contains() / Equals()  │
└───────────────────────────────┘              └───────────┬────────────┘
                                                              │ consumed by
        ┌───────────────────────────┬──────────────────────┼───────────────────────────┐
        ▼                           ▼                        ▼                           ▼
AttendanceBuilder      AttendanceSessionQueryService   AttendanceController      DashboardController
(Application)          (Application)                   (API, via IAttendanceCalendar) (API, via ReportingCalendar
                                                                                          → AttendanceDay internally)
```

`ReportingCalendar` (API/Common) keeps its exact public static signatures (used by `DashboardController` and
historically by `AttendanceController`) but its body now delegates the local-midnight/UTC-range computation to
`AttendanceDay.FromDate` — see AI13.DOMAIN.3 for the precise before/after and the one deliberate exception
(`NormalizeToUtc`'s Unspecified-kind branch, left untouched — documented there).

### 3.3 Sequence diagram — manual attendance marking (`POST /api/attendance/mark`)

```
Client            AttendanceController        IAttendanceCalendar        AttendanceDay        Attendance (EF)
  │  POST /mark          │                            │                       │                     │
  │  { date, students }  │                            │                       │                     │
  │─────────────────────▶│                            │                       │                     │
  │                      │ GetAttendanceDay(date)     │                       │                     │
  │                      │───────────────────────────▶│                       │                     │
  │                      │                            │ FromReportingCalendar │                     │
  │                      │                            │──────────────────────▶│                     │
  │                      │                            │◀──────────────────────│ AttendanceDay        │
  │                      │◀───────────────────────────│                       │                     │
  │                      │ today = Today()            │                       │                     │
  │                      │───────────────────────────▶│──────────────────────▶│                     │
  │                      │◀───────────────────────────│◀──────────────────────│                     │
  │                      │ if attendanceDay.LocalDate > today.LocalDate → 400 │                     │
  │                      │ Where(a.Date >= attendanceDay.UtcStart            │                     │
  │                      │        && a.Date < attendanceDay.UtcEnd)          │                     │
  │                      │───────────────────────────────────────────────────────────────────────▶│
  │                      │ new Attendance { Date = attendanceDay.UtcStart, ... }                    │
  │                      │───────────────────────────────────────────────────────────────────────▶│
  │◀─────────────────────│  200 OK { Message, Count } │                       │                     │
```

### 3.4 Sequence diagram — photo/AI attendance finalization (`AttendanceBuilder.BuildAsync`)

```
AttendanceSessionFinalizer   AttendanceBuilder        IAttendanceCalendar       AttendanceDay      Attendance (EF)
       │  BuildAsync(sessionId)    │                          │                      │                  │
       │──────────────────────────▶│                          │                      │                  │
       │                           │ GetAttendanceDay(         │                      │                  │
       │                           │   session.GetAttendanceDateUtc())               │                  │
       │                           │─────────────────────────▶│                      │                  │
       │                           │                          │─────────────────────▶│                  │
       │                           │                          │◀─────────────────────│ AttendanceDay     │
       │                           │◀─────────────────────────│                      │                  │
       │                           │ isLocked? existing?  Where(a.Date in            │                  │
       │                           │   [attendanceDay.UtcStart, attendanceDay.UtcEnd))                  │
       │                           │─────────────────────────────────────────────────────────────────▶│
       │                           │ StageAttendances(Date = attendanceDay.UtcStart)                    │
       │                           │─────────────────────────────────────────────────────────────────▶│
       │◀──────────────────────────│  AttendanceBuildSummaryDto│                      │                  │
```

Both paths resolve `attendanceDay` from the **same** `IAttendanceCalendar` implementation, so `attendanceDay.UtcStart`
is bit-for-bit identical for the same calendar day — this is the structural guarantee that closes the original
incident, now enforced by a single, testable, immutable type instead of by convention across three call sites.

## 4. Migration Strategy

- **No database schema change.** `Attendance.Date` remains a `DateTime` (`timestamp with time zone`) column.
  `AttendanceDay` is a pure in-memory/application-layer concept; only its `.UtcStart` value ever reaches the
  database, exactly as the raw UTC `DateTime` did before.
- **No breaking change to any public contract.** DTOs, HTTP request/response shapes, and the recognition pipeline
  are untouched — verified in AI13.DOMAIN.3's build/test verification.
- **Incremental, additive rollout:** `AttendanceDay` was introduced as a new file with no consumers, then adopted
  one call site at a time (documented in AI13.DOMAIN.3), so at every step the solution built and behavior was
  preserved.

## 5. Benefits

1. **Single source of truth** for "what UTC instant does this reporting day start/end at" — eliminates the class
   of bug that caused the prior incident by construction, not by convention.
2. **Type safety.** A method that needs "a day" now asks for an `AttendanceDay`, not two loosely-related
   `DateTime` parameters that could be passed in the wrong order or computed by different logic.
3. **Usable as a dictionary/`GroupBy` key** (implements value equality + `GetHashCode`), enabling cleaner
   reporting code (see the `DashboardController.GetMonthlyTrend` refactor in AI13.DOMAIN.3).
4. **Self-documenting call sites** — `_attendanceCalendar.GetAttendanceDay(date)` communicates intent far better
   than `ReportingCalendar.GetUtcRangeForReportingDayContainingUtc(ReportingCalendar.NormalizeToUtc(date), tz)`.
5. **Testable in isolation.** `AttendanceDay` has no infrastructure dependencies, so its day-boundary math can be
   unit tested directly (e.g. DST transition dates, year boundaries) without a database or HTTP context.

## 6. Files Modified / Created (DOMAIN.2 scope)

| File | Change |
|---|---|
| `Abhyanvaya.Domain/ValueObjects/AttendanceDay.cs` | **Created.** The value object itself. |
| `docs/AI13_DOMAIN2_ATTENDANCE_DAY.md` | **Created.** This document. |

The adoption of `AttendanceDay` across `IAttendanceCalendar`, `AttendanceCalendar`, `AttendanceBuilder`,
`AttendanceSessionQueryService`, `AttendanceController`, `ReportingCalendar`, and `DashboardController` is recorded
in full, file-by-file, in `docs/AI13_DOMAIN3_ATTENDANCE_DAY_ADOPTION.md` (AI13.DOMAIN.3).

## 7. Architecture Impact

- **DDD alignment:** `AttendanceDay` is a textbook value object — immutable, defined entirely by its value
  (`LocalDate` + `ReportingTimeZone`), with no identity of its own, living in the Domain layer alongside
  `ClassroomImageMetadata` (the existing value-object convention in this codebase).
- **Clean Architecture respected:** the Domain layer stays free of infrastructure/config concerns — it does not
  know *which* time zone is "the" reporting zone; that decision still lives in `AttendanceCalendar`
  (Infrastructure), which reads `Dashboard:ReportingTimeZoneId` from configuration and hands a resolved
  `TimeZoneInfo` to `AttendanceDay`'s factories.
- **No new cross-project dependencies were introduced** beyond what already existed: `Abhyanvaya.Application` and
  `Abhyanvaya.Infrastructure` already referenced `Abhyanvaya.Domain`; `Abhyanvaya.API` already referenced both.
