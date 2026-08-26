# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3G  
# Controlled Section Semester Remediation

**Date:** 2026-08-22  
**Type:** Data remediation — `Section.SemesterId` only  
**Legacy → Target:** Semester **3** → Semester **11** (CA / Group 2)  
**Required known blocker:** Section **5**  
**Schema hardening:** NOT applied  
**Prompt 3F re-execution:** NOT performed in this prompt  

---

## 1. Purpose

Unblock P1-4 Prompt 3F Teaching Group Semester remediation by remapping approved CA Sections that still reference legacy NULL-group Semester **3** onto the Architect-approved CA Semester **11**.

Teaching Group records are **not** mutated here. After Section remediation succeeds, Prompt 3F may be re-run separately under Chief Architect control.

---

## 2. Architecture

| Layer | Owner |
| --- | --- |
| Semester ownership SSOT | `Semester.GroupId` (Group authoritative) |
| Course denorm on Semester | `Semester.CourseId` ← `Group.CourseId` |
| Catalog Department SSOT | `Course.DepartmentId` |
| Section academic path | Course → Group → Semester |
| Mutation in 3G | Approved `Section.SemesterId` only |

Frozen boundaries preserved: Teaching Group TG.4A–TG.6, CAP, ConflictEngine, Publish Gate, TimetableSection projector ownership.

---

## 3. Scope

| Allowed | Forbidden |
| --- | --- |
| `Section.SemesterId` for approved CA Sem-3 Sections | TeachingGroup / Membership / TeachingGroupSection writes |
| Disposition journal row (`SECTION_SEMESTER_REMEDIATION`) | SubjectAllocation / TimetableEntry / TimetableSection |
| | Attendance / StudentSection / Student |
| | Semester ownership / GroupId assignment |
| | Legacy Semester delete / NOT NULL / UNIQUE |
| | Auto re-run of Prompt 3F |

Approved set = live Sections on Sem **3** whose `CourseId`/`GroupId` match target Sem **11** (CA). Finance Sem-3 Sections are reported **BLOCKED** (out of scope), not remapped to Sem 11.

---

## 4. Eligibility (fail closed)

A Section is remediable only when all hold:

1. Tenant matches ambient tenant  
2. Currently on legacy Sem **3**  
3. Target Sem **11** exists, not deleted, Number=3, non-NULL GroupId  
4. Section Course/Group match target Course/Group  
5. Explicitly in approved CA set  
6. Lifecycle Status not Archived/Merged/Split  
7. No SectionCode collision under target Sem 11 (same AY/Course/Group)  
8. No cross-tenant reference  

Any approved-set MANUAL_REVIEW/BLOCKED → **entire batch abort**, zero Section writes.

---

## 5. API

| Endpoint | Mode |
| --- | --- |
| `GET /api/semester/section-semester-remediation-preview` | Read-only |
| `POST /api/semester/section-semester-remediation/execute` | Transactional |

Auth: `CanManageSemesters`.  
Runner: `--section-remediate-preview` / `--section-remediate-execute`.

---

## 6. Transaction / idempotency / concurrency

- `ExecuteInTransactionAsync` — atomic batch  
- Re-validate inside transaction before write  
- First success: `Completed`, ChangedCount = N  
- Second run: `AlreadyComplete`, ChangedCount = 0, no extra SaveChanges work beyond no-op path  
- `ConcurrencyExceptionHelper.SaveChangesAsync` — concurrent admin change → abort/rollback  

---

## 7. Audit

Journal: `LegacySemesterDispositionJournal`

| Field | Value |
| --- | --- |
| DispositionCode | `SECTION_SEMESTER_REMEDIATION` |
| PromptCode | `P1-4-3G` |
| SemesterId | 11 |
| Evidence | `SectionIds=[...]; legacy=3; actor=...` |
| SemesterRowMutated | false |

---

## 8. Teaching Group relationship handling

Example: TG1 → Section 5 → Sem 3  

Prompt 3G may change: `Section 5.SemesterId: 3 → 11`  

Prompt 3G must **not** change: `TeachingGroup.SemesterId`, membership, or TeachingGroupSection links.

Post-exec immutability checks assert TG SemesterIds and TGS link sets unchanged.

---

## 9. Live-data results (2026-08-22)

### Preview
| Metric | Value |
| --- | --- |
| ExecutionSafe | **true** |
| Legacy / Target | 3 → 11 |
| Target Course / Group | 1 / 2 (CA) |
| Approved Sections | **5, 13, 14, 15** |
| Eligible | 4 READY |
| Blocked | 4 Finance Sem-3 (9–12) — out of scope |
| Section 5 TGs | TeachingGroup **1** (link unchanged) |

### Execution
| Metric | Value |
| --- | --- |
| Status | **Completed** |
| ChangedCount | **4** (5,13,14,15 → Sem 11) |
| TeachingGroupsUnchanged | **true** (still Sem 3) |
| TeachingGroupSectionsUnchanged | **true** |
| Finance Sections 9–12 | still Sem 3 (intentional residual) |

### Idempotent re-run
| Metric | Value |
| --- | --- |
| Status | **AlreadyComplete** |
| ChangedCount | **0** |

### Post-check (TG preview only — 3F NOT executed)
TG 1 & 2 preview: **ExecutionSafe=true**, Section 5 `SemesterId=11` compatible. Chief Architect may re-run Prompt 3F separately.

---

## 10. Residual risks

1. Finance Sem-3 Sections **9–12** remain on legacy Sem 3 until a separate approved remediation (target would be Sem **10**, not 11).  
2. Prompt 3F still required to move TG 1/2 `SemesterId` 3→11.  
3. Legacy Sem 3 row retained; NOT NULL/UNIQUE hardening deferred.  
4. Pre-existing Prompt 2A guard `No_NOT_NULL_Migration_Introduced_In_This_Prompt` fails on Prompt **3E** journal migration filename pattern — unrelated to 3G (3G adds **no** EF migration).

---

## 11. STOP

Do **not** auto-execute Prompt 3F.  
Do **not** harden Semester constraints.  
Do **not** delete legacy Semesters.  
Do **not** mutate Teaching Groups in this prompt.
