# AI30 Phase 1.9 — Architecture Hardening

| Field | Value |
|-------|-------|
| **Document ID** | AI30-Phase1.9-Architecture-Hardening |
| **Status** | Complete |
| **Date** | August 2026 |
| **Scope** | Scheduling foundation review — no functional redesign |

---

## Verification checklist

| Concern | Status | Evidence |
|---------|--------|----------|
| Repository pattern | Pass | `I*Repository` in Application; impl in Infrastructure/Persistence/Repositories/Scheduling |
| CQRS (service-method) | Pass | Explicit List/Get (queries) vs Create/Update/Delete/Clone (commands); **no MediatR** per ADL Naming §11 |
| DTOs | Pass | `Application/DTOs/Scheduling/*` |
| Validation | Pass | FluentValidation validators for Academic Year/Holiday + Subject Allocation; domain checks in services |
| Soft delete | Pass | `IsDeleted = true` on deletes; EF global filter on `BaseEntity` |
| Tenant filter | Pass | `BaseEntity.TenantId` + `SetTenantFilter` |
| Audit fields | Pass | `CreatedDate`/`CreatedBy`/`UpdatedDate`/`UpdatedBy` stamped in `SaveChangesAsync` |
| Permission checks | Pass | `Scheduling.View` / `Scheduling.Manage` + `CanViewScheduling` / `CanManageScheduling` |
| Logging | Pass | Existing API/host logging; services throw `DomainException` for business faults |
| Error handling | Pass | Controllers map `DomainException` → 400, `KeyNotFoundException` → 404 |
| Caching ready | Pass | Read methods are `AsNoTracking`-friendly via repositories; no cache coupling |
| Swagger | Pass | Standard controller discovery under `api/scheduling/*` |
| Testing | Pass | `Application.UnitTests/Scheduling` |
| Folder structure | Pass | Domain → Application → Infrastructure → API layering |
| No business logic in controllers | Pass | Thin controllers delegate to services |

## Architecture decisions (Phase 1)

1. **CQRS without MediatR** — Matches ADL; feature folders use service command/query methods.
2. **FluentValidation introduced for Scheduling** — First catalog area to adopt FV; other modules remain inline until migrated.
3. **No timetable / conflict / attendance / AI** — Foundation master data only.
4. **Extension points** — Subject allocation flags (`AiAttendanceEnabled`, `AttendanceMandatory`) reserved for later phases.

## Refactors applied during hardening

- Dashboard aggregates expanded (buildings, subjects, faculty, weekly hours, room capacity) without changing API route.
- Controllers remain free of EF access.
