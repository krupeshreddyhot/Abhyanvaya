# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3I  
# Controlled Finance Section Semester Remediation

**Date:** 2026-08-23  
**Type:** Data remediation — Finance `Section.SemesterId` only  
**Legacy → Target:** Semester **3** → Semester **10** (Finance / Group **1**)  
**PromptCode:** `P1-4-3I`  
**Journal:** `FINANCE_SECTION_SEMESTER_REMAP`  
**Schema hardening:** NOT applied  

---

## 1. Problem

After Prompt 3G (CA Sections → Sem 11) and Prompt 3F (TG → Sem 11), Finance Sections **9–12** still referenced legacy NULL-group Sem **3**. Prompt 3H classified Sem 3 as `BLOCKED_BY_REFERENCE` (Section refs=4).

Approved model: `Group → Semester → Section`. Finance Sections must use Finance Sem III (**Id=10**, `GroupId=1`, `Number=3`).

---

## 2. Architecture

| Rule | Enforcement |
| --- | --- |
| Identity | `GroupId` / `Semester.GroupId` / `CourseId` — **not** names |
| Finance Group | Contract `ExpectedFinanceGroupId=1` + Sem 10 ownership |
| Target Sem | Exactly Id **10**; multiple candidates → abort |
| CA Sections | `NOT_IN_SCOPE` |
| TG / TGS / Student / SA / TT / TimetableSection | **never mutated** |

---

## 3. APIs

| Endpoint | Mode |
| --- | --- |
| `GET /api/semester/finance-section-remediation-preview` | Read-only |
| `POST /api/semester/finance-section-remediation/execute` | Transactional |

Auth: `CanManageSemesters`. Runner: `--finance-section-remediate-preview` / `--finance-section-remediate-execute`.

Client cannot supply authoritative source/target Semester or Group IDs.

---

## 4. Eligibility

`SAFE_TO_REMEDIATE` only when all hold: tenant match; `GroupId=1`; Course matches Group/Semester; current Sem=3; target Sem=10 with GroupId=1, CourseId match, Number=3; no code collision; no TG with `SemesterId ≠ 10` linking the Section.

TG mismatch → **BLOCKED** (do not mutate TG).

---

## 5. Transaction / concurrency / idempotency

- `ExecuteInTransactionAsync` — atomic batch  
- Single `ConcurrencyExceptionHelper.SaveChangesAsync`  
- Second run → `AlreadyComplete`, ChangedCount=0  

---

## 6. UI

Deferred (consistent with P1-4 phased approach). Admin APIs documented.

---

## 7. Live results (2026-08-23)

### Preview
| Metric | Value |
| --- | --- |
| ExecutionSafe | **true** |
| Approved Sections | **9, 10, 11, 12** |
| Eligible | 4 SAFE_TO_REMEDIATE |
| TG links | none |

### Execution
| Metric | Value |
| --- | --- |
| Status | **Completed** |
| ChangedCount | **4** (9–12 → Sem **10**) |
| TG / TGS / Student | unchanged |
| Transaction | committed |

### Idempotent re-run
| Metric | Value |
| --- | --- |
| Status | **AlreadyComplete** |
| ChangedCount | **0** |

---

## 8. Residuals

- Subject catalog still on NULL-group Semesters (Prompt 3H)  
- Legacy Semesters 1/2/4/5 dispositions  
- Wildcard deprecation  
- Schema NOT NULL / UNIQUE still **not** ready  
- Sem 3 may still have Subject refs even after Section remaps  

---

## STOP

Do **not** start Prompt 3J or schema hardening from this prompt.
