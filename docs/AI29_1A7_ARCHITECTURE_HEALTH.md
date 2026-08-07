# AI29.1A.7 — Architecture Health

## Health model (advisory)

| Level | Meaning |
|-------|---------|
| Healthy | Within budget / checks passed |
| Warning | Soft degradation (slow cache, guard violations) |
| Critical | Probe failure (e.g. tree build exception) |

`IAcademicHealthService` **never**:

- invalidates caches
- restarts services
- modifies hierarchy
- repairs data

## Architecture trends

Entity: `AcademicArchitectureTrends`

Captured by:

- Background service (every 6 hours when `EnableArchitectureMetrics`)
- Explicit `POST .../architecture/trends/capture`

Score = `100 - 10 * violationCount` (floor 0). Runtime is never blocked by guard failures.
