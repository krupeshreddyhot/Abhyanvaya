# AI29.1D.24B.2 Prompt 9.2 — JWT Cross-Tenant Security Hardening

**Date:** 2026-08-11  
**Production delta in Prompt 9:** **None** — JwtService contract already hardened in Prompt 8 / 8A.  
**Review result:** Existing fix is compatible with required security chain; no further JwtService change.

## Security chain (verified)

```
Authenticated User
  → UserApplicationRoles (ownership)
  → ApplicationRole (IgnoreQueryFilters + role.TenantId == user.TenantId + !IsDeleted)
  → ApplicationRolePermissions (by assigned role IDs only)
  → JWT permission claims
```

### Non-bypass rule

`IgnoreQueryFilters()` applies **only** to authentication-time `ApplicationRole` lookup so ambient login TenantId cannot hide same-tenant roles. It does **not** authorize:

- scanning foreign-tenant roles,
- granting permissions from unrelated Tenant B roles,
- elevating through corrupt `UserApplicationRole` links to other tenants’ admin roles.

Same-tenant match remains mandatory. Permissions are never loaded by open-ended cross-tenant permission scans.

## Prompt 8 defect (retained fix)

Ambient TenantId filtering during login dropped assigned ApplicationRole rows → LegacyFacultySet fallback → missing `Section.View`.

Fix retained in `Abhyanvaya.Infrastructure/Services/JwtService.cs` (see inline comments).

## Prompt 9 Tests A–F mapping

Implemented and executed via  
`AI29_1D_24B2_Prompt8A_JwtPermissionIsolationTests` (+ Prompt 9 named wrappers):

| Prompt 9 | Coverage | Result |
|----------|----------|--------|
| **TEST A** Tenant A user + Tenant A role → Tenant A permissions | `TestA_TenantA_user_receives_TenantA_role_permissions` | PASS |
| **TEST B** Tenant A user + unrelated Tenant B role → no Tenant B permissions | `TestB_…` + cross-tenant ARP leak tests | PASS |
| **TEST C** Tenant A cannot obtain Tenant B admin permissions via IgnoreQueryFilters | `TestC_IgnoreQueryFilters_does_not_grant_cross_tenant_role` | PASS |
| **TEST D** Missing/invalid role assignment → no unauthorized permissions | `TestE_No_ApplicationRole_uses_LegacyFacultySet` + TestC | PASS |
| **TEST E** Section.View resolution continues | TestA / TestF include permission id 210 | PASS |
| **TEST F** Attendance permissions continue | Attendance.View / Attendance.Manage in TestA/F | PASS |

Domain note: mark authority is **`Attendance.Manage`** (not a separate `Attendance.Mark` key).

## What was not done

- No second permission system.
- No weakening of tenant authorization.
- No Attendance / Timetable / Section / Allocation business-logic changes.
