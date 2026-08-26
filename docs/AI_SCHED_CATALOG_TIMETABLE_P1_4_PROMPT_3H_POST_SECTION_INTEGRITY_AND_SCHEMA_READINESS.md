# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3H  
# Post-Section-Remediation Integrity Audit & Schema-Hardening Readiness

> **Superseded for current readiness numbers by**  
> `docs/AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3H_POST_SECTION_INTEGRITY_AUDIT.md`  
> (re-audit after Prompt 3G / 3I / 3J; package folder `P1-4/3H1`).

**Date:** 2026-08-23 (original)  
**Type:** AUDIT AND READINESS CONTRACT ONLY — zero mutations, zero schema changes  
**PromptCode:** `P1-4-3H`  
**API:** `GET /api/semester/post-section-remediation-integrity-audit`  
**Runner:** `--prompt3h-audit`

See the superseding document for live post–Section/Subject remediation counts and hardening flags.


---

## 2. Prompt 3G verification (live)

| Check | Result |
| --- | --- |
| Journal evidence | Found (`P1-4-3G` / `SECTION_SEMESTER_REMEDIATION`) |
| Journaled Section IDs | **5, 13, 14, 15** |
| On target Sem 11 | **5, 13, 14, 15** |
| CA still on Sem 3 | **none** |
| Finance residual on Sem 3 | **9, 10, 11, 12** (out of 3G scope — intentional) |
| Contract satisfied | **true** |

---

## 3. Current Semester inventory

| Metric | Count |
| ---: | ---: |
| Total Semesters | 8 |
| NULL GroupId | **5** (`1,2,3,4,5`) |
| Group-specific | 3 |
| Course/Group mismatch | 0 |
| Duplicate Group+Number | 0 |
| Historical retained (3H class) | 1 (Sem 2) |

---

## 4. Student integrity

| Metric | Value |
| --- | --- |
| Checked | 300 |
| Healthy | 300 |
| Legacy NULL-group | 0 |
| Incompatible | 0 |

---

## 5. Attendance integrity

| Metric | Value |
| --- | --- |
| Checked | 67 |
| Legacy NULL-group | 0 |
| Incompatible | 0 |

---

## 6. Subject integrity

| Metric | Value |
| --- | --- |
| Checked | 18 |
| Legacy NULL-group | **18** |
| Incompatible | 0 |

All catalog Subjects still reference NULL-group Semesters — major NOT NULL blocker.

---

## 7. Section integrity

| Metric | Value |
| --- | --- |
| Checked | 8 |
| Healthy (Group-specific Sem) | 4 (CA on Sem 11) |
| Legacy NULL-group | **4** (Finance 9–12 on Sem 3) |
| Incompatible | 0 |

---

## 8. SubjectAllocation integrity

| Metric | Value |
| --- | --- |
| Checked | 1 |
| Legacy / Incompatible | 0 / 0 |

---

## 9. TimetableEntry integrity

| Metric | Value |
| --- | --- |
| Checked | 1 |
| Legacy / Incompatible | 0 / 0 |

---

## 10. TeachingGroup integrity (classify-only)

| Metric | Value |
| --- | --- |
| Checked | 2 |
| On Group-specific Sem | **2** |
| Legacy NULL-group | **0** |

Post–Prompt 3F: TG residuals cleared.

---

## 11. TeachingGroupSection integrity (classify-only)

| Metric | Value |
| --- | --- |
| Links | 1 |
| Compatible (TG Sem == Section Sem) | **1** |
| Incompatible | **0** |

---

## 12. TimetableSection ownership

| Metric | Value |
| --- | --- |
| Rows | 0 |
| Projector-owned confirmed | true |
| Direct writer in 3H | **absent** |

---

## 13. Legacy Semester classification

| Sem | Classification | Notes |
| ---: | --- | --- |
| 1 | `BLOCKED_BY_REFERENCE` | Operational refs remain |
| 2 | `RETAIN_HISTORICAL` | Zero operational refs |
| 3 | `BLOCKED_BY_REFERENCE` | Section refs=4 (+ Subject refs) |
| 4 | `DUPLICATE_REVIEW` | Prompt 3A duplicate |
| 5 | `DUPLICATE_REVIEW` | Prompt 3A duplicate |

---

## 14. Wildcard dependency inventory

**14** catalogued sites (Prompt 3D reuse): AcademicTree `(GroupId == null \|\| …)`, `filterSemestersForScope`, Semesters UI “Legacy / Course-wide”, attendance/subject cascades, etc.  
**Not removed** in this prompt.

---

## 15. NOT NULL readiness

**NOT_NULL_READY = false (NOT READY)**

Blockers:
1. 5 NULL-group Semester rows remain `[1,2,3,4,5]`
2. Subject legacy NULL-group refs = 18
3. Section legacy NULL-group refs = 4 (Finance)
4. 14 wildcard dependency sites
5. 4 legacy Semesters still BLOCKED/DUPLICATE/OTHER

Historical preservation: deleting NULL rows solely to apply NOT NULL is **forbidden**. Requires Architect-approved design (archive table / `IsHistorical` + filtered unique / explicit MAP+RETAIN clearing all operational refs).

---

## 16. UNIQUE readiness

**UNIQUE_READY = false (NOT READY)**

- No Group-specific duplicate keys today (good).
- Still blocked while NULL-group rows remain without an approved UNIQUE-with-NULL preservation design.

---

## 17. Exact blockers

See live `ExactBlockers` list (NULL rows, Subject refs, Finance Sections, wildcards, dispositions, UNIQUE preservation).

---

## 18. Recommended next architectural step

1. **Finance Section remediation** (Sem 3 → Sem **10**) — mirror of Prompt 3G for Group 1.  
2. **Subject catalog Semester remediation** for remaining NULL-group Subject refs (explicit Architect prompt).  
3. Complete dispositions for Semesters **1 / 4 / 5** (and confirm Sem **2** RETAIN).  
4. **Wildcard deprecation prompt** (AcademicTree + UI filters).  
5. Only then authorize schema-hardening (`NOT NULL` / filtered `UNIQUE`).

**Schema-hardening prompt safe to begin:** **NO**

---

## STOP

Do **not** implement Prompt 3I or any `NOT NULL` / `UNIQUE` migration from this prompt.
