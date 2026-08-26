# AI29.1D.15A Prompt 7 — Faculty Allocation Authorization Hardening

## Threat

Client manipulates `POST /api/faculty-sections` with an arbitrary `facultyId` or mismatched academic year.

## Server validation (assign)

`FacultySectionAssignmentAuthorization.ValidateAssignAsync` then `SectionManagementService.AssignFacultyAsync`:

| Check | Reject when |
|-------|-------------|
| Tenant | Staff/section not in current tenant |
| Authorized Faculty | Missing, other-tenant, or soft-deleted staff |
| Inactive faculty | Employment status inactive / INACTIVE|TERMINATED|… or inactive staff type |
| Academic Year | Request AY ≠ section AY, or AY not in tenant |
| Course / Group / Semester | Section’s C/G/S invalid for tenant (group must belong to course) |
| Section | Missing / deleted |

**Never** silently substitute another faculty. Failed validation keeps the requested `FacultyId` in the result for audit; persist uses only validated ids.

## Compatibility

- Same `AssignFacultySectionRequest` / `FacultySectionDto` / `POST /api/faculty-sections`
- Still `404` for missing section (`KeyNotFoundException`)
- Still `400` for authorization/scope failures (`InvalidOperationException`)

## Files

- `Abhyanvaya.Application/Academic/FacultySectionAssignmentAuthorization.cs`
- `Abhyanvaya.Application/Academic/SectionManagementService.cs`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt7_FacultySectionAssignmentAuthorizationTests.cs`
- `docs/AI29_1D_15A_PROMPT_7_FACULTY_ASSIGNMENT_AUTHORIZATION.md`
