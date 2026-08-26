# AI29.1D.24 Prompt 4B.5 — Tenant and Authorization Hardening

## Scope

Prove cross-tenant Course → Program assignment is rejected with no side effects, and that assignment authorization reuses existing policies/permissions only.

**No** new permission names. **No** schema changes. **No** second assignment endpoint.

## Cross-tenant scenario

| Actor | Resource |
|-------|----------|
| Tenant A | Course A (`TenantId = A`) |
| Tenant B | Program B (`TenantId = B`, Active) |

**Attempt:** Tenant A user assigns Course A → Program B.

### Application behavior (verified)

- Program lookup is fail-closed: `Programs.Where(p => p.Id == next && p.TenantId == currentUser.TenantId)`
- Foreign Program is invisible ⇒ rules return `"Invalid Program."`
- Throws `FluentValidation.ValidationException`
- **No** `Course.ProgramId` mutation
- **No** domain events (`CourseAssigned` / `CourseRemoved`)
- **No** hierarchy / statistics cache invalidation
- Course Master Update TX rolls back Code/Name if assign fails after staging

### API convention (verified in controllers)

| Failure | Status | Mapping |
|---------|--------|---------|
| Cross-tenant / invalid Program | **400** | `catch (ValidationException) → BadRequest` on `CourseController` and `ProgramsController` |
| Missing `CanAssignCourseToProgram` | **403** | `Forbid()` from `EnsureProgramAssignAuthorizedAsync` **before** write service |

## Authorization — existing policy only

Policy: `AuthorizationPolicies.CanAssignCourseToProgram` (wired in `Program.cs`).

| Principal | Result |
|-----------|--------|
| `Program.Manage` + valid `TenantId` | Allow |
| `Setup.Courses.Manage` + valid `TenantId` | Allow |
| Both | Allow |
| SuperAdmin | Allow |
| Faculty with only Attendance.* | **Deny** |
| Admin with only `Program.View` | **Deny** |
| Unauthenticated | **Deny** |
| Permission claim but missing/invalid `TenantId` | **Deny** |

Permission strings (unchanged):

- `PermissionKeys.ProgramManage` = `Program.Manage`
- `PermissionKeys.SetupCoursesManage` = `Setup.Courses.Manage`

### Inactive faculty / admin

`CanAssignCourseToProgram` does **not** inspect Staff/User `IsActive` flags. Access is claim-based. An inactive account that still presents JWT permission claims would be allowed by this policy alone; account lifecycle is outside this prompt. Users without the manage claims are denied (covers typical inactive-permission / stripped-role cases).

## Tests

`AI29_1D_24_Prompt4B5_TenantAuthorizationTests.cs`

- Cross-tenant Assign + Course Master Update (actual Tenant B Program entity)
- Policy allow/deny matrix for existing permissions
- Source guards: no new permission names; Forbid-before-write; ValidationException → 400

## Files

- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_24_Prompt4B5_TenantAuthorizationTests.cs`
- `docs/AI29_1D_24_PROMPT_4B5_TENANT_AUTHORIZATION.md`
- Related: `AcademicCatalogService.AssignCourseToProgramAsync`, `CourseController`, `Program.cs` policy registration
