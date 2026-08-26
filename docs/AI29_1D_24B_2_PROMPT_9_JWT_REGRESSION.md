# AI29.1D.24B.2 Prompt 9.3 — JWT Regression

**Date:** 2026-08-11  
**Rule:** Login success alone is insufficient — permission claims / authorization behavior verified.

## Live claim verification (Faculty-A)

From discovery (`prompt9-discovery.json`):

| Check | Result |
|-------|--------|
| Login HTTP | 200 |
| `permission` claims count | 7 |
| `Section.View` | present |
| `Attendance.View` | present |
| `Attendance.Manage` | present |
| `Program.Manage` | absent |
| `GET /api/sections` | **200** (not 403) |
| Resolver mode | Legacy / `hasTimetable=false` |

## Automated results

Skipped never counted as passed.

| Suite | Passed | Failed | Skipped |
|-------|--------|--------|---------|
| Prompt 9 JWT cross-tenant wrappers (TEST A–F) | 6 | 0 | 0 |
| Prompt 8A JWT isolation | 12 | 0 | 0 |
| Prompt 8 JwtRolePermissionResolution | 1 | 0 | 0 |
| JWT security bundle (8+8A+9) | 19 | 0 | 0 |
| Prompt 16 / 16A | 18 | 0 | 0 |
| Architecture Guard Prompt 21/21A | 29 | 0 | 0 |
| Broader authz/JWT/tenant filter (earlier Prompt 9.3 run) | 78 | 0 | 0 |

### Prompt 18

No dedicated `Prompt18*` unit-test class exists in the repository. Permission/tenant isolation principles from `docs/AI29_1D_PROMPT_18_PERMISSIONS_TENANT_ISOLATION.md` are covered by Prompt 16/16A permission cases, JWT isolation Tests A–F, and live Faculty claim checks above. **Not marked PASS via skip.**

## Verdict

**JWT security / regression: PASS**
