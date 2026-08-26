# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3E  
# Legacy Semester Disposition Finalization

**Date:** 2026-08-22  
**Type:** Controlled disposition finalization (journal + fail-closed)  
**Teaching Group:** OUT OF SCOPE / IDENTIFY-ONLY  
**Schema hardening:** NOT applied  
**Final status:** PASS

---

## 1. Objective

Finalize Architect-approved dispositions for remaining NULL-group Semesters that are **not** blocked by the frozen Teaching Group boundary. Produce an auditable, idempotent execution result and re-run the Prompt 3D finalization audit.

---

## 2. Before audit (Prompt 3D baseline)

| SemId | Disposition | Action in 3E |
| ---: | --- | --- |
| 1 | MANUAL_MAPPING_REQUIRED | Block — no mutation |
| 2 | HISTORICAL_RETAIN | **RETAIN_HISTORICAL** journal |
| 3 | BLOCKED_BY_TEACHING_GROUP_REFERENCE | Defer TG — no mutation |
| 4 | DUPLICATE_REVIEW | Block — no mutation |
| 5 | DUPLICATE_REVIEW | Block — no mutation |

TG residuals (Ids 1–2 → candidate Sem 11): **excluded**.

---

## 3. Disposition matrix

| Code | Mutation | Notes |
| --- | --- | --- |
| RETAIN_HISTORICAL | Journal only; `Semester.GroupId` stays NULL | Sem 2 |
| MANUAL_MAPPING_REQUIRED | None | Sem 1 (Subject ref) |
| DUPLICATE_REVIEW | None | Sem 4/5 |
| BLOCKED_BY_TEACHING_GROUP_REFERENCE | None | Sem 3 + 2 TGs |
| ALREADY_GROUP_SPECIFIC | None | Sem 9 never in NULL inventory |
| FINALIZED_LEGACY | GroupId assign | **Disabled** in production (requires explicit Architect approval flag; not used for live data) |

**Critical rule:** SAFE_SINGLE_GROUP_MAPPING is **not** auto-executed. No name-based / first-Group / Student-majority inference.

---

## 4. API

| Endpoint | Mode |
| --- | --- |
| `GET /api/semester/legacy-finalization-execution-preview` | Read-only |
| `POST /api/semester/legacy-finalization/execute` | Transactional journal write |
| `GET /api/semester/legacy-finalization-audit` | Post-audit (3D) |

Auth: `CanManageSemesters`.

---

## 5. Transaction / idempotency

- Single `ExecuteInTransactionAsync` boundary.
- Re-read Semester before any write; abort on baseline drift.
- Journal table: `LegacySemesterDispositionJournals` (migration `20260822180000_...`).
- Second execution: `AlreadyComplete`, zero journal writes.
- Rollback on Abort (`RolledBack=true`).

---

## 6. Records changed / retained / blocked

| Category | Expected (tenant 1) |
| --- | --- |
| Retained (journal) | Sem **2** |
| Semester.GroupId mutations | **0** |
| Blocked DUPLICATE | Sem 4, 5 |
| Manual review | Sem 1 |
| Deferred TG | Sem 3 (TG 1, 2) |
| TeachingGroup mutations | **0** |

---

## 7. Schema hardening readiness

After 3E:

| Gate | Ready? |
| --- | ---: |
| `Semester.GroupId NOT NULL` | **NO** |
| `UNIQUE(TenantId, GroupId, Number)` | **NO** |

Remaining blockers: NULL-group rows (1,3,4,5 + retained 2), TG residuals, Subject/Section refs, wildcard deps.

---

## 8. Explicit exclusions

- No TeachingGroup / TeachingGroupSection / TimetableSection mutation
- No Attendance / SubjectAllocation / TimetableEntry / Student mutation
- No NOT NULL / UNIQUE on Semester
- No wildcard removal
- No legacy Semester deletion/merge

---

## 9. STOP

Do **not** automatically start:

- Teaching Group Semester remediation
- NOT NULL migration
- Unique index migration
- Wildcard removal
- Legacy Semester deletion

---

## 10. Live evidence

| Metric | Value |
| --- | --- |
| ExecutionStatus (1st) | **Completed** |
| RetainedCount | **1** (Sem 2) |
| ChangedCount (Semester.GroupId) | **0** |
| ManualReviewCount | 1 (Sem 1) |
| BlockedCount | 2 (Sem 4, 5) |
| DeferredTeachingGroupCount | 1 (Sem 3; TG 1+2 → candidate 11) |
| ExecutionStatus (2nd) | **AlreadyComplete** |
| AlreadyCompleteCount | 1 |
| RolledBack | false |
| Post NullGroup | 5 |
| Post TG residuals | 2 |
| Post NotNullReady | **false** |
| SchemaHardeningReady | **false** |
| Regression (TG/CAP/P1-4 focused) | **416** passed |
| API build | **PASS** (0 errors) |
| UI build | N/A (UI not touched) |

---

## 11. Files

- `LegacySemesterFinalizationExecutionService` + planner + DTOs
- `LegacySemesterDispositionJournal` entity + EF migration (journal table only)
- `GET .../legacy-finalization-execution-preview`
- `POST .../legacy-finalization/execute`
- Architecture guards + unit tests
- Runner: `--finalization-preview` / `--finalization-execute`
