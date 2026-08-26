# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3K-B  
# Controlled Historical Semester Archival Execution

**Date:** 2026-08-23  
**Architect package:** `P1-4/3KB`  
**Implementation PromptCode:** `P1-4-3KB`  
**API:** `POST /api/semester/historical-disposition/execute`  
**Auth:** `CanManageSemesters`  
**Prerequisite:** Prompt 3K-A discovery/contract (`GET .../historical-disposition-audit`)

---

## 1. Approved scope

Archive **only** Semesters that the **server-side** 3K-A classifier re-evaluates as:

`ARCHIVE_ELIGIBLE`

Reuse:

- `Semester.IsHistoricalArchive`
- `LegacySemesterDispositionJournal` (PromptCode `P1-4-3KB`)

Do **not**:

- invent `GroupId`
- mutate Student / Attendance / Subject / Section / SA / TT / TG / TimetableSection
- introduce a second lifecycle or journal table
- implement NOT NULL / UNIQUE

---

## 2. Eligibility rules

| Step | Rule |
| --- | --- |
| 1 | Explicit `semesterIds` required (empty → abort; no archive-all) |
| 2 | `disposition` must be `HISTORICAL_ARCHIVE` |
| 3 | Tenant-scoped load; missing/cross-tenant → reject |
| 4 | Re-run 3K-A audit classification |
| 5 | Must be `ARCHIVE_ELIGIBLE` (already `IsHistoricalArchive` → AlreadyComplete) |
| 6 | Live ops refs (Student+Attendance+Section+SA+TT+TG) must be 0 |
| 7 | Then set `IsHistoricalArchive=true`; journal; `AssignedGroupId=null` |

Rejected unchanged:

- `MANUAL_MAPPING_REQUIRED` (e.g. Sem 1)
- `DUPLICATE_REVIEW` (e.g. Sem 4/5)
- `BLOCKED_BY_REFERENCE` (incl. TG)
- `HISTORICAL_RETAIN` / other non-eligible
- `ACTIVE_OPERATIONAL`

---

## 3. Transaction model

**ALL_OR_NOTHING** via `ExecuteInTransactionAsync`.

Two-pass execution inside the transaction:

1. **Classify** every requested Semester (tenant load + 3K-A reclassification + live ops check).  
   Zero mutations in this pass.
2. If any rejected/blocked → abort (`IsSuccessful=false`); no `SaveChanges`.  
3. Else mutate `IsHistoricalArchive` + journal rows, then `ConcurrencyExceptionHelper.SaveChangesAsync`.

Any failure after mutations → `DomainException` / concurrency → full rollback.  
No partial archival.

---

## 4. Idempotency

| Case | Result |
| --- | --- |
| First ARCHIVE_ELIGIBLE | Archived + journal |
| Already `IsHistoricalArchive` | AlreadyComplete; **zero** additional writes |
| Second identical request after success | AlreadyComplete; journal count unchanged |

---

## 5. Disposition journal

Existing `LegacySemesterDispositionJournal`:

- `DispositionCode = HISTORICAL_ARCHIVE`
- `PromptCode = P1-4-3KB`
- `SemesterRowMutated = true`
- `AssignedGroupId = null`
- Evidence includes correlation id, actor, noGroupGuess, noDownstreamMutation

---

## 6. Tenant isolation

All loads filter `TenantId == ambient`. Cross-tenant IDs fail closed and abort the batch.

---

## 7. Records archived / not archived (baseline expectation)

| Semesters | Outcome |
| --- | --- |
| ARCHIVE_ELIGIBLE (typically Sem 2 and/or 3 when ops cleared) | May archive when requested |
| Sem 1 MANUAL_MAPPING_REQUIRED | Rejected |
| Sem 4/5 DUPLICATE_REVIEW | Rejected |
| TG-blocked | Rejected; TG untouched |
| Already archived | AlreadyComplete |

Exact live IDs depend on current DB audit — server reclassification is authoritative.

---

## 8. Post-execution audit

Re-run `GET /api/semester/historical-disposition-audit`:

- Prior ARCHIVE_ELIGIBLE → `ARCHIVED`
- MANUAL / DUPLICATE unchanged
- No wildcard operational selection introduced
- `GroupId` still NULL where historical

---

## 9. Why schema hardening remains deferred

NULL-group Semesters may still exist (manual/duplicate/non-archived).  
`SchemaHardeningDeferred = true` on every 3K-B result.  
NOT NULL / UNIQUE require a later Architect-authorized prompt after full disposition readiness.

---

## 10. Remaining blockers

1. Sem1 Subject historical / MANUAL_MAPPING_REQUIRED  
2. Sem4/5 DUPLICATE_REVIEW  
3. Any residual BLOCKED_BY_REFERENCE  
4. Soft-deleted NULL GroupId DBA scan before ALTER  

---

## Request / response

```json
POST /api/semester/historical-disposition/execute
{
  "disposition": "HISTORICAL_ARCHIVE",
  "semesterIds": [2, 3],
  "reason": "Architect-approved 3K-B archival"
}
```

Response includes requested/archived/alreadyComplete/rejected counts and per-semester results.

**STOP after Prompt 3K-B.** Do not auto-start 3K-C or schema hardening.
