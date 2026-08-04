# AI22.8.6.1 — Attendance SLA Indicators

## Bands

| Level | Elapsed age | Status |
|-------|-------------|--------|
| Green | &lt; 15 minutes | On Track |
| Yellow | 15–30 minutes | Watch |
| Orange | 30–60 minutes | At Risk |
| Red | &gt; 60 minutes | Breach |

## Display

Pending session cards and admin/faculty dashboards show:

- SLA badge (`SlaLevel` + `SlaStatus`)
- Elapsed time (`ElapsedDisplay`)
- Expected completion (`ExpectedCompletionUtc` from remaining estimate)
- SLA status text

## Rules

- Operational visibility only — **does not** change workflow transitions.
- Calculated in `AttendanceSlaCalculator` and mapped by `AttendanceSessionDisplayEnricher`.
- Age is based on session `CreatedUtc` (same basis as priority age).
