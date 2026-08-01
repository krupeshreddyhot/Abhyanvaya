# AI30 Phase 2A.5 — Architecture Review

## Compliance checklist

| Item | Status |
|------|--------|
| No Timetable Designer redesign | PASS |
| No Timetable aggregate redesign | PASS |
| No versioning engine redesign | PASS — extended Archive only |
| No approval engine redesign | PASS — additive comments/decision history |
| No conflict detection / Phase 2B | PASS |
| No optimizer / AI | PASS |
| Repository pattern | PASS |
| FluentValidation | PASS |
| Tenant filtering | PASS (BaseEntity + repository tenantId) |
| Soft delete | PASS |
| Audit / change history | PASS (Freeze/Unfreeze/Archive recorded) |
| Permissions seeded | PASS (ids 46–53) |

## Performance notes

- Version comparison loads entries per version in one query each (`ListEntriesForVersionAsync`), then diffs in memory.
- Name lookups batched (subjects/staff/rooms) — avoids N+1 per difference.
- Dashboard archive distribution uses grouped SQL; latest archives limited to 10.
- Comparison UI filters client-side after server filter for search/kind; Excel export reuses same service path.

## Security

- Compare/export gated by VersionCompare permissions.
- Freeze/Unlock gated separately from designer Lock/Unlock.
- Archive reason manage uses Archive.Manage; legacy Scheduling.Archive still authorizes archive endpoints.
- Frozen timetables blocked in `TimetableService.EnsureDraft`.

## Regression surface

Existing Phase 2A flows preserved: versioning, submit/decide, publish, clone, soft validation, change history, governance dashboard KPIs (extended, not replaced).
