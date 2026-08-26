# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3J-A  
# Legacy Semester Historical Archive & Operational Disposition

**Date:** 2026-08-23  
**Type:** HISTORICAL DISPOSITION BOUNDARY — controlled mutation (explicit only)  
**Architect package:** `P1-4/3JA`  
**Implementation PromptCode:** `P1-4-3JA`  
*(Subject Catalog remediation already owns journal PromptCode `P1-4-3J`.)*  

**APIs:**  
- `GET /api/semester/legacy-historical-disposition-preview` (read-only)  
- `POST /api/semester/legacy-historical-disposition/execute` (explicit Items required)  

**Auth:** `CanManageSemesters`  
**Runner:** `--legacy-historical-disposition-preview` / `--legacy-historical-disposition-execute`

---

## 1. Problem

Group is the authoritative owner of an operational Semester. Schema hardening (`GroupId NOT NULL`, `UNIQUE(TenantId, GroupId, Number)`) remains blocked while legacy NULL-group Semesters 1–5 remain in the operational table without an explicit historical disposition.

Silent Group assignment, duplicate deletion, and merge-by-heuristic are forbidden.

---

## 2. Current blockers (from P1-4-3I / 3N)

| Item | State |
| --- | --- |
| Semesters 1–5 | `GroupId = NULL` |
| Semester 1 | Historical Subject ref; multi-Group Course → **MANUAL_MAPPING_REQUIRED** |
| Semesters 4 & 5 | Same Number on Course → **DUPLICATE_REVIEW** |
| Semesters 2 & 3 | Ops refs cleared → eligible for **HISTORICAL_ARCHIVE** after explicit approval |
| SchemaHardeningReady | **FALSE** |

---

## 3. Historical vs operational model

| | OPERATIONAL | HISTORICAL |
| --- | --- | --- |
| `GroupId` | Required (non-null) | May remain NULL |
| `IsHistoricalArchive` | `false` | `true` |
| Semester selection / AcademicTree / Student | Included | Excluded |
| New SA / TT / TG / Attendance | Allowed (Group-owned) | Rejected |
| Historical reporting | N/A | Explicit `includeHistorical=true` |
| Soft-delete (`IsDeleted`) | Unchanged | **Not used** as historical marker |

**Why not soft-delete?** Soft-delete removes the row from normal tenant queries and implies disposal. Historical rows must remain readable for audit/FK integrity (e.g. Subject → Sem 1).

**Why a column instead of journal-only?** Operational queries need an efficient, row-local SSOT. Journals remain the audit trail; `IsHistoricalArchive` is the selection gate.

---

## 4. Disposition state machine

```
LEGACY_NULL_GROUP
  ├─ MANUAL_MAPPING_REQUIRED     → journal only (Sem 1 until Architect decision)
  ├─ DUPLICATE_REVIEW            → journal only (Sem 4/5; no delete/merge)
  ├─ RETAIN_HISTORICAL_PENDING_REVIEW → journal only
  └─ HISTORICAL_ARCHIVE          → sets IsHistoricalArchive=true (ops refs must be 0)
```

No disposition assigns `GroupId`. `AssignedGroupId` on journals is always null for P1-4-3JA.

---

## 5. Semester treatments

| Sem | Treatment |
| ---: | --- |
| 1 | **MANUAL_MAPPING_REQUIRED** / pending — never auto-map to Finance or CA |
| 2–3 | **HISTORICAL_ARCHIVE** only after finalization-audit SSOT shows ops refs cleared |
| 4–5 | **DUPLICATE_REVIEW** journal — no ID/CreatedDate/student-count winner |

---

## 6. Downstream reference policy (dependency matrix)

| Entity | Semester FK | Meaning | Can ref archived? | Must remap before archive? |
| --- | --- | --- | --- | --- |
| Student | SemesterId | Operational | Yes (existing) | Yes (for new ops) |
| AttendanceSession | SemesterId | Operational | Yes (existing) | Yes |
| Subject | SemesterId | Catalog / historical | Yes | No (Sem1 Subject may remain) |
| SubjectAllocation | SemesterId | Operational | Yes (existing) | Yes |
| TimetableEntry | SemesterId | Operational | Yes (existing) | Yes |
| Section | SemesterId | Operational | Yes (existing) | Yes |
| TeachingGroup | SemesterId | Operational | Yes (existing) | Yes |
| TimetableSection | projector | Derived | N/A | No direct writes |
| DispositionJournal | SemesterId | Audit | Yes | No |

A reference is **not** assumed historical merely because its Semester has `GroupId=NULL`.

---

## 7. Rollback / idempotency / concurrency

- Entire execute batch is transactional; any blocked item aborts the batch (no partial completion).
- Second identical disposition → `AlreadyComplete`, zero additional writes.
- Uses existing `ExecuteInTransactionAsync` + `ConcurrencyExceptionHelper` / `ConcurrencyConflictException`.

---

## 8. Schema-hardening prerequisites

`IsHistoricalArchive` does **not** clear NullGroupReady. Remaining NULL-group non-archived rows (Sem 1, 4, 5 pending) keep **SchemaHardeningReady = FALSE**.

**Prompt 3J (DDL NOT NULL / UNIQUE) is NOT authorized by this prompt.**

---

## 9. Explicit non-goals

- No `GroupId NOT NULL`
- No `UNIQUE(TenantId, GroupId, Number)`
- No automatic Group assignment
- No automatic duplicate deletion / merge
- No TG / TimetableSection / CAP / Publish gate changes
- No second migration engine (reuses finalization audit + disposition journals)
