# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3K  
# Section Semester Remediation Verification (CA Sem 3 → Sem 11)

**Date:** 2026-08-23  
**Type:** Verification / readiness package — **no new mutation mechanism**  
**Capability owner:** Prompt **3G** (`SectionSemesterRemediationService`)  
**Legacy → Target:** Semester **3** → Semester **11** (Course 1 / Group 2 CA)  
**Known blocker addressed:** Section **5**  

---

## 1. Root cause addressed

Prompt 3F initially aborted because:

```
TeachingGroup 1 → TeachingGroupSection → Section 5 → SemesterId = 3
Target Semester = 11
```

Section Semester was incompatible with the TG target. Prompt 3G implemented controlled `Section.SemesterId` remediation only. This Prompt **3K** verifies that capability and live state without re-running TG remediation.

---

## 2. Implementation ownership (already delivered in 3G)

| Artifact | Location |
| --- | --- |
| Service | `SectionSemesterRemediationService` / `ISectionSemesterRemediationService` |
| DTOs | `SectionSemesterRemediationDtos.cs` |
| Preview API | `GET /api/semester/section-semester-remediation-preview` |
| Execute API | `POST /api/semester/section-semester-remediation/execute` |
| Auth | `CanManageSemesters` |
| Journal | `SECTION_SEMESTER_REMEDIATION` / PromptCode `P1-4-3G` |
| Runner | `--section-remediate-preview` / `--section-remediate-execute` |
| Tests / guards | `SectionSemesterRemediationServiceTests.cs` |
| Spec | `docs/AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3G_SECTION_SEMESTER_REMEDIATION.md` |

**This prompt remediates `Section.SemesterId` only.**  
**Teaching Group remediation remains a separate Prompt 3F operation** (already completed in a prior re-execution; not invoked here).

---

## 3. Live verification (2026-08-23)

### Section preview (`--section-remediate-preview`)

| Metric | Value |
| --- | --- |
| ExecutionSafe | true |
| Eligible | **0** |
| AlreadyComplete | **4** (`5, 13, 14, 15`) |
| All on Sem | **11** |
| Section 5 TGs | TG **1** (link unchanged; Section Sem=11) |
| Blocked / Manual | 0 / 0 |

### Section execute (idempotent re-run)

| Metric | Value |
| --- | --- |
| ExecutionStatus | `AlreadyComplete` |
| ChangedCount | **0** |
| AlreadyCompleteCount | **4** |
| RolledBack | false |
| TeachingGroupsUnchanged | true |
| TeachingGroupSectionsUnchanged | true |
| ConcurrencyResult | None |

### TG preview (read-only — **3F NOT executed**)

| Metric | Value |
| --- | --- |
| TG 1 / TG 2 | `ALREADY_COMPLETE` on Sem **11** |
| Section 5 compatibility | **Compatible** (Sem=11) |
| ExecutionSafe | true |

---

## 4. Architectural boundaries (preserved)

| Forbidden in this workflow |
| --- |
| TeachingGroup / TeachingGroupSection mutation |
| TimetableSection direct writes |
| Attendance / StudentSection / SA / TT mutation |
| Semester ownership / NOT NULL / UNIQUE |
| Auto re-run of Prompt 3F |
| Generic “fix all legacy Sections” |

---

## 5. STOP

Do **not** automatically:

- re-run Prompt 3F (already complete on live data)
- modify Teaching Groups / TeachingGroupSections
- harden Semester.GroupId NOT NULL
- add Semester unique constraints
- finalize remaining legacy Semesters
- remove AcademicTree wildcard behavior

---

## 6. Recommended next step

If Chief Architect requires further P1-4 work: legacy Semesters **1/2/4/5** disposition, wildcard deprecation, or schema-hardening readiness re-audit (Prompt 3H-style) — **not** duplicate Section remediation.
