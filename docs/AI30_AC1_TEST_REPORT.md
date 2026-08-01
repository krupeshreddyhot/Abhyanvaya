# AI30 AC1 — Test Report

## Scope

Regression for Architecture Correction 1: Catalog Department SSOT; Scheduling consumers rewired; duplicate Scheduling Department module removed.

## Automated results

| Suite | Filter | Result |
|-------|--------|--------|
| `Abhyanvaya.Application.UnitTests` | `FullyQualifiedName~Scheduling` | **87 passed**, 0 failed |

Includes:

- Existing Phase 1 / 1A / 1B / 2 / 2A scheduling tests
- Updated `Phase1APermissionKeysTests` (retired Scheduling Department keys excluded from `All`)
- New `Ac1CatalogDepartmentSsotTests`

## Manual / UI regression checklist

| Case | Expected | Status |
|------|----------|--------|
| Catalog → Departments CRUD | Works at `/setup/departments` | Ready for QA |
| Scheduling Hub | No Departments tile; starts Academic Years… | Implemented |
| Subject Allocation department dropdown | Loads Catalog `GET /department?isActive=true` | Implemented |
| Faculty Preferences preferred department | Catalog only (no “(scheduling)” merge) | Implemented |
| Timetable Designer / Hub department filter | Catalog departments | Implemented |
| Schedule Versions department field | Catalog departments | Implemented |
| `GET /api/scheduling/departments` | 404 / absent | Implemented |
| Scheduling dashboard | No “Departments” ownership tile | Implemented |

## Target

**100% automated scheduling regression success** — achieved (87/87).

## Notes

- API project rebuild may fail if `Abhyanvaya.API` process locks output DLLs; Application layer build + unit tests succeeded independently.
- No EF migration required for AC1.
