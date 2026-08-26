# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3F Re-execution  
# Controlled Teaching Group Semester Remediation (post–Prompt 3G)

**Date:** 2026-08-22  
**Type:** Re-execution of existing Prompt 3F workflow (no new migration mechanism)  
**Approved TG IDs:** **1**, **2**  
**Legacy → Target:** Semester **3** → Semester **11**  
**Schema hardening:** NOT applied  
**Prompt 3G dependency:** Section Semester remediation completed first  

---

## 1. Why the previous 3F execution aborted

First live 3F attempt (before Prompt 3G):

| Check | Result |
| --- | --- |
| Approved TGs | `{1, 2}` on Sem 3 |
| Target | Sem 11 (CA / Group 2 / Course 1 / Number 3) |
| TG1 → Section 5 | `Section.SemesterId = 3` ≠ target **11** |
| Batch rule | Fail-closed — entire transaction aborted |
| ChangedCount | **0** |
| RolledBack | **true** |

This was correct behavior. Section ownership was out of scope for 3F; Prompt **3G** remapped approved CA Sections (including Section **5**) from Sem 3 → Sem 11 without mutating Teaching Groups.

---

## 2. What Prompt 3G changed (prerequisite)

| Field | Before 3G | After 3G |
| --- | --- | --- |
| Section 5 `SemesterId` | 3 | **11** |
| Sections 13, 14, 15 | 3 | **11** |
| TeachingGroup 1/2 `SemesterId` | 3 | 3 (unchanged) |
| TeachingGroupSection links | unchanged | unchanged |

Finance Sections 9–12 remain on Sem 3 (out of 3G/3F CA scope).

---

## 3. Preview before re-execution

| Metric | Value |
| --- | --- |
| ExecutionStatus | NotExecuted |
| IsReadOnly | true |
| ExecutionSafe | **true** |
| ApprovedTeachingGroupIds | `[1, 2]` |
| Legacy / Target | 3 → 11 |
| TG1 | READY — Section 5 Sem=11 compatible |
| TG2 | READY — zero sections |
| ManualReview / Blocked | 0 / 0 |
| SubjectAllocation | consistent with Sem 11 |
| TimetableEntryCount | 0 for both |

---

## 4. Transaction result (first re-execution)

| Metric | Value |
| --- | --- |
| ExecutionStatus | **Completed** |
| RolledBack | false |
| TransactionCommitted | true |
| ChangedCount | **2** |
| AffectedTeachingGroupIds | `[1, 2]` |
| OldSemesterIds | `[3]` |
| NewSemesterIds | `[11]` |
| PostTgResiduals | **0** |
| PostHealthy | **true** |
| ConcurrencyResult | None |

### Records changed
- `TeachingGroup` Id=1: `SemesterId` 3 → **11**
- `TeachingGroup` Id=2: `SemesterId` 3 → **11**
- TeachingGroupSection / Membership / SA / Attendance / StudentSection: **not mutated**
- TimetableSection: not written directly (projector path only; TT count was 0)
- Legacy Semester 3 row: **retained / unmodified**

---

## 5. Idempotency (second execution)

| Metric | Value |
| --- | --- |
| ExecutionStatus | **AlreadyComplete** |
| ChangedCount | **0** |
| AlreadyCompleteCount | **2** |
| AffectedTeachingGroupIds | `[]` |
| PostTgResiduals | 0 |

No duplicate records; TGs not moved again.

---

## 6. Post-migration integrity audit

| Check | Result |
| --- | --- |
| IsHealthy | **true** |
| Critical / Errors | 0 / 0 |
| TEACHING_GROUP | **PASS** (0 violations) |
| Warnings | 5 × `LEGACY_COURSE_WIDE_SEMESTER` (Semesters 1, 2, 3, 4, 5) |

TG residual warning on legacy Sem 3: **cleared** (2 → 0).

---

## 7. Remaining warnings / residuals

Expected and **not** in scope for this re-execution:

1. NULL-group Semesters **1, 2, 3, 4, 5** (disposition / hardening deferred)
2. Sem 3 still has non-TG downstream refs (e.g. Finance Sections 9–12)
3. Semesters 4/5 `DUPLICATE_REVIEW`
4. NOT NULL / UNIQUE Semester hardening — Chief Architect approval required
5. No automatic Semester 1/4/5 disposition or AcademicTree wildcard removal

---

## 8. Architectural boundaries (unchanged)

- Approved set remains `{1, 2}` only — no expansion
- Fail-closed batch atomicity retained
- CAP / ConflictEngine / Publish Gate untouched
- Projector remains sole TimetableSection writer
- Course / Department / Program / Semester ownership untouched

---

## 9. STOP

Do **not** begin:

- Semester NOT NULL hardening  
- Semester UNIQUE constraint  
- Legacy Semester deletion  
- Semester 1/4/5 disposition  
- AcademicTree wildcard removal  
- Additional Teaching Group migration  
- Schema migration  

Those require separate Chief Architect approval.
