# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3K-A  
# Historical Semester Disposition & Archive Architecture Discovery

**Date:** 2026-08-23  
**Architect package:** `P1-4/3KA`  
**Implementation PromptCode:** `P1-4-3KA`  
**Mode:** DISCOVERY + ARCHITECTURE CONTRACT ONLY  
**API:** `GET /api/semester/historical-disposition-audit` (read-only)  
**Auth:** `CanManageSemesters`

**Explicit non-goals of this prompt:** no production data mutation, no GroupId assignment, no TG/TimetableSection writes, no NOT NULL/UNIQUE DDL, no new POST disposition/archive endpoint.

---

## 1. Current-state architecture

### Frozen ownership

- Group owns operational Semester (`Semester.GroupId` authoritative).
- `Semester.CourseId` validated denorm of `Group.CourseId`.
- New Semesters require Group (`SemesterGroupOwnershipRules`).
- Programs optional via `EnablePrograms`.
- Course catalog SSOT: `Course.DepartmentId`; SA/TT Department denorms unchanged.
- TG / TimetableSection / CAP / ConflictEngine / Publish frozen.

### Existing historical pattern (REUSED — no competing lifecycle)

| Mechanism | Role |
| --- | --- |
| `Semester.IsHistoricalArchive` | Row-local selection gate (Prompt 3J-A / migration additive) |
| `LegacySemesterDispositionJournals` | Auditable disposition trail (single journal) |
| `OperationalSemesterRules` | Operational vs historical query helpers |
| `GET ...?includeHistorical` / `includeNullGroupLegacy` | Explicit discoverability without operational selection |
| Soft-delete `IsDeleted` | **Distinct** disposal — must not overload as historical |

**Finding:** A suitable archive pattern **already exists**. Prompt 3K-A does **not** introduce a second status enum column or archive table.

### Where NULL GroupId / historical semantics appear

| Site | Behavior |
| --- | --- |
| AcademicTree / `filterSemestersForScope` | Group-specific only; excludes NULL + archived |
| Student / SA / TT / TG / Attendance writes | Reject historical (`OperationalSemesterRules`) |
| SemestersPage | Chip “Legacy / Historical” for NULL GroupId |
| SemesterController GetAll | Default operational; opt-in historical/legacy flags |
| 3JA preview/execute | Controlled `HISTORICAL_ARCHIVE` (execute out of scope for 3K-A) |

---

## 2. Historical Semester classification model

| Code | Meaning | DB mapping (existing) |
| --- | --- | --- |
| `ACTIVE_OPERATIONAL` | Group-owned, usable for ops | `GroupId != null && !IsHistoricalArchive` |
| `HISTORICAL_RETAIN` | Valid historical meaning; not yet archived; not Group-converted | NULL GroupId, journal/retain semantics |
| `MANUAL_MAPPING_REQUIRED` | Group ownership unsafe to invent | e.g. Sem 1 + Subject historical |
| `DUPLICATE_REVIEW` | Ambiguous duplicate Number | e.g. Sem 4/5 |
| `BLOCKED_BY_REFERENCE` | Ops/TG/Section/SA/TT/Attendance/Student refs remain | ops total &gt; 0 |
| `ARCHIVE_ELIGIBLE` | Zero ops refs; safe for **explicit** archive decision | Candidate for `IsHistoricalArchive=true` later |
| `ARCHIVED` | Finalized historical | `IsHistoricalArchive=true` |

These codes are **audit classifications**, not a new persisted enum (unless a future Architect prompt promotes them).

---

## 3. Downstream dependency matrix

| Entity | Kind | Blocks archive? | Notes |
| --- | --- | :---: | --- |
| Student | operational | Yes | Remap before archive |
| AttendanceSession | operational | Yes | New sessions reject historical |
| Subject | historical / informational | No* | *May force MANUAL_MAPPING_REQUIRED |
| Section | operational | Yes | No TimetableSection direct writes |
| SubjectAllocation | operational | Yes | Course Department SSOT intact |
| TimetableEntry | operational | Yes | CAP frozen |
| TimetableSection | projector / frozen | Yes (via Section) | Identify only |
| TeachingGroup / TGS | operational | Yes | **Identify-only in 3K-A** |
| DispositionJournal | audit | No | Single journal |

Archive eligibility requires **all** operational refs cleared — **not** Student-only.

---

## 4. Archive eligibility rules

1. `OperationalRefTotal` (Student+Attendance+Section+SA+TT+TG) = 0.  
2. TG refs block until separate TG remediation.  
3. MANUAL_MAPPING_REQUIRED / DUPLICATE_REVIEW ⇒ never ARCHIVE_ELIGIBLE.  
4. Archive must **not** assign `GroupId`.  
5. Subject-only historical may remain HISTORICAL_RETAIN until Architect confirms archive-with-FK.  
6. Deletion is never archival.

---

## 5. Retain vs archive distinction

| | Retained historical | Archived |
| --- | --- | --- |
| Table | Remains in `Semester` | Remains in `Semester` |
| `IsHistoricalArchive` | `false` | `true` |
| `GroupId` | May stay NULL | May stay NULL |
| Operational selectors | Excluded (default GetAll / tree / cascades) | Excluded |
| Discoverability | `includeNullGroupLegacy` / admin list | `includeHistorical` / `WhereHistoricalArchive` |
| Mutation | None in 3K-A | Future explicit 3JA/3K-B only |

---

## 6. Tenant isolation

- All audit queries scoped by ambient `TenantId`.  
- Never infer tenant from Course/Group/Department/Program alone.  
- Cross-tenant relationships fail closed (EF filters + write-path rules elsewhere).

---

## 7. Proposed API contract (implemented read-only)

`GET /api/semester/historical-disposition-audit`

Returns `HistoricalSemesterDispositionAuditDto` including:

- Classification counts  
- `Items[]` → `HistoricalSemesterDispositionDto` (SemesterId, CourseId, GroupId, Number, Classification, IsOperational/IsHistorical/IsArchiveEligible, BlockingReasons, DownstreamReferenceSummary, RecommendedAction)  
- Dependency matrix, archive rules, retain-vs-archive notes, future execution contract, UI recommendations  
- `SaveChangesInvoked=false`

**No POST** in Prompt 3K-A. Existing 3JA execute remains the authorized mutation path when Architect permits.

---

## 8. Future execution contract (documented only)

- Explicit per-Semester disposition  
- Operator identity on journal  
- Transactional fail-closed batch  
- Concurrency protection  
- Idempotent AlreadyComplete  
- Rollback on abort  
- Post-integrity audit before schema hardening  

---

## 9. Database hardening prerequisites

NOT NULL / UNIQUE remain **deferred** until:

- Remaining NULL-group rows are ARCHIVED or otherwise Architect-approved excluded  
- MANUAL_MAPPING / DUPLICATE_REVIEW cleared  
- Soft-deleted NULL GroupId DBA scan complete  

---

## 10. UI recommendations (not implemented)

Operational / Historical retained / Archived / Manual-review / Blocked queues — archived/historical must never be accidental operational picks. SemestersPage already labels Legacy/Historical.

---

## 11. Risks & unresolved decisions

1. Sem1 Subject historical — Architect disposition still required.  
2. Sem4/5 duplicate business review — no auto-merge.  
3. Whether Subject FKs may remain on ARCHIVED rows long-term (3JA allows Subject without remapping).  
4. Filtered UNIQUE excluding historical — design owned by schema prompt after disposition.  

---

## 12. Explicitly deferred

- Archive/execute mutation (use 3JA when authorized)  
- Group assignment / merge / delete  
- TG / Section / Student / Attendance remaps  
- NOT NULL / UNIQUE migrations  
- UI redesign  

---

## Recommended next prompt

**Prompt 3K-B** (or Architect-approved re-run of **3JA execute**) — explicit `HISTORICAL_ARCHIVE` for `ARCHIVE_ELIGIBLE` only — then re-run **3J schema-hardening readiness**.

**STOP after 3K-A.**
