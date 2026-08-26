# AI-SCHED-TG.5 Prompt 5A.1A — Strict Membership Constraint Identity & Final Freeze

**Workstream:** AI-SCHED-TG.5  
**Prompt:** 5A.1A  
**Date:** 2026-08-19  
**Type:** Final targeted correction + freeze (no redesign / no migration / no UI)  
**Status:** FULL PASS — FROZEN

---

## 1. Scope

Correct Prompt 5A.1 exception-mapping overbreadth:

- Remove table-level / SQLSTATE-only / prefix / message fallbacks.
- Authorize `ConcurrencyConflictException` **only** when `PostgresException.ConstraintName` equals the **actual** PostgreSQL current-membership unique index identity.

---

## 2. Previous Prompt 5A.1 finding

5A.1 correctly added a genuine concurrent race and narrowed blanket `DbUpdateException` handling, but still allowed:

```text
TableName == SchedulingTeachingGroupMembership  →  conflict
```

A future unique index on the same table could incorrectly become HTTP 409.

---

## 3. Actual PostgreSQL constraint/index identity

| Layer | Identifier |
|---|---|
| EF / migration logical name (71 chars) | `IX_SchedulingTeachingGroupMembership_TenantId_TeachingGroupId_StudentId` |
| **Actual PostgreSQL catalog / `ConstraintName` (≤63)** | `IX_SchedulingTeachingGroupMembership_TenantId_TeachingGroupId_S` |

Evidence:

- `pg_indexes` filtered unique + `IsCurrent`/`IsDeleted` → indexname equals approved constant  
- Duplicate-insert `PostgresException.ConstraintName` equals the same value  
- Constant: `TeachingGroupMembershipPersistenceExceptionMapper.ApprovedPostgresConstraintName`

Migration source (unchanged): `20260817153000_AI_SCHED_TG_3_TeachingGroup` — **no new migration**.

---

## 4. Before / after exception mapping

| Before (5A.1) | After (5A.1A) |
|---|---|
| Exact EF name OR truncated prefix OR message contains name OR **table name alone** | **Exact** `ConstraintName == ApprovedPostgresConstraintName` **and** `SqlState == 23505` |
| Table fallback authorized conflict | **Removed** |

Pipeline unchanged:

```text
exact membership unique ConstraintName + 23505
  → ConcurrencyConflictException
  → HTTP 409
```

---

## 5. Exact positive test

- Unit: `Exact_approved_constraint_with_23505_maps`  
- PG: `Duplicate_insert_ConstraintName_equals_approved_postgres_identity`  
- PG: `Approved_current_membership_unique_index_identity_matches_postgres_catalog`

---

## 6. Same-table unrelated constraint negative test

Unit seam (no schema change):

```text
SqlState=23505
ConstraintName=IX_SchedulingTeachingGroupMembership_UnrelatedFutureUnique
→ MatchesApproved… == false
```

Also: EF logical full name with 23505 does **not** map (must use truncated live identity).

---

## 7. Foreign-key negative test

PG: `Foreign_key_violation_is_not_mapped_to_membership_concurrency_conflict` → **false**

---

## 8. Non-PostgreSQL negative test

Unit: `Non_postgres_DbUpdateException_is_not_mapped` → **false**  
Unit: unrelated `DbUpdateException` rethrown unchanged

---

## 9. Genuine concurrency evidence

`Genuine_concurrent_duplicate_current_membership_one_succeeds_one_conflicts`  
Barrier (`CountdownEvent`) · two contexts · **1 success / 1 conflict**

---

## 10. Final database-state evidence

After race: current membership count for `(TG, Student)` = **1**

---

## 11. Regression results

| Suite | Result |
|---|---|
| Prompt 5A.1 / 5A.1A unit filter | **12** passed |
| PostgreSQL membership concurrency IT | **7** passed |
| Combined TG.5 + TG.4A + TeachingGroup domain/EF | **149** passed, 0 failed |

---

## 12. Architecture Guard results

Source guard: mapper has no table-level / SQLSTATE-only / prefix / message authorization.  
Existing Prompt 5/5A guards remain green within the 149-pass suite.

---

## 13. API build

**0 Error(s)**

---

## 14. UI build

**✓ built**

---

## 15. Migration status

**NO NEW MIGRATION** · schema unchanged

---

## 16. RBAC status

**DATA UNAVAILABLE** (no WebApplicationFactory live host suite). Static policy guards unchanged.

---

## 17. Explicit unchanged-scope confirmation

Unchanged: Model B / Hybrid / ExclusionGroupKey / capacity / lifecycle / tenant / resolver / API DTOs / UI / Attendance / StudentSection / TimetableSection / TeachingGroupSection SoT / TG.4A projector / no auto-create / no SA→TG inference.

---

## 18. Final architectural decision

**FULL PASS — FROZEN**

Teaching Group membership application concurrency translation is frozen on exact PostgreSQL constraint identity.
