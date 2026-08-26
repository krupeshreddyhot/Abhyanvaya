# AI29.1D.24B.2 Prompt 8A — JWT Permission Hardening

**Date:** 2026-08-11  
**FINAL STATUS: PASS**

## 1. Original defect

Faculty application-role assignments (including `Section.View`) were silently omitted from JWT `permission` claims at login. The token fell back to `LegacyFacultySet`, so `GET /api/sections` returned 403 and optional Section attendance could not load section options.

## 2. Root cause

`ApplicationRole` is `BaseEntity`-scoped (tenant + soft-delete query filter). During login, ambient `ICurrentUserService.TenantId` is often `0`/unset. Navigating `UserApplicationRoles → ApplicationRole` under that filter yielded **zero roles**, so JwtService used Faculty legacy fallback and dropped assigned keys such as `Section.View`.

## 3. Current solution

In `JwtService.ResolvePermissionKeysAsync`:

1. Resolve role IDs from `UserApplicationRoles` where `UserId == user.Id`.
2. Join `ApplicationRole` with `IgnoreQueryFilters()` **and** `role.TenantId == user.TenantId` and `!role.IsDeleted`.
3. Load `ApplicationRolePermissions` **only** for those role IDs.
4. Emit JWT `permission` claims from those keys.
5. Preserve SuperAdmin catalog / Admin `PermissionKeys.All` / Faculty `LegacyFacultySet` fallbacks when no qualifying assigned-role permissions exist.

## 4. Tenant isolation model

```
Authenticated User
  → UserApplicationRoles (user-owned)
  → ApplicationRole IDs (same TenantId as user)
  → ApplicationRolePermissions for those IDs only
  → Permission claims
```

Cross-tenant roles are rejected even if a corrupt `UserApplicationRole` row points at them.

## 5. IgnoreQueryFilters safety rationale

> IgnoreQueryFilters() bypasses the global filter only for the ApplicationRole lookup during authentication; the role IDs remain constrained by the authenticated user's UserApplicationRoles relationship.

Additionally enforced: `role.TenantId == user.TenantId`. It is **not** a scan of all roles across tenants.

## 6. Permission claim flow

Login → `GenerateTokenAsync` → `ResolvePermissionKeysAsync` → one JWT claim per key (`type=permission`).

## 7. Legacy fallback behavior

Faculty with no same-tenant ApplicationRole permissions → `PermissionKeys.LegacyFacultySet` (no `Section.View`).

## 8. SuperAdmin behavior

Unchanged: all `Permission.Key` values from the catalog.

## 9. Admin behavior

Unchanged: assigned ApplicationRole permissions when present; otherwise `PermissionKeys.All`.

## 10. Faculty behavior

Assigned ApplicationRole permissions when present (including `Section.View` when granted); otherwise LegacyFacultySet. No-timetable manual attendance remains independent of timetable assignment.

## 11. Negative authorization tests

Covered by `AI29_1D_24B2_Prompt8A_JwtPermissionIsolationTests`:

| Case | Result |
|------|--------|
| Attendance-only role excludes `Section.View` | PASS |
| Program.View-only excludes Section create/manage | PASS |
| Unauthenticated has no permission claims | PASS |
| Tenant A cannot receive Tenant B permissions | PASS (unit) |
| Corrupt cross-tenant role link rejected | PASS |

## 12. Architecture Guard investigation

| Item | Finding |
|------|---------|
| Prompt 8 raw artifact | `ArchGuard => Failed! Failed: 1, Passed: 28, Skipped: 0` (`--no-build` batch) |
| Prompt 8 summary | Later reported 29/0 after rebuild |
| Authoritative rule | **Raw test-runner output wins**; do not invent 29/0 from a later summary alone |
| Exact failure class | Environmental / race — shared write of `docs/architecture/AI29_1D_architecture_compliance.json` under parallel Prompt 21 / 21A execution; possible stale `--no-build` DLL during API file-lock rebuild |
| JwtService causation | **No** — failure mode was guard snapshot parallelism / stale binary, not a JwtService architecture violation |
| Guard correctness | Guard rules remain valid; not weakened |
| Remediation | `[Collection("AI29.1D.ArchitectureGuard")]` + `DisableParallelization = true` for Prompt 21 / 21A |

## 13. Final Architecture Guard result (authoritative)

```
Passed!  - Failed:     0, Passed:    29, Skipped:     0, Total:    29
```

## 14. Regression counts

| Suite | Passed | Failed | Skipped |
|-------|--------|--------|---------|
| AI29 | 469 | 0 | 0 |
| AI29.1A | 53 | 0 | 0 |
| AI29.1A.5 | 14 | 0 | 0 |
| AI29.1A.6 | 18 | 0 | 0 |
| AI29.1A.7 | 12 | 0 | 0 |
| AI29.1B | 41 | 0 | 0 |
| AI29.1B.5 | 11 | 0 | 0 |
| AI29.1B.7 | 16 | 0 | 0 |
| AI29.1C | 36 | 0 | 0 |
| AI29.1C.5 | 28 | 0 | 0 |
| AI29.1C.5A | 16 | 0 | 0 |
| AI29.1D | 331 | 0 | 0 |
| AI29.1D.10A | 20 | 0 | 0 |
| AI29.1D.15A | 73 | 0 | 0 |
| AI29.1D.24 | 115 | 0 | 0 |
| AI29.1D.24A | 2 | 0 | 0 |
| AI29.1D.24B | 36 | 0 | 0 |
| AI29.1D.24B.2 | 32 | 0 | 0 |
| AI22 Attendance | 33 | 0 | 0 |
| AI30 Scheduling / Optimization | 165 | 0 | 0 |
| AI31 Faculty / Dashboard | 71 | 0 | 0 |
| Architecture Guard | 29 | 0 | 0 |
| AttendanceSessionResolver | 22 | 0 | 0 |
| Prompt 8A JwtIsolation | 12 | 0 | 0 |

## 15. API build

**PASS** — rebuilt and restarted on `http://localhost:5210` with current `JwtService`.

## 16. UI build

**PASS** — `npm run build` in `abhyanvaya-ui`.

## 17. Production files changed

| File | Change |
|------|--------|
| `Abhyanvaya.Infrastructure/Services/JwtService.cs` | Tenant-safe role join (`IgnoreQueryFilters` + `role.TenantId == user.TenantId`) |
| `Abhyanvaya.Application.UnitTests/.../AI29_1D_24B2_Prompt8A_JwtPermissionIsolationTests.cs` | Isolation + JWT claim tests A–H / negatives |
| `Abhyanvaya.Application.UnitTests/.../AI29_1D_Prompt21*_ArchitectureGuard*.cs` | Serialized collection to eliminate snapshot race |
| `Abhyanvaya.Application.UnitTests/.../Ai291DArchitectureGuardTestCollection.cs` | Collection definition |
| `Abhyanvaya.Application.UnitTests/Abhyanvaya.Application.UnitTests.csproj` | EF Core InMemory for JWT tests |

## 18. Database changes

**None.**

## 19. Known limitations

1. Live multi-tenant Faculty login: **NOT EXECUTED — DATA UNAVAILABLE** (single validation college). Cross-tenant isolation proven in unit tests A–D.
2. Domain uses `Attendance.Manage` (not a separate `Attendance.Mark` key); claim tests assert `Attendance.Manage`.
3. Prompt 8 browser no-timetable hard gate remains the live attendance evidence; not re-driven end-to-end in 8A beyond resolver/API confirmation.

## Gate summary

| Gate | Result |
|------|--------|
| JWT assigned permissions | PASS |
| Tenant isolation | PASS (unit) / live multi-tenant NOT EXECUTED |
| Cross-tenant permission leakage | 0 |
| Legacy Faculty fallback | PASS |
| SuperAdmin | PASS |
| Admin | PASS |
| No-timetable Attendance | PASS |
| Section.View | PASS |
| Architecture Guard | 0 failures (29 passed) |
| API build | PASS |
| UI build | PASS |
| Full regression | PASS |
