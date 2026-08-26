# AI29.1D.24B.3A — Allocation Permission Matrix

**Existing roles only:** `ADMIN`, `FACULTY`  
**Values:** ALLOW | DENY | UNCHANGED

| Permission | ADMIN | FACULTY |
|------------|-------|---------|
| Allocation.Run | **ALLOW** | **DENY** |
| Allocation.Operations.View | **DENY** | **DENY** |
| Allocation.Scenario.View | **ALLOW** | **DENY** |
| Allocation.Scenario.Create | **ALLOW** | **DENY** |
| Allocation.Scenario.Compare | **ALLOW** | **DENY** |
| Allocation.Scenario.Replay | **ALLOW** | **DENY** |
| Allocation.Scenario.Review | **ALLOW** | **DENY** |
| Allocation.Approve | **DENY** | **DENY** |
| Allocation.Reject | **DENY** | **DENY** |
| Allocation.Scenario.Archive | **DENY** | **DENY** |
| Allocation.Export | **DENY** | **DENY** |

### Rationale

- Only existing tenant role that operates Allocation Workspace is **ADMIN**.  
- Operator set enables simulate/run/save/compare/replay/review without inheriting governance or technical ops.  
- Approve/Reject/Archive/Export/Operations.View require **explicit** RBAC grants (not startup, not implied by Run).  
- SuperAdmin continues to use existing policy bypass (`AddSetupManagePolicy`).

### Allocation operator

**Existing role:** `ADMIN` (`ApplicationRole.Code = ADMIN`)
