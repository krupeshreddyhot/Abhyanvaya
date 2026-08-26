# AI-SCHED-TG.4 Prompt 2 — TimetableEntry TeachingGroupId Domain & EF Integration

**Workstream:** AI-SCHED-TG.4  
**Prompt:** 2 — TimetableEntry → TeachingGroup domain/EF relationship  
**Date:** 2026-08-18  
**Predecessor:** AI-SCHED-TG.4 Prompt 1 (PASS — discovery)

**STATUS: PASS**

---

## 1. Architecture changes

Introduced the explicit optional relationship:

```text
TeachingGroup 1 ──── * TimetableEntry
```

Authoritative resolve key when assigned: `TimetableEntry.TeachingGroupId`.

**Not** inferred from SubjectAllocation, Section, TimetableSection, Course/Group/Semester/Subject.

TimetableSection remains the untouched compatibility bridge for Attendance/legacy until a later prompt. It is **not** the TeachingGroup SoT.

TG.3 TeachingGroup schema left unchanged.

---

## 2. Domain changes

`TimetableEntry`:

- `int? TeachingGroupId`
- `TeachingGroup? TeachingGroup`

Nullable for incremental migration.

`TeachingGroupRules.EnsureTimetableEntryTeachingGroupTenant(...)` — reusable application-level tenant check (PostgreSQL cannot enforce TenantId equality on the FK). Does **not** resolve or create TeachingGroups.

`TimetableService.CloneEntry` copies `TeachingGroupId` (including null) so clone/copy does not invent associations and does not fail on null.

`ApplyAllocationDenormalization` does **not** set TeachingGroupId.

---

## 3. EF configuration

`TimetableEntryConfiguration`:

| Setting | Value |
|---|---|
| Relationship | `HasOne(TeachingGroup).WithMany().HasForeignKey(TeachingGroupId)` |
| Required | false (nullable) |
| Delete | **Restrict** |
| Index | `(TenantId, TeachingGroupId)` |

No Cascade from TeachingGroup → TimetableEntry.  
No SectionGroupId.  
No SubjectAllocation uniqueness for TeachingGroup.

---

## 4. Migration details

| Item | Value |
|---|---|
| ID | `20260818110000_AI_SCHED_TG_4_TimetableEntryTeachingGroupId` |
| File | `Abhyanvaya.Infrastructure/Persistence/Migrations/20260818110000_AI_SCHED_TG_4_TimetableEntryTeachingGroupId.cs` |
| Approach | Hand-authored focused migration (ModelSnapshot drift avoidance) |
| Up | Add nullable column + Restrict FK + indexes |
| Applied to | Local disposable `abhyanvaya_db` — **Done** |
| Backfill | **None** (no synthetic TeachingGroups) |

Does not alter: TeachingGroup tables, TimetableSection, StudentSection, Attendance, SectionGroup, SubjectAllocation.

---

## 5. Tenant isolation

| Layer | Behavior |
|---|---|
| Global query filters | Unchanged BaseEntity tenant + soft-delete |
| IgnoreQueryFilters | **Not introduced** |
| DB FK | Enforces TeachingGroup row exists; **cannot** enforce TenantId match |
| Application | `EnsureTimetableEntryTeachingGroupTenant` for same-tenant validation when assigning |

Documented limitation: cross-tenant TenantId equality is application-enforced, consistent with existing Abhyanvaya FK patterns.

---

## 6. Compatibility behavior

| Scenario | Result |
|---|---|
| `TeachingGroupId == null` | Valid; existing timetable load/grid/lifecycle/approval/conflict paths unchanged |
| TimetableSection | Untouched |
| AttendanceSessionResolver | Untouched (still TimetableSections + Legacy) |
| Governance / publish / lock / freeze | Untouched |
| Entry create via allocation denorm | Does not auto-set TeachingGroupId |
| Clone/copy | Preserves null or existing TeachingGroupId |

---

## 7. Tests

| Suite | Failed | Passed | Skipped |
|---|---:|---:|---:|
| TimetableEntryTeachingGroupEf + TeachingGroup domain/EF + ArchitectureGuard + SchedulingFoundation + TimetableEntryMapping + Phase2APermissionKeys + TimetableStatusTransition | 0 | 107 | 0 |

Coverage includes: nullable FK, Restrict delete, many entries per TG, many TGs per SA, no SA inference methods, tenant rejection, no SectionId requirement, migration focus, Attendance untouched, clone null-safe.

---

## 8. Architecture Guard

Focused guards in `TimetableEntryTeachingGroupEfTests`:

- no IgnoreQueryFilters in TimetableEntry/TG config/rules  
- no SectionGroupId on TimetableEntry/TeachingGroup  
- no CreateTeachingGroup / ResolveTeachingGroupFromSubjectAllocation in TimetableService  
- AttendanceSessionResolver still TimetableSection-based, no TeachingGroup  
- TeachingGroup FK delete is Restrict (not Cascade)

Existing AI29 Architecture Guard suite included in the 107-pass run.

---

## 9. Build results

| Build | Result |
|---|---|
| API | PASS (0 errors) |
| UI | PASS (no UI source changes) |

---

## 10. Database verification

On `localhost` / `abhyanvaya_db`:

| Check | Result |
|---|---|
| Migration in `__EFMigrationsHistory` | Present |
| Column `TeachingGroupId` | integer, **nullable** |
| FK | `… → SchedulingTeachingGroup(Id) ON DELETE RESTRICT` |
| Indexes | `(TenantId, TeachingGroupId)` + `TeachingGroupId` |
| TeachingGroup tables | Still present / unchanged |
| Existing entries | 0 rows post–Prompt 3 cutover; all would be NULL-capable |

---

## 11. Deferred work

Explicitly **not** implemented:

- TeachingGroup application / resolution services  
- legacy PUT `/sections` façade  
- TimetableSection projection from TeachingGroupSection  
- TeachingGroup API / UI  
- Attendance TG enrichment  
- automatic TeachingGroup creation  
- allocation engine integration  
- timetable designer redesign  
- mandatory TeachingGroupId  

---

## 12. Deviations

1. Hand-authored migration (same rationale as TG.3 Prompt 2).  
2. CloneEntry copies TeachingGroupId — additive compatibility, not API redesign.  
3. Tenant equality validated via domain helper; not a composite DB FK.

---

## Final acceptance checklist

| # | Criterion | Met |
|---|---|---|
| 1 | TeachingGroupId exists and nullable | Yes |
| 2 | EF FK correctly configured | Yes |
| 3 | Delete Restrict/NoAction | Yes (Restrict) |
| 4 | Tenant isolation preserved | Yes |
| 5 | Existing timetable null path works | Yes |
| 6 | No automatic TG create/resolve | Yes |
| 7 | TimetableSection untouched | Yes |
| 8 | Attendance untouched | Yes |
| 9 | Governance untouched | Yes |
| 10 | TeachingGroup domain tests green | Yes |
| 11 | Architecture Guard green | Yes |
| 12 | API build PASS | Yes |
| 13 | UI build PASS | Yes |
| 14 | Relevant regression PASS | Yes (107/0/0) |
| 15 | Migration inspected + applied | Yes |

**STATUS = PASS**

Do not proceed to legacy bridge / Attendance / UI without an explicit next prompt.
