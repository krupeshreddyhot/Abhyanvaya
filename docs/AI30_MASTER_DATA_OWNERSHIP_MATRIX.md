# AI30 — Master Data Ownership Matrix

**Status:** Architecture Documentation Library (ADL) candidate · enforced by AI30 AC1.5 Architecture Guard  
**Related:** AI30 AC1 Single Source of Truth · **ADR-021 Master Data Ownership** · `AI30_AC15_ARCHITECTURE_GUARD.md`  

## Ownership rules

| Owner | Responsibility |
|-------|----------------|
| **Catalog** | Institution academic / HR master data; CRUD under Setup / Catalog |
| **Scheduling** | Timetable foundation & operational schedule data; may **reference** Catalog IDs |

## Catalog-owned masters

| Entity | Notes |
|--------|-------|
| Department | SSOT confirmed by AC1 |
| Course | Catalog |
| Group | Catalog |
| Semester | Catalog |
| Subject | Catalog / subject catalog |
| Staff | Catalog |
| Language | Catalog lookup |
| Medium | Catalog lookup |
| Gender | Catalog lookup |
| Role | RBAC / department & college role lookups |

## Scheduling-owned masters

| Entity | Notes |
|--------|-------|
| AcademicYear | Scheduling calendar |
| AcademicTerm | If present / planned |
| WorkingDay | Per academic year |
| Holiday | Holiday calendar entries |
| HolidayType / HolidayTypeCatalog | Scheduling catalog |
| Campus | Facilities |
| Building | Facilities |
| Floor | Facilities |
| Room | Facilities (may FK DepartmentId → Catalog) |
| RoomFeature | Scheduling |
| RoomAvailability | Scheduling |
| FacultyAvailability | Scheduling |
| FacultyPreference / FacultyTeachingPreference | Scheduling (FK Department optional) |
| SubjectAllocation | Scheduling (required DepartmentId → Catalog) |
| TimeSlotSet / TimeSlot | Scheduling |
| TimeSlotTemplate | Scheduling |
| Timetable / TimetableEntry | Scheduling (FK DepartmentId → Catalog) |
| ScheduleVersion | Governance |

## Duplicate ownership findings (AC1 audit)

| Finding | Severity | Resolution |
|---------|----------|------------|
| Scheduling Department CRUD + Catalog Department CRUD | **Critical** | **Fixed in AC1** — Scheduling surface removed |
| Parallel Scheduling Department permissions | Medium | Retired from `PermissionKeys.All`; seed IDs 20–21 legacy only |
| Faculty Preferences merging Catalog + “scheduling” dept labels | Medium | **Fixed** — Catalog only |

## No other duplicate masters detected (current solution)

Course, Group, Semester, Subject, Staff remain Catalog-only. Scheduling pages load them via Catalog / master APIs and store FKs.

## Enforcement (AC1.5)

| Control | Location |
|---------|----------|
| Architecture Guard tests | `Abhyanvaya.Application.UnitTests/Architecture/ArchitectureOwnershipTests.cs` |
| Validator | `MasterOwnershipValidator` |
| ADR | ADR-021 in `00_Architecture_Decision_Records.md` |

## Recommendation

Re-run this matrix after each AI30 phase that introduces master-like entities. Any new entity must declare an owner before UI CRUD is added. Keep Architecture Ownership tests green.
