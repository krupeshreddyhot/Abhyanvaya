# AI29.1D.24B.3A — Provisioning Hardening

## Changes

### 1. `AllocationPermissionCatalogReconciler`

- **Catalog only** — inserts missing Allocation.* `Permission` rows.  
- **No** `ApplicationRolePermission` writes.  
- **No** `IgnoreQueryFilters` admin role scan for grants.  
- Startup no longer broadens authorization.

### 2. EF seed (`StaffHubSeed`)

ADMIN allocation links reduced to operator ids:  
`227, 230, 231, 232, 233, 234`  
(+ SectionCapacity/Readiness `225–226`).  
Excluded from ADMIN seed: Approve, Ops.View, Reject, Export, Archive.

### 3. Deployment SQL

`scripts/Apply_AI29_1D_24B3A_AdminAllocationLeastPrivilege.sql`  
— ensure operator links; delete governance/ops over-grants on ADMIN.

### 4. Live repair

`scripts/ai29_1d_24b3a_apply_least_privilege.mjs` via tenant-rbac API.

## Separation

| Concern | Mechanism |
|---------|-----------|
| Permission catalog | Reconciler + seed Permission HasData |
| Role assignment | Seed + SQL + `/tenant-rbac` |

## Verified live

Admin JWT Allocation.* = Run + Scenario.View/Create/Compare/Replay/Review only.  
Simulate/Run = 200. Approve = 403. Faculty simulate/run = 403.
