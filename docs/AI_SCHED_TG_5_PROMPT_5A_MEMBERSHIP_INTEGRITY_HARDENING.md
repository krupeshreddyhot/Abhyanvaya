# AI-SCHED-TG.5 Prompt 5A — Membership Integrity Hardening & Final Application Gate

**Workstream:** AI-SCHED-TG.5  
**Prompt:** 5A — Membership Integrity Hardening  
**Date:** 2026-08-19  
**Type:** Hardening + final application gate (no UI)  
**Status:** CONDITIONAL PASS

---

## 1. Objective

Close Prompt 5 review conditions so the Teaching Group membership application boundary is stable for a subsequent UI prompt:

- Deterministic Model B resolution  
- Resolved-membership ExclusionGroupKey enforcement  
- Capacity / lifecycle / tenant / idempotency hardening  
- PostgreSQL uniqueness + concurrency evidence  
- DTO contract cleanup  
- Architecture + RBAC guards  

---

## 2. Existing architecture relied upon

Frozen ownership (unchanged):

```text
TeachingGroup
  ├── TeachingGroupMembership (explicit Include/Exclude overlays)
  ├── TeachingGroupSection (section SoT)
  └── Membership Resolver → Resolved roster (read-only)

TeachingGroupSection → TimetableSectionProjector → TimetableSection → Attendance
```

No Attendance / StudentSection / StudentSubject / TimetableSection writes from membership services. No TG auto-create. No SA→TG inference.

---

## 3. Membership resolution contract (Model B)

```text
Resolved = (Base ∪ ExplicitIncludes) − ExplicitExcludes
```

| Source | Base | Overlays |
|---|---|---|
| ExplicitStudents | empty | Includes only |
| Section / CombinedSections | TeachingGroupSection → StudentSection | ignored |
| StudentSubject | StudentSubject ∩ academic scope | ignored |
| Hybrid | Sections if linked, else StudentSubject | Include + Exclude |

Exclude always wins. Distinct StudentId. Ordered by StudentId ascending. Resolver never persists.

---

## 4. ExclusionGroupKey semantics (Prompt 5A algorithm)

**Rule:** Two non-Archived Teaching Groups sharing `(Tenant, SubjectAllocationId, ExclusionGroupKey)` must not share any StudentId in their **resolved** rosters.

**Algorithm (`EnsureExclusionAgainstResolvedPeersAsync`):**

1. Skip if ExclusionGroupKey empty or proposed resolved set empty.  
2. Load peers: same SubjectAllocationId + ExclusionGroupKey, Status ≠ Archived, Id ≠ self (tenant via ambient filters).  
3. For each peer, call `ITeachingGroupMembershipResolver.ResolveAsync` once (read-only; resolver does **not** call exclusion → no recursion).  
4. Run `TeachingGroupRules.EnsureStudentNotInMutuallyExclusiveGroup` with peer **resolved** StudentIds.  
5. On conflict → `ConcurrencyConflictException` → HTTP **409**.  
6. Never mutate peer Teaching Groups / never auto-exclude / never reassign.

Archived peers ignored. Cross-tenant peers invisible under query filters.

---

## 5. Capacity semantics

| Field | Role |
|---|---|
| ResolvedStudentCount | Derived from resolver / proposed set |
| ExpectedStudentCount | Planning intent (0 allowed) |
| MaxTeachingCapacity | Optional ceiling (null = unlimited; ≤0 invalid) |
| Room.Capacity | Not checked here |

Mutation validates proposed resolved count ≤ Max when configured.

---

## 6. Tenant isolation

- Ambient EF query filters; no `IgnoreQueryFilters`  
- Student.TenantId must equal TeachingGroup.TenantId  
- Academic scope: Course / Group / Semester (+ Section AcademicYear for derived)  

---

## 7. Lifecycle rules

Uses existing `TeachingGroup.EnsureCanMutate()`:

| Status | Membership mutation |
|---|---|
| Draft | Allowed |
| Active | Allowed |
| Locked | Rejected |
| Archived | Rejected |

No new lifecycle states. Domain has no separate Published/Frozen enum.

---

## 8. Idempotency

- Duplicate Add Include → no duplicate current row  
- Remove missing → success / no destructive side effects  
- Replace same payload → stable current overlay set  

---

## 9. PostgreSQL uniqueness

Migration `20260817153000_AI_SCHED_TG_3_TeachingGroup` creates filtered unique index:

```text
IX_SchedulingTeachingGroupMembership_TenantId_TeachingGroupId_StudentId
UNIQUE (TenantId, TeachingGroupId, StudentId)
WHERE "IsCurrent" = TRUE AND "IsDeleted" = FALSE
```

Verified by integration test querying `pg_indexes`.

---

## 10. Concurrency test evidence

Suite: `Abhyanvaya.IntegrationTests.Scheduling.TeachingGroupMembershipConcurrencyIntegrationTests`  
Environment: `PostgreSqlFixture` (Testcontainers / `ABHYANVAYA_TEST_CONNECTION` / local Postgres)

| Test | Expectation |
|---|---|
| Filtered unique index exists | UNIQUE + IsCurrent/IsDeleted filter |
| Concurrent duplicate insert | One succeeds; other → conflict message (no SQL leak); one current row remains |
| Application Add idempotent | One current membership |

`DbUpdateException` → `ConcurrencyConflictException` → controller `Conflict` (409).

---

## 11. DTO decision

**Removed `ResolvedTeachingGroupMemberDto.IsExcludedFromBase`.**

Evidence:

- Field was always `false` for active resolved members  
- No UI / TypeScript consumer referenced it  
- Exclude semantics remain on raw `GET .../memberships` via Inclusion  

Active resolved DTO retains: `StudentId`, `Provenance` (`Derived` | `ExplicitInclude`).

---

## 12. RBAC evidence

| Operation | Policy |
|---|---|
| GET memberships / resolved-members | Class `CanViewSchedulingTeachingGroup` |
| POST/PUT/DELETE memberships | Method `CanManageSchedulingTeachingGroup` |

Guards: source inspection of controller + `Program.cs` policy registration.  
**Not executed:** full `WebApplicationFactory` host auth matrix (not present in repo) → CONDITIONAL for live-host RBAC.

---

## 13. Architecture Guard evidence

`AiSchedTg5Prompt5AArchitectureGuardTests`:

- Resolver: no SaveChanges/AddAsync/forbidden entity creates; uses `IApplicationDbContext` only  
- Service: resolved-peer exclusion + no Attendance/StudentSection/TimetableSection writes  
- Controller: thin; Conflict mapping; no direct EF membership sets  
- UI: still read-only membership banner  

---

## 14. Regression results

| Suite | Result |
|---|---|
| `AiSchedTg5Prompt5*` (unit, includes 5A) | **34** passed, 0 failed |
| Combined Prompt5 + Prompt2 + Prompt4 + TG.4A P8/P10 | **76** passed, 0 failed |
| `TeachingGroupMembership*` (PostgreSQL IntegrationTests) | **3** passed, 0 failed |
| `Abhyanvaya.API` build | **0 Error(s)** |
| `abhyanvaya-ui` (`npm run build`) | **✓ built** |

PostgreSQL evidence commands:

```text
dotnet test Abhyanvaya.IntegrationTests/Abhyanvaya.IntegrationTests.csproj --filter FullyQualifiedName~TeachingGroupMembership
```

---

## 15. Known limitations

1. No `WebApplicationFactory` host RBAC suite in this repository — policies verified by controller/`Program.cs` source guards.  
2. ExclusionGroupKey peer resolve is N×Resolve per mutation (acceptable for small capacity-split peer sets).  
3. Section-link mutations are outside this service; ExclusionGroupKey on section attach is a separate boundary.  
4. Published/Frozen are not domain statuses — Locked/Archived cover immutability.  

---

## 16. Final decision

**CONDITIONAL PASS**

Material application gates are proven (Model B, resolved ExclusionGroupKey, capacity, lifecycle, tenant, idempotency, PostgreSQL uniqueness/concurrency, DTO cleanup, architecture guards, API/UI builds, TG.4A regression).

Not FULL PASS solely because live-host HTTP RBAC (`WebApplicationFactory`) is unavailable in the existing test architecture — classified honestly per gate rules.
