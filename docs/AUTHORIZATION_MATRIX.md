# Authorization matrix (API)

This document maps **ASP.NET authorization policies** to **roles**, **JWT `permission` claims**, and **extra data rules** implemented in controllers. It is derived from `Program.cs` and controller attributes as of the last update.

**Spreadsheet exports (CSV):** see `docs/csv/` — `authorization_policies.csv`, `permission_keys.csv`, `jwt_permission_resolution.csv`, `data_layer_rules.csv`, and `authorization_endpoints.csv` (one row per route where listed).

## How policies combine

- **Class + method** `[Authorize]`: the user must satisfy **all** applicable policy requirements (logical **AND**).
- **`TenantScopedUser`**: `HasTenantHandler` — **SuperAdmin** always passes; others need **`TenantId` claim greater than zero**.

---

## 1. Policy definitions (`Program.cs`)

| Policy | Who passes (summary) | JWT `permission` claim? |
|--------|----------------------|-------------------------|
| `AuthenticatedUser` | Any authenticated user | No |
| `TenantScopedUser` | SuperAdmin **or** `TenantId` &gt; 0 | No |
| `AdminOnly` | SuperAdmin **or** `Admin` | No |
| `AdminOrFaculty` | Role `Admin` or `Faculty` (no tenant check in policy) | No |
| `TenantScopedAdmin` | SuperAdmin **or** (`Admin` and tenant-scoped) | No |
| `TenantCollegeAdminOnly` | `Admin` and tenant-scoped (excludes SuperAdmin) | No |
| `CanViewStudents` | SuperAdmin **or** (`Admin` or `Faculty`) with tenant | **No** (role + tenant only) |
| `CanManageStudents` | SuperAdmin **or** tenant-scoped `Admin` | **No** |
| `CanManageAttendance` | SuperAdmin **or** (`Admin` or `Faculty`) with tenant | **No** |
| `CanViewReports` | SuperAdmin **or** (`Admin` or `Faculty`) with tenant | **No** |
| `SuperAdminOnly` | Role `SuperAdmin` | No |
| `UniversityListAccess` | SuperAdmin, **or** tenant `Admin`, **or** (tenant user with claim `Organization.Manage`) | **Yes** for org branch |
| `DashboardOverviewAccess` | SuperAdmin, **or** tenant-scoped `Admin`/`Faculty` | No |
| `CanManageCourses` | SuperAdmin, **or** (tenant + claim `Setup.Courses.Manage`) | **Yes** |
| `CanManageGroups` | SuperAdmin, **or** (tenant + claim `Setup.Groups.Manage`) | **Yes** |
| `CanManageSemesters` | SuperAdmin, **or** (tenant + claim `Setup.Semesters.Manage`) | **Yes** |
| `CanManageOrganization` | SuperAdmin, **or** (tenant + claim `Organization.Manage`) | **Yes** |

**JWT permission resolution** (`JwtService.ResolvePermissionKeysAsync`):

- If the user has **any** `UserApplicationRole` rows → JWT `permission` claims come **only** from those roles (no merge with `PermissionKeys.All` / `LegacyFacultySet`).
- If **none** → **Admin** gets `PermissionKeys.All`; **Faculty** gets `PermissionKeys.LegacyFacultySet`; **SuperAdmin** gets every permission row in the database.

---

## 2. Permission keys catalog (`PermissionKeys`)

Stable strings include: `Students.View`, `Students.Manage`, `Attendance.View`, `Attendance.Manage`, `Reports.View`, `Dashboard.View`, `Master.View`, `Setup.Subjects.Manage`, `Setup.Departments.Manage`, `Setup.Staff.Manage`, `Setup.Lookups.Manage`, `Setup.Courses.Manage`, `Setup.Groups.Manage`, `Setup.Semesters.Manage`, `Organization.Manage`.

**Note:** Many API policies above **do not** assert these strings; they assert **role + tenant** instead. The keys still matter for **setup-style policies** and for **front-end** feature toggles. Keeping JWT and API behavior aligned is a maintenance concern.

---

## 3. Data-layer rules (not policies)

These apply **after** the user passes policy checks:

| Area | Rule |
|------|------|
| **Faculty + linked staff (`StaffId` positive)** | `FacultySubjectAccess` / `StaffSubjectAssignment` — subject-scoped operations only for assigned subjects (e.g. attendance, reports, master subjects, faculty students). |
| **Faculty, legacy (no staff link)** | Many endpoints further restrict by JWT **CourseId** / **GroupId** (cohort). |
| **Reports** (`/api/reports/student`, `/api/reports/monthly`) | Legacy Faculty: **403** if student not in same course/group. Staff-linked Faculty: attendance rows filtered to **assigned subject IDs** + tenant. |
| **Dashboard overview** (staff-linked Faculty) | Metrics scoped to assigned subjects / related students. |

---

## 4. Endpoints by controller

Legend: **Policy** = effective gate (class ∩ method). **Data** = extra rules in code.

### `api/Auth`

| Route | Method | Policy / access | Notes |
|-------|--------|-----------------|-------|
| `super-admin-login` | POST | Anonymous | |
| `login` | POST | Anonymous | |
| `forgot-password` | POST | Anonymous | |
| `reset-password` | POST | Anonymous | |
| `change-password` | POST | `[Authorize]` — authenticated | |
| `universities` | GET | `[Authorize]` — authenticated | |

### `api/Admin`

| Route | Method | Policy | Notes |
|-------|--------|--------|-------|
| *(controller)* | | `[Authorize]` | |
| `universities` | GET | `UniversityListAccess` | |
| `universities` | POST | `SuperAdminOnly` | |
| `parent-college-options` | GET | `CanManageOrganization` | Claim `Organization.Manage` for tenant admins |
| `tenant-college` | GET | `CanManageOrganization` | |
| `tenant-college/logo` | POST | `CanManageOrganization` | |
| `tenant-college/logo-health` | GET | `CanManageOrganization` | |
| `tenant-college` | PUT | `CanManageOrganization` | |

### `api/Organization`

| Route | Method | Policy |
|-------|--------|--------|
| `parent-college-options` | GET | `SuperAdminOnly` |
| `colleges` | POST | `SuperAdminOnly` |

### `api/Dashboard`

| Route | Method | Policy | Data |
|-------|--------|--------|------|
| `overview` | GET | `DashboardOverviewAccess` | Staff-linked Faculty: assigned subjects |
| `student` | GET | `TenantScopedUser` | Student portal (`UserId`) |
| `low-attendance` | GET | `TenantScopedUser` | |
| `monthly-trend` | GET | `TenantScopedUser` | |
| `class` | GET | `TenantScopedUser` | Faculty + staff: subject access check |
| `subject-performance` | GET | `TenantScopedUser` | Same |

### `api/Master`

| Route | Method | Policy | Data |
|-------|--------|--------|------|
| `courses`, `semesters`, `semesters/full`, `genders`, `mediums`, `languages`, `subjects`, `groups`, `faculty-subjects`, `my-subjects`, `faculty-students` | GET | `TenantScopedUser` | Staff Faculty: subjects/students per assignments |

### `api/Student`

| Route | Method | Policy | Notes |
|-------|--------|--------|-------|
| list / get pattern | GET | `CanViewStudents` | Role gate; not `Students.View` claim |
| create / update | POST, PUT | `CanManageStudents` | Admin + tenant (Faculty cannot via policy) |
| `export` | GET | `CanManageStudents` | |
| `upload` | POST | `CanManageStudents` | |

### `api/Attendance`

| Route | Method | Policy | Data |
|-------|--------|--------|------|
| `mark`, `GET`, `students-for-marking`, `lock`, `edit` | various | `CanManageAttendance` | Staff Faculty: subject + cohort rules |

### `api/Reports`

| Route | Method | Policy | Data |
|-------|--------|--------|------|
| `student`, `subject`, `monthly` | GET | `CanViewReports` | Tenant + Faculty scope (see §3) |

### `api/Faculty`

| Route | Method | Policy | Data |
|-------|--------|--------|------|
| `students` | GET | `AdminOrFaculty` | Tenant + `FacultyMayAccessSubjectAsync` |

### `api/Subject`

Class: `TenantScopedUser`. Actions that add `AdminOnly` require **both**.

| Route | Method | Policy |
|-------|--------|--------|
| `catalog`, `tenant-lookup` GET/POST, `POST` (create subject), `PUT` | | `TenantScopedUser` **and** `AdminOnly` |
| `my-subjects`, `electives`, `select` | | `TenantScopedUser` only |

### `api/Course`, `api/Group`, `api/Semester`

| Policy |
|--------|
| `CanManageCourses` / `CanManageGroups` / `CanManageSemesters` (permission + tenant; SuperAdmin bypass) |

### `api/Staff`

Class: `TenantScopedUser`. All listed actions also use **`TenantScopedAdmin`** (college Admin / SuperAdmin).

### `api/Department`

Class: `TenantScopedUser`. CRUD uses **`TenantScopedAdmin`**.

### `api/StaffHubLookups`

Class: `AdminOnly`.

### `api/ElectiveGroup`, `api/Gender`, `api/Language`, `api/Medium`

Class: `AdminOnly`.

### `api/TenantRbac` (`api/tenant-rbac`)

Class: `TenantCollegeAdminOnly`.

### `api/TenantUsers` (`api/tenant-users`)

Class: `TenantCollegeAdminOnly`.

### `api/Ui`

Class: `TenantScopedUser` (`me`, `header`).

---

## 5. Maintenance tips

1. When adding an endpoint, set **policy** on the action (or document why it inherits class-level auth only).
2. If you introduce a **new permission key**, wire it in **`Program.cs`** if the API should enforce it; otherwise the UI may claim-gate while the API stays role-gated.
3. **Application roles** replace the default JWT permission sets entirely when any assignment exists — document that for tenant admins when they start using RBAC UI.

---

*Generated as a reference; update when policies or controllers change.*
