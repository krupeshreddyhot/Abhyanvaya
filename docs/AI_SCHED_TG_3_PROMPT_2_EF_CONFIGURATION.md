# AI-SCHED-TG.3 Prompt 2 — EF Core Configuration & Relationship Integrity

**Workstream:** AI-SCHED-TG.3  
**Prompt:** 2 — EF Core configuration & relationship integrity  
**Date:** 2026-08-18  
**Predecessor:** AI-SCHED-TG.3 Prompt 1A (FULL PASS — frozen)

**STATUS: PASS**

---

## 1. Executive Summary

Configured the approved TeachingGroup domain model for EF Core persistence:

| Table | Entity |
|---|---|
| `SchedulingTeachingGroup` | `TeachingGroup` |
| `SchedulingTeachingGroupSection` | `TeachingGroupSection` |
| `SchedulingTeachingGroupMembership` | `TeachingGroupMembership` |

SubjectAllocation → **many** TeachingGroups (no unique SubjectAllocationId).  
TeachingGroupSection is the **sole** TG → Section relationship (`SectionGroupId` absent).  
Cascade deletes never target Student / Section / StudentSection / StudentSubject / SubjectAllocation.  
Migration `20260817153000_AI_SCHED_TG_3_TeachingGroup` was **generated and inspected** but **NOT applied**.

Out of scope (unchanged): TimetableEntry.TeachingGroupId, TimetableSection, Attendance, APIs, UI, membership resolver.

---

## 2. Entities configured

| Entity | Configuration class | DbSet / IQueryable |
|---|---|---|
| TeachingGroup | `TeachingGroupConfiguration` | `SchedulingTeachingGroups` |
| TeachingGroupSection | `TeachingGroupSectionConfiguration` | `SchedulingTeachingGroupSections` |
| TeachingGroupMembership | `TeachingGroupMembershipConfiguration` | `SchedulingTeachingGroupMemberships` |

Registered on `ApplicationDbContext` + `IApplicationDbContext` using existing `IQueryable<T> => Set<T>()` convention.  
Force-included via `builder.Entity<TeachingGroup*>()` in `OnModelCreating`.  
Configs discovered by `ApplyConfigurationsFromAssembly`.

---

## 3. Relationships

```
SubjectAllocation 1 ── * TeachingGroup
TeachingGroup 1 ── * TeachingGroupSection * ── 1 Section
TeachingGroup 1 ── * TeachingGroupMembership * ── 1 Student
```

- No TeachingGroup → SectionGroup relationship.  
- No TeachingGroupMembership → StudentSection / StudentSubject relationship.  
- SubjectAllocation inverse collection omitted (`.WithMany()`), matching TimetableEntry convention.

---

## 4. Primary keys

All three entities use `BaseEntity.Id` (int identity) — project convention.  
TeachingGroup is **not** keyed by SubjectAllocationId / SectionId / StudentId.

---

## 5. Foreign keys

| Dependent | Principal | Column | Delete |
|---|---|---|---|
| TeachingGroup | SubjectAllocation | SubjectAllocationId | Restrict |
| TeachingGroup | AcademicYear | AcademicYearId | Restrict |
| TeachingGroup | Course | CourseId | Restrict |
| TeachingGroup | Group | GroupId | Restrict |
| TeachingGroup | Semester | SemesterId | Restrict |
| TeachingGroup | Subject | SubjectId | Restrict |
| TeachingGroupSection | TeachingGroup | TeachingGroupId | Cascade |
| TeachingGroupSection | Section | SectionId | Restrict |
| TeachingGroupMembership | TeachingGroup | TeachingGroupId | Cascade |
| TeachingGroupMembership | Student | StudentId | Restrict |

---

## 6. Nullability

| Field | Nullability |
|---|---|
| ExpectedStudentCount | nullable |
| MaxTeachingCapacity | nullable |
| Code, ExclusionGroupKey, Notes, EffectiveTo | nullable |
| Name | required |
| Type / MembershipSource / Status / ActivityKind / Inclusion | required (byte) |

Capacity business rules (Max=0 invalid, Expected>Max invalid) remain **domain** (`SetCapacity`) — not DB CHECK constraints (project does not generally encode such rules as CHECKs).

---

## 7. Delete behaviors

| Relationship | Behavior | Rationale |
|---|---|---|
| TG → SubjectAllocation / academic masters | Restrict | Must not delete allocation or catalog rows |
| TGSection → TG | Cascade | Owned child of TeachingGroup (hard-delete dependents) |
| TGMembership → TG | Cascade | Owned child of TeachingGroup |
| TGSection → Section | Restrict | Academic Section must survive |
| TGMembership → Student | Restrict | Student must survive |

**Critical:** No cascade path into StudentSection or StudentSubject (no FKs configured). Soft-delete remains the primary operational delete model via `IsDeleted`.

---

## 8. Tenant configuration

All three entities inherit `BaseEntity` → participate in existing:

- `TenantId` assignment on save  
- Global tenant query filter  
- No second tenant mechanism  
- No `IgnoreQueryFilters` in TeachingGroup configs/repositories

Cross-tenant consistency for FKs remains application/domain enforced (existing architecture); no composite tenant FK invented.

---

## 9. Query filters

BaseEntity global filter applies automatically:

`!IsDeleted && (user null || SuperAdmin || TenantId == current)`

Verified by EF integrity tests (soft-deleted and cross-tenant rows hidden unless `IgnoreQueryFilters` used in **test** assertions only).

---

## 10. Indexes

| Index | Unique | Rationale |
|---|---|---|
| `(TenantId, SubjectAllocationId)` | No | List TGs for an allocation |
| `(TenantId, SubjectAllocationId, ExclusionGroupKey)` | No | Mutual-exclusion peer lookup (key shared intentionally) |
| `(TenantId, Status)` | No | Lifecycle queries |
| `(TenantId, TeachingGroupId, SectionId)` filter `IsDeleted = FALSE` | **Yes** | Prevent duplicate TG↔Section links |
| `(TenantId, SectionId)` | No | Reverse section lookup |
| `(TenantId, TeachingGroupId, StudentId)` filter `IsCurrent AND NOT IsDeleted` | **Yes** | One current membership; history allowed |
| `(TenantId, TeachingGroupId)` / `(TenantId, StudentId)` | No | Membership list / student reverse lookup |

EF also emits supporting single-column FK indexes in the migration.

---

## 11. Unique constraints

- **NOT** unique: `SubjectAllocationId` alone; `ExclusionGroupKey` alone.  
- **Unique filtered:** TG + Section (active rows).  
- **Unique filtered:** TG + Student where `IsCurrent = TRUE` (preserves EffectiveFrom/EffectiveTo history).

---

## 12. Enum storage

`HasConversion<byte>()` → PostgreSQL `smallint`, matching Scheduling conventions (`TimetableStatus`, conflict enums, etc.).

Enums: TeachingGroupType, MembershipSource, Status, ActivityKind, MembershipInclusion.

---

## 13. Audit configuration

Inherited from `BaseEntity`: CreatedDate, CreatedBy, UpdatedDate, UpdatedBy, IsDeleted, TenantId.  
No duplicate audit abstraction.

---

## 14. Soft-delete behavior

`IsDeleted` + global query filter. Soft-deleting a TeachingGroup does not delete Section/Student rows.  
Physical Cascade on children applies only to hard `Remove()` of TeachingGroup (owned dependents).

---

## 15. Concurrency behavior

No new concurrency token. Follows existing BaseEntity audit/update semantics (application-layer concurrency deferred — same as most Scheduling entities).

---

## 16. Migration identifier

`20260817153000_AI_SCHED_TG_3_TeachingGroup`  
File: `Abhyanvaya.Infrastructure/Persistence/Migrations/20260817153000_AI_SCHED_TG_3_TeachingGroup.cs`

---

## 17. Migration inspection findings

### Generation approach (documented deviation)

`ApplicationDbContextModelSnapshot` under `Infrastructure/Migrations` lags hand-authored AI29 Persistence migrations. A naïve `dotnet ef migrations add` would emit **unrelated** AllocationEngine / Section lifecycle / etc. schema diffs → would be **BLOCKED** per prompt rules.

Therefore this migration is **hand-authored and focused**, matching the established AI29 Persistence/Migrations pattern (e.g. `AI29_1C_AllocationEngine`).

### Inspection checklist

| Check | Result |
|---|---|
| Creates only SchedulingTeachingGroup* tables | Pass |
| No TimetableEntry.TeachingGroupId | Pass |
| No SectionGroupId | Pass |
| No ResolvedStudentCount / PlannedCapacity | Pass |
| No StudentSection / StudentSubject / Attendance alterations | Pass |
| No unique SubjectAllocationId | Pass |
| No DropTable/AlterColumn in Up | Pass |
| Down drops only the three new tables | Pass |

**Migration was NOT applied to any database.**

---

## 18. Tests

| Suite | Failed | Passed | Skipped |
|---|---:|---:|---:|
| TeachingGroupDomainTests + TeachingGroupEfModelIntegrityTests + ArchitectureGuard + SchedulingFoundation + SubjectAllocationService | 0 | 79 | 0 |

EF integrity coverage includes: many-TG-per-allocation, no SubjectAllocation uniqueness, section/membership graphs, no StudentId on TGSection, Restrict cascades, filtered uniques, ExclusionGroupKey non-unique, nullable capacity, no ResolvedStudentCount/SectionGroupId, tenant + soft-delete filters, migration text review, no IgnoreQueryFilters in configs.

---

## 19. Build results

| Build | Result |
|---|---|
| API (`Abhyanvaya.API`) | PASS (rebuild after transient DLL lock from parallel test build) |
| UI (`abhyanvaya-ui` `npm run build`) | PASS — no source changes |

---

## 20. Architecture Guard results

Architecture Guard tests included in the 79-pass run. TeachingGroup EF work did not introduce:

- IgnoreQueryFilters for tenant access in TG configs  
- SubjectAllocation uniqueness  
- SectionGroupId / second Section SoT  
- Room.Capacity on TeachingGroup  
- TimetableSection / StudentSection mutation from TG domain

---

## 21. Files changed

| File | Action |
|---|---|
| `Infrastructure/.../TeachingGroupConfiguration.cs` | Added |
| `Infrastructure/.../TeachingGroupSectionConfiguration.cs` | Added |
| `Infrastructure/.../TeachingGroupMembershipConfiguration.cs` | Added |
| `Infrastructure/.../Migrations/20260817153000_AI_SCHED_TG_3_TeachingGroup.cs` | Added (not applied) |
| `Infrastructure/Persistence/ApplicationDbContext.cs` | DbSets + entity registration |
| `Application/Common/Interfaces/IApplicationDbContext.cs` | IQueryable properties |
| `Application.UnitTests/Scheduling/TeachingGroupEfModelIntegrityTests.cs` | Added |
| `docs/AI_SCHED_TG_3_PROMPT_2_EF_CONFIGURATION.md` | Added |

---

## 22. Deviations

1. **Hand-authored migration** instead of auto-diff against stale ModelSnapshot (required to avoid unrelated schema changes).  
2. **SubjectAllocation.TeachingGroups** inverse collection not added — `.WithMany()` empty, consistent with TimetableEntry → SubjectAllocation.  
3. Capacity rules not expressed as DB CHECK constraints (domain/application only).

---

## 23. Explicit confirmation — migration NOT applied

Confirmed: **no** `dotnet ef database update` / `MigrateAsync` targeting this migration was executed as part of Prompt 2 deliverable work. Schema changes exist only as migration source + EF model configuration.

---

## Deferred / Out of Scope Findings

None that block EF configuration. Membership resolver, TimetableEntry.TeachingGroupId, TimetableSection projection, Attendance, APIs, and UI remain later prompts.

---

## Final architectural gate checklist

| # | Criterion | Met |
|---|---|---|
| 1 | Correct EF relationships | Yes |
| 2 | SubjectAllocation → many TeachingGroups | Yes |
| 3 | No SubjectAllocation unique constraint | Yes |
| 4 | TeachingGroupSection sole TG → Section | Yes |
| 5 | SectionGroupId absent | Yes |
| 6 | Membership TG → Student correct | Yes |
| 7 | No dangerous cascade deletes | Yes |
| 8 | Tenant filters preserved | Yes |
| 9 | No IgnoreQueryFilters introduced | Yes |
| 10 | Nullable capacity fields | Yes |
| 11 | ResolvedStudentCount not persisted | Yes |
| 12 | Correct duplicate relationship semantics | Yes |
| 13 | Migration generated and inspected | Yes |
| 14 | Migration NOT applied | Yes |
| 15 | API build PASS | Yes |
| 16 | UI build PASS | Yes |
| 17 | Relevant tests PASS | Yes (79/0/0) |
| 18 | Architecture Guard PASS | Yes |
| 19 | No unrelated production changes | Yes |

---

## Chief Architect handoff

**STATUS = PASS**

AI-SCHED-TG.3 Prompt 2 is approved for migration application and clean pre-production timetable cutover under Prompt 3.

Do **not** apply the migration or start Prompt 3 automatically; wait for an explicit Prompt 3 instruction.
