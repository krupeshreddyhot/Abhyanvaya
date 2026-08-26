# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3H  
# Post-Section-Remediation Integrity Audit & Hardening Readiness

**Date:** 2026-08-23  
**Type:** AUDIT AND READINESS ONLY — zero mutations, zero schema changes  
**PromptCode:** `P1-4-3H`  
**API:** `GET /api/semester/post-section-remediation-integrity-audit`  
(alias: `GET /api/semester/post-section-integrity-schema-readiness`)  
**Runner:** `--prompt3h-audit`

---

## 1. Executive summary (live re-audit after 3G / 3I / 3J)

| Gate | Verdict |
| --- | --- |
| Prompt 3G contract | **Verified** |
| Aggregate IsHealthy (operational) | **true** |
| Section legacy on Sem 3 | **0** |
| TeachingGroup residuals on legacy | **0** |
| `CanMakeGroupIdNotNull` | **false** |
| `CanAddGroupSemesterUniqueConstraint` | **false** |
| `CanRemoveLegacyWildcardSemantics` | **false** |
| Schema-hardening safe to begin? | **NO** |

**STATUS: CONDITIONAL PASS** — audit complete; operational Section/TG remediations verified; schema hardening **blocked**.

---

## 2. Prompt 3G verification

| Check | Result |
| --- | --- |
| Journal evidence | Found (`P1-4-3G` / `SECTION_SEMESTER_REMEDIATION`) |
| Journaled Section IDs | **5, 13, 14, 15** |
| On target Sem 11 | **5, 13, 14, 15** |
| CA still on Sem 3 | **none** |
| Finance residual on Sem 3 | **none** (cleared by Prompt 3I → Sem 10) |
| Contract satisfied | **true** |

---

## 3. Before / after (Section Sem 3)

| Scope | Pre-3G | Post-3G/3I (live) |
| --- | ---: | ---: |
| CA Sections on Sem 3 | 4 (5,13,14,15) | **0** (on Sem 11) |
| Finance Sections on Sem 3 | 4 (9–12) | **0** (on Sem 10) |
| Sections on Sem 3 total | 8 | **0** |

---

## 4. Semester integrity

| Metric | Count |
| ---: | ---: |
| Total Semesters | 8 |
| NULL GroupId | **5** (`1,2,3,4,5`) |
| Group-specific | 3 (10 Finance, 11 CA, + prior) |
| Course/Group mismatch | 0 |
| Duplicate Group+Number | 0 |

---

## 5. Student integrity

| Metric | Value |
| --- | --- |
| Checked | 300 |
| Healthy | 300 |
| Legacy NULL-group | 0 |
| Incompatible | 0 |

---

## 6. Section integrity

| Metric | Value |
| --- | --- |
| Checked | 8 |
| Healthy | **8** |
| Legacy NULL-group | **0** |
| Incompatible | **0** |

Former 3F blocker Section **5** is on Sem **11** and TG-compatible.

---

## 7. Attendance / SubjectAllocation / TimetableEntry

| Entity | Checked | Legacy NULL | Incompatible |
| --- | ---: | ---: | ---: |
| Attendance | 67 | 0 | 0 |
| SubjectAllocation | 1 | 0 | 0 |
| TimetableEntry | 1 | 0 | 0 |

---

## 8. Subject catalog integrity

| Metric | Value |
| --- | --- |
| Checked | 18 |
| Legacy NULL-group | **1** (historical retain — Subject on Sem 1) |
| Incompatible | 0 |

---

## 9. Teaching Group boundary (classify-only — **no mutation**)

| Metric | Value |
| --- | --- |
| TG checked | 2 |
| On Group-specific Sem | **2** |
| Legacy NULL-group | **0** |
| TGS links | 1 |
| TGS compatible | **1** |
| TimetableSection rows | 0 (projector-owned; not written) |

---

## 10. Legacy Semester classification

| Sem | Classification | Blocks hardening | Notes |
| ---: | --- | --- | --- |
| 1 | `MANUAL_MAPPING_REQUIRED` | yes | 1 Subject operational ref |
| 2 | `RETAIN_HISTORICAL` | yes | Zero ops; historical retain |
| 3 | `RETAIN_HISTORICAL` | yes | Zero ops post Section/Subject remaps |
| 4 | `DUPLICATE_REVIEW` | yes | Prompt 3A duplicate |
| 5 | `DUPLICATE_REVIEW` | yes | Prompt 3A duplicate |

Allowed codes only: `RETAIN_HISTORICAL`, `MANUAL_MAPPING_REQUIRED`, `DUPLICATE_REVIEW`, `BLOCKED_BY_TEACHING_GROUP_REFERENCE`, `READY_FOR_RETIREMENT`, `READY_FOR_GROUP_ASSIGNMENT`.

---

## 11. Wildcard dependencies

**14** catalogued sites (Prompt 3D reuse): AcademicTree `(GroupId == null \|\| …)`, `filterSemestersForScope`, Semesters UI “Legacy / Course-wide”, attendance/subject cascades, etc.

Classified as `ACTIVE_RUNTIME_DEPENDENCY` / `REQUIRES_FOLLOWUP` / `LEGACY_READ_ONLY_COMPATIBILITY` / `SAFE_TO_REMOVE` — **not removed** in this prompt.

`CanRemoveLegacyWildcardSemantics = false`.

---

## 12. Hardening readiness

| Flag | Value |
| --- | --- |
| `CanMakeGroupIdNotNull` | **false** |
| `CanAddGroupSemesterUniqueConstraint` | **false** |
| `CanRemoveLegacyWildcardSemantics` | **false** |

Blockers: 5 NULL-group Semester rows; 1 Subject legacy ref; 14 wildcards; legacy dispositions still blocking.

---

## 13. Unresolved blockers / recommended next step

1. Architect disposition for Semesters **1 / 2 / 3 / 4 / 5** (RETAIN vs retire vs duplicate merge).  
2. Resolve remaining historical Subject on Sem 1 (or confirm permanent RETAIN).  
3. Wildcard deprecation prompt (AcademicTree + UI filters).  
4. Only then authorize schema hardening (`NOT NULL` / filtered `UNIQUE`).

**Do not start Prompt 3I from this prompt** (Finance Section remediation already completed earlier). Next Architect-authorized increment should address remaining legacy disposition / wildcards — **not** schema DDL.

---

## STOP

Do **not**:

- make `Semester.GroupId` NOT NULL  
- add UNIQUE constraint  
- delete / assign remaining NULL-group Semesters  
- modify Teaching Groups / TeachingGroupSections / TimetableSections  
- remove wildcard behavior  
- modify CAP / ConflictEngine / Publish  

This prompt is strictly **audit + readiness**.
