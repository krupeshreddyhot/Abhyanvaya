# AI30 AC1.5 — Architecture Guard

**Status:** Implemented  
**Related:** ADR-021 Master Data Ownership · `AI30_MASTER_DATA_OWNERSHIP_MATRIX.md` · AI30 AC1  

## Objective

Protect the architecture from future **duplicate master entities** by failing automated tests when Scheduling (or any non-owner context) reintroduces CRUD for Catalog-owned masters.

This is an **architecture hardening** control — not a feature release.

## Source of truth

`docs/AI30_MASTER_DATA_OWNERSHIP_MATRIX.md`

Catalog-owned masters validated:

| Master |
|--------|
| Department |
| Course |
| Group |
| Semester |
| Subject |
| Staff |
| Language |
| Medium |
| Gender |
| Role |

## Components

| Artifact | Location | Role |
|----------|----------|------|
| `MasterOwnershipValidator` | `Abhyanvaya.Application.UnitTests/Architecture/MasterOwnershipValidator.cs` | Scans solution; applies ownership rules |
| `ArchitectureOwnershipReport` | `…/ArchitectureOwnershipReport.cs` | Structured + Markdown report |
| `ArchitectureOwnershipTests` | `…/ArchitectureOwnershipTests.cs` | xUnit entry points (fail on violations) |

**Framework:** existing xUnit project only — no new test framework.

## Rules enforced

1. Exactly one Catalog `DepartmentController` (and peer Catalog controllers for listed masters).
2. **No** Scheduling CRUD surfaces for Catalog masters:
   - Controllers / routes under `api/scheduling/{master}`
   - Parallel Scheduling DTOs / services / UI pages
3. Scheduling Department repository helper (if present) must remain **read-only**.
4. Retired `Scheduling.Department.*` keys must not appear in `PermissionKeys.All`.
5. Allowed: `DepartmentId` FKs, Catalog API consumption, AC1 redirect `/setup/scheduling/departments` → `/setup/departments`.

## How to run

```bash
dotnet test Abhyanvaya.Application.UnitTests/Abhyanvaya.Application.UnitTests.csproj --filter "FullyQualifiedName~ArchitectureOwnership"
```

On failure, the assertion message includes `ArchitectureOwnershipReport` Markdown with paths and severities.

## Failure behavior

If duplicate ownership is detected → **architecture validation fails** (test red). Merge should be blocked until ownership is restored.

## Out of scope (AC1.5)

- UI redesign  
- Scheduling feature changes  
- Database redesign  
- API redesign  
