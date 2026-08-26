# AI-SCHED-TG.5 Prompt 5A.1 — PostgreSQL Concurrency & Persistence Error Mapping Final Gate

**Workstream:** AI-SCHED-TG.5  
**Prompt:** 5A.1  
**Date:** 2026-08-19  
**Type:** Final hardening gate (no redesign / no migration / no UI)  
**Status:** FULL PASS

---

## 1. Scope

Close two Prompt 5A review findings only:

1. Genuine concurrent PostgreSQL race for current Teaching Group membership.  
2. Narrow `DbUpdateException` → `ConcurrencyConflictException` mapping to the **approved membership unique index / table invariant only**.

---

## 2. Existing defect addressed

| Defect | Fix |
|---|---|
| “Concurrency” test called `SaveChanges` sequentially on two contexts | Barrier-synchronized dual-task race (`CountdownEvent`) |
| `catch (DbUpdateException) → ConcurrencyConflictException` for **all** persistence errors | `TeachingGroupMembershipPersistenceExceptionMapper` requires Postgres `23505` **and** membership uniqueness identity |

---

## 3. PostgreSQL constraint/index used

```text
IX_SchedulingTeachingGroupMembership_TenantId_TeachingGroupId_StudentId
UNIQUE (TenantId, TeachingGroupId, StudentId)
WHERE "IsCurrent" = TRUE AND "IsDeleted" = FALSE
```

Table: `SchedulingTeachingGroupMembership` (sole unique index on this table).  
Source: TG.3 migration `20260817153000_AI_SCHED_TG_3_TeachingGroup` + EF configuration.  
**No new migration.**

---

## 4. Genuine concurrency test methodology

`Genuine_concurrent_duplicate_current_membership_one_succeeds_one_conflicts`:

1. Seed TG + Student once.  
2. Two independent `ApplicationDbContext` instances / connections.  
3. Each task: `Add` same current membership → `CountdownEvent.Signal()` → `Wait()` until both ready.  
4. Both call `SaveChangesAsync` after the barrier (no `Thread.Sleep`).  
5. Assert: exactly one success, one `ConcurrencyConflictException` via narrow mapper, final current row count = 1.

---

## 5. Concurrency test evidence

```text
dotnet test Abhyanvaya.IntegrationTests --filter FullyQualifiedName~TeachingGroupMembershipConcurrency
Passed!  - Failed: 0, Passed: 6, Skipped: 0
```

Includes: index exists, genuine race, sequential duplicate map, FK not mapped, unrelated unique not mapped, app-service idempotent add.

---

## 6. Exception mapping implementation

`Abhyanvaya.Application/Internal/TeachingGroupMembershipPersistenceExceptionMapper.cs`

```text
DbUpdateException
  → walk InnerException / AggregateException
  → Npgsql.PostgresException (type name via reflection; no Application→Npgsql package dep)
  → SqlState == 23505
  → ConstraintName == approved index (or unambiguous truncated prefix)
     OR Message contains approved index name
     OR TableName == SchedulingTeachingGroupMembership (sole unique on table)
  → YES: ConcurrencyConflictException
  → NO: rethrow original DbUpdateException
```

`TeachingGroupMembershipApplicationService.SaveMembershipChangesAsync` calls `RethrowUnlessCurrentMembershipUniqueViolation`.

HTTP path unchanged: `ConcurrencyConflictException` → controller `Conflict` → **409**.

---

## 7. Non-concurrency error evidence

| Case | Result |
|---|---|
| FK violation (invalid StudentId) | `TryMap…` = **false** |
| Unrelated unique (SubjectAllocation academic unique) | `TryMap…` = **false** |
| Non-Postgres `DbUpdateException` (unit) | `TryMap…` = **false**; rethrow preserves instance |

---

## 8. Regression results

| Suite | Result |
|---|---|
| `AiSchedTg5Prompt5*` unit | **38** passed (includes 5A.1 mapping tests) |
| Combined TG.5 Prompt2/4/5 + TG.4A P8/P10 + TeachingGroup domain/EF | **141** passed, 0 failed |
| PostgreSQL membership concurrency IT | **6** passed, 0 failed |

---

## 9. Architecture Guard results

Prompt 5 / 5A architecture guards remain green within the 141-pass suite.  
5A.1 adds source guard: service uses narrow mapper; blanket `catch (DbUpdateException) → ConcurrencyConflictException` removed.

---

## 10. Build results

| Build | Result |
|---|---|
| `Abhyanvaya.API` | **0 Error(s)** |
| `abhyanvaya-ui` (`npm run build`) | **✓ built** |

---

## 11. RBAC limitation

Live host `WebApplicationFactory` RBAC: **DATA UNAVAILABLE** (unchanged).  
Static View/Manage policy verification from Prompt 5A remains. Per prompt instructions this is not a failed architecture gate.

---

## 12. Explicitly NOT changed

- Membership Model B / Hybrid semantics  
- ExclusionGroupKey resolved-roster rules  
- Capacity / lifecycle / tenant rules  
- API routes / DTOs  
- Schema / migrations  
- UI  
- Attendance / StudentSection / TimetableSection / TeachingGroupSection SoT  
- Auto-create / SA→TG inference  

---

## 13. Final decision

**FULL PASS**

Genuine concurrent race proven; membership unique violation alone maps to conflict/409; unrelated persistence errors propagate; regressions, architecture guards, API/UI builds verified; no migration.
