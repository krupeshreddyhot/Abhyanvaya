# AI29.1D.24B.2 Prompt 8A — JWT Discovery

**Date:** 2026-08-11  
**Scope:** Read-only discovery of `JwtService` permission resolution (Prompt 8A Task 1)

## Entry point

`AuthController` college login loads the tenant-scoped `User`, then calls:

`IJwtService.GenerateTokenAsync(user)` → `JwtService`.

## Claim construction

Claims always include: `UserId`, `ClaimTypes.Role`, `TenantId`, `CourseId`, `GroupId`, `StaffId`, `must_change_password`.

Each resolved permission key is emitted as a separate claim:

`new Claim("permission", key)`.

## Permission resolution flow (`ResolvePermissionKeysAsync`)

```
User (authenticated / just verified)
  │
  ├─ SuperAdmin → all Permission.Key from catalog (no role join)
  │
  ├─ Else:
  │     UserApplicationRoles (UserId == user.Id)
  │           ↓ join
  │     ApplicationRole (IgnoreQueryFilters + !IsDeleted + TenantId == user.TenantId)
  │           ↓ roleIds
  │     ApplicationRolePermissions WHERE ApplicationRoleId ∈ roleIds
  │           ↓
  │     Permission.Key → JWT permission claims
  │
  └─ If no same-tenant assigned role permissions:
        Admin  → PermissionKeys.All
        Faculty → PermissionKeys.LegacyFacultySet
        other → empty
```

## Tenant filtering

- `ApplicationRole` inherits `BaseEntity` → global tenant + soft-delete query filter.
- During login, ambient `ICurrentUserService.TenantId` is often `0` / unset.
- Navigating `uar.ApplicationRole` under that ambient filter hid tenant-scoped roles and forced LegacyFacultySet fallback (Prompt 8 defect).
- Current fix: `IgnoreQueryFilters()` **only** on the `ApplicationRole` join, still constrained by:
  1. `uar.UserId == user.Id` (assignment authority)
  2. `role.TenantId == user.TenantId` (tenant ownership)

## LegacyFacultySet

When no qualifying ApplicationRole permissions exist for Faculty:

`Students.View`, `Attendance.View`, `Attendance.Manage`, `Reports.View`, `Dashboard.View`, `Master.View`  
(**does not** include `Section.View`).

## SuperAdmin / Admin

| Role | Behavior |
|------|----------|
| SuperAdmin | Full permission catalog query |
| Admin without app roles | `PermissionKeys.All` |
| Admin with app roles | Assigned role permissions only (same path as Faculty) |

## Forbidden flow (must never occur)

```
User → all ApplicationRoles across tenants → arbitrary permissions
```

## Discovery conclusion

The Prompt 8 `IgnoreQueryFilters()` approach is architecturally acceptable **iff** role IDs remain owned by `UserApplicationRoles` and same-tenant matched. Prompt 8A adds the explicit `role.TenantId == user.TenantId` guard and isolation tests.
