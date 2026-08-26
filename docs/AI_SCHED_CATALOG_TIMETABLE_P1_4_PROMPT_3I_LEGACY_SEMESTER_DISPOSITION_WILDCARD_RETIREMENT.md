# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3I  
# Legacy Semester Disposition & Wildcard Dependency Retirement

**Date:** 2026-08-23  
**Architect package folder:** `P1-4/3I1`  
**Implementation PromptCode:** `P1-4-3L`  
*(Finance Section remediation already owns journal PromptCode `P1-4-3I`; this disposition/wildcard increment uses `P1-4-3L` to avoid collision.)*

**Type:** Controlled disposition journaling + **operational wildcard retirement**  
**APIs:**  
- `GET /api/semester/legacy-wildcard-retirement-preview`  
- `POST /api/semester/legacy-wildcard-retirement/execute`  
*(Existing 3D/3E preview/execute remain authoritative for RETAIN_HISTORICAL journaling.)*

---

## 1. Naming note

| Letter | Meaning in this repo |
| --- | --- |
| **3I (Finance)** | `FinanceSectionSemesterRemediationService` — Sem 3 → Sem 10 |
| **3I (this Architect prompt) / package 3I1** | Legacy disposition + wildcard retirement — implemented as **`P1-4-3L`** |

---

## 2. What changed

### Operational wildcard retired

| Site | Before | After |
| --- | --- | --- |
| `AcademicTreeService` | `GroupId == null \|\| GroupId == g.Id` | **`GroupId == g.Id` only** |
| `filterSemestersForScope` | includes null-group | **Group-specific only** |
| `filterSemestersForCourseGroup` | includes null-group | **Group-specific only** |
| `resolveSemestersForCourseGroup` | null + course fallback | **Group-specific; empty if missing** |
| `StudentsPage` semester filter | null OR group | **group only** |
| `SubjectAllocationPage` label | `(all groups)` | historical label only |
| `SemestersPage` chip | `Legacy / Course-wide` | **`Legacy / Historical`** |

### Historical retention (unchanged rows)

NULL-group Semesters **1–5** remain in DB for audit.  
They are **excluded** from operational academic-tree / cascade / scheduling selectors.  
Admin Semester list still shows them as **Legacy / Historical**.

### Not done (STOP)

- No `Semester.GroupId` NOT NULL  
- No UNIQUE(TenantId, GroupId, Number)  
- No physical deletes  
- No guessed Group assignments  
- No TG / TimetableSection / CAP / Publish mutation  

---

## 3. Live legacy disposition (post 3G/3I-Finance/3J)

| Sem | Disposition | Notes |
| ---: | --- | --- |
| 1 | `MANUAL_MAPPING_REQUIRED` | 1 Subject historical ref |
| 2 | `RETAIN_HISTORICAL` | Zero operational refs |
| 3 | `RETAIN_HISTORICAL` | Zero operational refs post remaps |
| 4 | `DUPLICATE_REVIEW` | Prompt 3A duplicate |
| 5 | `DUPLICATE_REVIEW` | Prompt 3A duplicate |

Active Student / Attendance / Section / SA / TT / TG refs on NULL-group Semesters: **0**.

---

## 4. Wildcard dependency inventory

| Class | Count (approx) |
| --- | ---: |
| Found (3D catalog) | 14 |
| Operational resolution removed | AcademicTree, filterSemesters*, schedulingFormUtils, Students/Attendance/Subjects cascades |
| Remaining historical display | SemestersPage, MasterController `IsLegacyCourseWide`, SemesterController list |
| ElectiveGroups follow-up | `REQUIRES_FOLLOWUP` if still listing null-group rows |

`CanRemoveLegacyWildcardSemantics` (operational): **true** for tree/cascades.  
Schema NOT NULL still **false** while NULL rows + Subject historical remain.

---

## 5. Schema readiness

| Flag | Value |
| --- | --- |
| NOT NULL | **NOT READY** |
| UNIQUE | **NOT READY** |

Blockers: 5 NULL-group rows (historical/duplicate), 1 Subject historical ref, Architect disposition for duplicates 4/5.

---

## 6. Residual risks

1. Elective Groups UI may still surface historical Semesters — follow-up if observed.  
2. Schema hardening still requires Architect design for historical NULL preservation.  
3. Semesters 4/5 remain DUPLICATE_REVIEW.

---

## 7. Recommended next step

Architect-approved: (a) duplicate Sem 4/5 disposition, (b) Subject Sem 1 historical confirm, (c) schema-hardening design — **not** auto-started here.

**STOP.**
