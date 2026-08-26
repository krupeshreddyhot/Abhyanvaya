# AI29.1D.24B.3A — Authorization Discovery

**Date:** 2026-08-15  
**Mode:** Discovery findings (Prompt 1) — production changes applied in later prompts as authorized.

## Canonical permission catalog

`Permission` table / `PermissionKeys` / EF `StaffHubSeed` HasData (ids 1–55, 210–237).  
Allocation keys: `Allocation.Run` (227) … `Allocation.Scenario.Archive` (237).

## Canonical role-permission assignment

1. **EF seed** (`ApplicationDbContext.StaffHubSeed`) for new databases  
2. **Deployment SQL** scripts (idempotent)  
3. **Tenant RBAC API** `PUT /api/tenant-rbac/roles/{id}/permissions` (college Admin)  
4. **NOT** JWT / React / runtime blanket grants

## Roles

| Code | Scope | Purpose |
|------|--------|---------|
| `ADMIN` | Tenant-scoped `ApplicationRole` | College administrator (only existing allocation operator persona) |
| `FACULTY` | Tenant-scoped | Teaching staff — no Allocation.* in seed |
| SuperAdmin | Enum `UserRole.SuperAdmin` | Cross-tenant; policy bypass for setup manage policies |

No separate “Allocation Operator”, “Approver”, or “Operations” application role exists.

## Answers (Prompt 1 questions)

1. Catalog = `Permission` / `PermissionKeys`  
2. Assignment = seed + SQL + tenant-rbac API  
3–4. ApplicationRoles are **tenant-scoped** (`TenantId`)  
5–6. **ADMIN** is the intended allocation executor (workspace under Setup/Sections)  
7–8. Approve/Reject are **separate** permissions; not implied by Run  
9–11. No dedicated operator/ops/approver roles — only ADMIN/FACULTY  
12. Production: migrations/seed/SQL/RBAC UI  
13. Yes — `TenantRbacController`  
14. Startup reconciliation for **catalog** is acceptable; **role grants at startup were Prompt 3 exception** and are removed in 3A  

## Prompt 3 reconciler (before 3A)

Granted **all** Allocation.* to every `ADMIN` via `IgnoreQueryFilters` role scan — broader than required.

## Risks

- Removing Approve from ADMIN leaves no tenant ApplicationRole with Approve (SuperAdmin policy bypass remains).  
- Colleges needing Approve on Admin must grant explicitly via RBAC UI/SQL.  
