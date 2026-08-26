# AI29.1D.24B.2 Prompt 9.1 — Security & Validation Discovery

**Date:** 2026-08-11  
**Mode:** Discovery only — no production code changes in this prompt step.  
**Evidence:** `prompt9-discovery.json`, `prompt9-timetable-probe.json`

## 1. Current JWT role-resolution flow

`JwtService.GenerateTokenAsync` → `ResolvePermissionKeysAsync(user)`:

1. **SuperAdmin** → full `Permissions` catalog keys.
2. Else resolve role IDs:
   - Start from `UserApplicationRoles` for `user.Id` (ownership authoritative).
   - Join `ApplicationRoles` with **`IgnoreQueryFilters()`** so ambient login `TenantId` (often 0) cannot hide tenant roles.
   - Require `!role.IsDeleted` and **`role.TenantId == user.TenantId`**.
3. Load permission keys from `ApplicationRolePermissions` **only** for those role IDs.
4. Fallback if no assigned same-tenant role permissions:
   - Admin → `PermissionKeys.All`
   - Faculty → `PermissionKeys.LegacyFacultySet`
   - else empty

Claims emitted include `UserId`, `Role`, `TenantId`, `StaffId`, and one `permission` claim per key.

## 2. Tenant-filter behavior during authentication

- Global EF tenant filters on `ApplicationRole` apply when ambient `ICurrentUserService.TenantId` is set.
- At login, ambient tenant is frequently unset/0, which previously zeroed role joins and forced LegacyFacultySet (Prompt 8 defect).
- Prompt 8/8A fix uses `IgnoreQueryFilters()` **only** on the ApplicationRole join for authentication-time resolution, then re-applies tenant ownership via `role.TenantId == user.TenantId` and UserApplicationRoles.

## 3. Existing security boundaries

| Boundary | Authority |
|----------|-----------|
| User ↔ role membership | `UserApplicationRoles` |
| Role ↔ tenant | `ApplicationRole.TenantId` must match authenticated `user.TenantId` |
| Role ↔ permissions | `ApplicationRolePermissions` for assigned same-tenant role IDs only |
| API authorization | JWT `permission` claims + server policies (no React-side authz) |
| Attendance write scope | Existing Attendance APIs / AI29.1D.15A save-scope integrity |
| Section list | Requires `Section.View` among others |

`IgnoreQueryFilters()` is **not** a general tenant-isolation bypass: cross-tenant role links do not yield foreign permission keys (covered by Prompt 8A Tests B/C/D).

## 4. Existing Prompt 8 defect and fix

**Defect:** Ambient tenant filtering hid assigned `ApplicationRole` rows during login → silent LegacyFacultySet → missing `Section.View` → `GET /api/sections` 403.

**Fix (JwtService):** IgnoreQueryFilters on ApplicationRole join + same-tenant constraint + permissions loaded by role ID list.

**Hardening (Prompt 8A):** Explicit cross-tenant isolation tests; source guard for `role.TenantId == user.TenantId`.

## 5. Existing validation data (Prompt 6/7/8)

| Item | Notes |
|------|--------|
| Academic Year 1 | Pre-existing |
| B.Com / CA / Finance / Sem III | Pre-existing |
| Semester IV (id 9), Section `CA-IV-A` | Prompt 7 created |
| Sections `CA-A`, `CA-B`, `FIN-A` | Prompt 7 created |
| Section `SCCA01` | Pre-existing |
| 5 students → Semester IV | Prompt 7 modified; **no before/after map persisted** |
| Faculty-A `teststaff1` (user 6 / staff 7) | Prompt 8 prepared |
| FACULTY role 101 + permission 210 (`Section.View`) | Prompt 8 RBAC |
| Staff 7 teaching subjects | Prompt 8 |
| StudentSections 20+20 on CA-A/CA-B | Prompt 8 |

## 6. Original vs temporary (where determinable)

| Item | Verdict |
|------|---------|
| `SCCA01`, core catalog (AY/Course/Groups/Sem III) | Original / legitimate (A) |
| `CA-A`/`CA-B`/`FIN-A`/`CA-IV-A`, Sem IV node | Temporary validation (B) — created in Prompt 7 |
| Sem IV student moves | Modified existing (C) — originals unknown |
| FACULTY `Section.View` on role 101 | Legitimate RBAC (A) post-fix |
| CA-A/CA-B memberships | Temporary validation (B) |
| `teststaff1` password / staff link | Modified (C) — persona retained |

## 7. Available Faculty personas

| Persona | Username | StaffId | Notes |
|---------|----------|---------|--------|
| Faculty-A (no TT) | `teststaff1` | 7 | Login OK; Section.View + Attendance.View/Manage |
| Faculty-B (expected TT) | `knraj` | 4 | Login OK; resolver `hasTimetable=false` |
| Admin | `admin` | — | College admin |
| SuperAdmin | `superadmin` | — | Platform |

## 8. Faculty with no timetable

**Faculty-A `teststaff1`:** `AttendanceSessionResolver` → `mode=Legacy`, `hasTimetable=false`  
Message: Faculty has no published/locked timetable; use legacy attendance workflow.

## 9. Faculty with published timetable

**None confirmed.** Faculty-B probes across a 14-day window all return `hasTimetable=false`.

## 10. Available SectionGroups

`GET /api/section-groups` → **count = 0**. Combined candidates = 0.

## 11. Available TimetableSections / timetables

One timetable record exists:

| Id | Name | Status | Published/Locked? |
|----|------|--------|-------------------|
| 4 | Timetable for Commerce Sem 3 | **Draft (1)** | **No** |

No Locked (2) or Published (3) timetable in the validation tenant.

## 12. Combined A+B timetable scenario

**Unavailable.** Requires existing SectionGroup with ≥2 sections **and** published/locked timetable participation via AttendanceSessionResolver. Neither exists.

### Discovery verdict for acceptance paths

| Path | Availability |
|------|----------------|
| Manual no-timetable attendance | Available (Faculty-A) — already live-proven Prompt 8 |
| Optional Section manual | Available (CA-A membership) — already live-proven Prompt 8 |
| Timetable-driven attendance | **DATA UNAVAILABLE** (Draft-only timetable; resolver no TT) |
| Combined A+B | **DATA UNAVAILABLE** (no SectionGroup) |
