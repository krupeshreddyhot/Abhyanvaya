# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3J  
# Controlled Subject Catalog Semester Remediation

**Date:** 2026-08-23  
**Type:** Data remediation — `Subject.SemesterId` only  
**PromptCode:** `P1-4-3J`  
**Journal:** `SUBJECT_CATALOG_SEMESTER_REMAP`  
**Schema hardening:** NOT applied  

---

## 1. Problem

Prompt 3H reported **18** Subject catalog rows still referencing NULL-group Semesters. Section remediations (3G/3I) and TG remediation (3F) do not move Subject Catalog Semester FKs.

---

## 2. Target architecture

```
Subject.GroupId → Semester.GroupId
Subject.CourseId → Semester.CourseId
Semester.Number == Legacy.Number
```

Identity is **not** inferred from names, departments, programs, TG, TT, or attendance.

---

## 3. Classification

| Code | Meaning |
| --- | --- |
| `ALREADY_CORRECT` | On Group-specific Sem matching Course/Group |
| `SAFE_TO_REMAP` | Exactly one deterministic target |
| `MANUAL_MAPPING_REQUIRED` | Multiple targets |
| `BLOCKED` | No safe path / TG Sem mismatch / duplicate key |
| `HISTORICAL_RETAIN` | No target; no SA/TG; retain |
| `ALREADY_COMPLETE` | Journaled remapping already applied |

---

## 4. APIs

| Endpoint | Mode |
| --- | --- |
| `GET /api/semester/subject-catalog-remediation-preview` | Read-only |
| `POST /api/semester/subject-catalog-remediation/execute` | Transactional SAFE only |

Auth: `CanManageSemesters`. Runner: `--subject-catalog-remediate-preview` / `--subject-catalog-remediate-execute`.

UI: deferred (admin API only; no Auto Fix All).

---

## 5. Boundaries

| Forbidden |
| --- |
| Semester.GroupId assignment / delete / merge |
| TeachingGroup / TGS / TimetableSection mutation |
| SubjectAllocation / TimetableEntry / Attendance / Student mutation |

---

## 6. Transaction / idempotency

- `ExecuteInTransactionAsync` + single `ConcurrencyExceptionHelper.SaveChangesAsync`
- Second run → `AlreadyComplete`, ChangedCount=0

---

## 7. Live results (2026-08-23)

### Preview
| Metric | Value |
| --- | --- |
| SAFE_TO_REMAP | **17** |
| HISTORICAL_RETAIN | **1** (Subject **11**, legacy Sem **1** / Number=1 — no Group-specific Sem I) |
| MANUAL / BLOCKED | 0 / 0 |

Finance Group subjects → Sem **10**; CA Group subjects → Sem **11**.

### Execution
| Metric | Value |
| --- | --- |
| Status | **Completed** |
| ChangedCount | **17** |
| TG / SA / TimetableSection | unchanged |
| CorrelationId | `250fbd1c093d4c6791fd1c260aef4069` |

### Idempotent re-run
| Metric | Value |
| --- | --- |
| Status | **AlreadyComplete** |
| ChangedCount | **0** |
| AlreadyCompleteCount | 17 |
| HistoricalRetain | 1 (unchanged) |

### Post-integrity
| Metric | Value |
| --- | --- |
| IsHealthy | **true** |
| Critical / Errors | **0 / 0** |
| Warnings | 5 × legacy NULL-group Semesters (expected) |

---

## 8. Residuals / deferred

- Subject **11** HISTORICAL_RETAIN on Sem 1 (needs Architect Sem I Group-specific or retain disposition)
- Legacy Semesters 1–5 still exist
- Wildcard deprecation
- NOT NULL / UNIQUE still **not** ready

---

## STOP

Do **not** start 3K or database hardening from this prompt.
