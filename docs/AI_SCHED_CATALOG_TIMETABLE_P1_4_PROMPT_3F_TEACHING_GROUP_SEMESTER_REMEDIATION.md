# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3F  
# Controlled Teaching Group Semester Remediation

**Date:** 2026-08-22  
**Type:** Data remediation — `TeachingGroup.SemesterId` only  
**Approved TG IDs:** **1**, **2**  
**Legacy → Target:** Semester **3** → Semester **11**  
**Schema hardening:** NOT applied  

---

## 1. Purpose

Resolve the two residual Teaching Groups deferred by Prompt 3C/3D/3E:

| TG Id | Code | Group | Legacy Sem | Candidate |
| ---: | --- | ---: | ---: | ---: |
| 1 | TG-PROOF-01 | 2 (CA) | 3 | **11** |
| 2 | TG-PROOF-02 | 2 (CA) | 3 | **11** |

No generic TG Semester reassignment API.

---

## 2. Mutation scope

| Allowed | Forbidden |
| --- | --- |
| `TeachingGroup.SemesterId` (approved IDs only) | TeachingGroupSection rows |
| TimetableEntry.SemesterId denorm align when `TeachingGroupId` matches | Membership / StudentSection |
| Projector sync (sole TimetableSection writer) | Direct TimetableSection writes |
| | Attendance / SA general migration |
| | Semester 3 delete / NOT NULL / UNIQUE |

---

## 3. Validation gates (fail closed)

1. Live TG IDs must be exactly `{1,2}` on legacy Sem 3 (no extras).
2. Target Sem 11: `GroupId == TG.GroupId`, `CourseId == TG.CourseId`, `TenantId` match, not NULL-group, Number=3, unique Group+Number.
3. Every linked Section: `CourseId/GroupId/SemesterId == target`.
4. SubjectAllocation: Course/Group match and `SemesterId == 11` (no SA mutate in 3F).
5. TimetableEntry: Course/Group match; SemesterId ∈ {3,11}.
6. Projection set equality after sync.
7. Batch atomic — any MANUAL_REVIEW/BLOCKED → zero TG writes.

---

## 4. API

| Endpoint | Mode |
| --- | --- |
| `GET /api/semester/teaching-group-remediation-preview` | Read-only |
| `POST /api/semester/teaching-group-remediation/execute` | Transactional |

Auth: `CanManageSemesters`. Runner: `--tg-remediate-preview` / `--tg-remediate-execute`.

---

## 5. Explicit non-mutations

- TeachingGroupSection: **unchanged**
- Membership: **unchanged**
- Attendance / StudentSection: **unchanged**
- TimetableSection: **not written directly** (projector only)
- Legacy Semester 3: **retained**

---

## 6. Live evidence (2026-08-22)

### Precondition revalidation
- TG 1 `TG-PROOF-01`, GroupId=2, Sem=3, SA consistent with Sem **11**, TT=0, TGS=1 → Section **5** still Sem **3**
- TG 2 `TG-PROOF-02`, GroupId=2, Sem=3, SA consistent, TT=0, TGS=0 → **READY**
- Target Sem 11: GroupId=2, CourseId=1, Number=3 — ownership validated
- No unexpected extra TGs on legacy Sem 3

### Execution
| Metric | Value |
| --- | --- |
| Preview ExecutionSafe | **false** |
| TG1 | **MANUAL_REVIEW_REQUIRED** (Section 5 Sem=3 ≠ target 11) |
| TG2 | READY (held by batch atomicity) |
| Execute | **Not applied** — fail closed; zero TG mutations |
| TeachingGroupSection | **unchanged** (no detach/move) |
| Membership | **unchanged** |
| Attendance / StudentSection | **unchanged** |
| Schema hardening | **NOT ready** |

### Why not Completed
Prompt rule: linked `Section.SemesterId` must equal target Semester. Section 5 (CA) still references legacy Sem 3 (Sections were out of scope in Prompt 3C). Do not auto-move Section. Entire batch aborts.

---

## 7. Remaining blockers (at first attempt)

1. Remap CA Section(s) currently on Sem 3 (at least Section Id=5) to Sem 11 under a **separate approved Section remediation prompt**, then re-run 3F.
2. Remaining NULL-group Semesters 1/3/4/5; Sem 3 still has other Section/Subject refs.
3. Wildcard / NOT NULL / UNIQUE still deferred.

---

## 8. Re-execution (post–Prompt 3G)

After Prompt **3G** remapped CA Sections **5, 13, 14, 15** to Sem **11**, Prompt 3F was **re-executed** successfully:

| Metric | Value |
| --- | --- |
| Preview ExecutionSafe | **true** (Section 5 compatible) |
| Execute | **Completed**, ChangedCount=**2**, TG 1 & 2 → Sem **11** |
| Idempotent re-run | **AlreadyComplete**, ChangedCount=**0** |
| TG residuals | **2 → 0** |
| Integrity | IsHealthy=**true**; TEACHING_GROUP PASS |

Full evidence: [`AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3F_REEXECUTION.md`](./AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3F_REEXECUTION.md).

---

## 9. STOP

Do **not** start Sem 1 mapping, Sem 4/5 duplicates, wildcard removal, NOT NULL, UNIQUE, or Semester deletion without Chief Architect approval.
