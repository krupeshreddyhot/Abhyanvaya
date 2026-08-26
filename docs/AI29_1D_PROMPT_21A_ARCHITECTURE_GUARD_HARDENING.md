# AI29.1D Prompt 21A — Architecture Guard Hardening

Hardens Prompt 21 into an enterprise architecture compliance gate **without** changing business functionality or introducing a second architecture framework.

## CI status (machine-readable)

| Status | When |
|--------|------|
| `FULLY_VERIFIED` | UI source available **and** all checks pass |
| `PARTIALLY_VERIFIED` | UI source unavailable **and** backend/assembly checks pass |
| `FAILED` | Any architectural violation |

**Do not report PARTIALLY_VERIFIED as FULLY_VERIFIED.**

Backward compatibility:

- `Passed` remains `true` for both `FULLY_VERIFIED` and `PARTIALLY_VERIFIED`
- `Passed` is `false` only for `FAILED`
- `FullyVerified` is `true` **only** for `FULLY_VERIFIED`

## Report fields (Prompt 21A)

| Field | Meaning |
|-------|---------|
| `Status` | `FULLY_VERIFIED` \| `PARTIALLY_VERIFIED` \| `FAILED` |
| `FullyVerified` | Explicit full verification flag |
| `UiScanExecuted` | Whether UI source was scanned |
| `BackendChecksPassed` | Authoritative backend types present / healthy |
| `PlatformBoundaryPassed` | Platform allocation boundary guard |
| `ViolationCount` | Number of violations |

Existing Prompt 21 fields (`Passed`, `Checks`, `Violations`, `UiScan`, `BackendAuthority`) are preserved.

## Hardened checks

1. **Application → UI** — inspect fields, properties, methods, parameters, return types, base classes, interfaces, generic arguments, attributes.
2. **Domain → Application/API/UI** — assembly references **and** Domain `.csproj` `ProjectReference` / `Reference` / `PackageReference` includes.
3. **UI static scan** — retained forbidden data-access + authority patterns (HTTP/API calls remain allowed).
4. **package.json** — forbidden direct DB/ORM dependencies.

## Snapshot

`docs/architecture/AI29_1D_architecture_compliance.json`

## API

`GET /api/v1/academic-structure/architecture/ai29-1d-report`  
Consumers should gate CI on `Status` (not only `Passed`).

## Quality gate

```bash
dotnet test --filter "FullyQualifiedName~AI29_1D_Prompt21"
dotnet build Abhyanvaya.API/Abhyanvaya.API.csproj
# UI
cd abhyanvaya-ui && npm run build
```

Filter also matches Prompt 21A (`AI29_1D_Prompt21A_*`).

## Out of scope (unchanged)

AttendanceSessionResolver, attendance save scope, Subject Master, Section / SectionGroup, scheduling engine, Allocation Engine / scoring / governance, capacity engine, Program/Course/Group/Semester hierarchy.
