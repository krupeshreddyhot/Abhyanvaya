# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 2B  
# Legacy Semester Mapping, Split Plan & Fail-Closed Migration Design

**Date:** 2026-08-22  
**Type:** Read-only audit / mapping worksheet  
**Mutations:** NONE  
**Final status: PASS**

---

## 1. Current data audit (local tenant)

| Metric | Count |
| --- | --- |
| Total Semesters | 6 |
| NULL GroupId (legacy) | 5 |
| Group-specific | 1 (Id=9 → Group 2 CA) |
| Courses with multiple Groups | 1 (B.Com → Finance + COMPUTER APPLICATIONS) |
| Courses with one / zero Groups | 0 / 0 |
| Duplicate legacy Course+Number keys | 1 (`CourseId=1`, `Number=4` → Semesters 4 & 5) |

---

## 2. Classification rules

| Code | Rule |
| --- | --- |
| ALREADY_GROUP_SPECIFIC | `GroupId` set |
| INVALID_DATA | Course missing/deleted |
| ORPHAN_NO_GROUP | Course has 0 active Groups |
| DETERMINISTIC_SINGLE_GROUP | `GroupId` null + exactly 1 active Group + no legacy Number duplicate |
| AMBIGUOUS_MULTI_GROUP | `GroupId` null + multiple Groups, or legacy Number duplicate |

**Never:** first Group, name match, student majority as auto-assign, UI visibility.

Student Group distribution is **evidence only** (drives `SPLIT_REQUIRED` vs `MANUAL_MAPPING_REQUIRED`).

---

## 3. Mapping worksheet (local DB — not fabricated)

| Legacy Sem | Course | # | Name | GroupId | Candidate Groups | Classification | Action | Students (by Group) | Att | SA | TT | Subj | TG | Dup # |
| --- | --- | ---: | --- | --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| 1 | B.Com | 1 | Semester I | NULL | CA / Finance | AMBIGUOUS_MULTI_GROUP | MANUAL_MAPPING_REQUIRED | 0 | 0 | 0 | 0 | 1 | 0 | N |
| 2 | B.Com | 2 | Semester II | NULL | CA / Finance | AMBIGUOUS_MULTI_GROUP | MANUAL_MAPPING_REQUIRED | 0 | 0 | 0 | 0 | 0 | 0 | N |
| 3 | B.Com | 3 | Semester III | NULL | CA / Finance | AMBIGUOUS_MULTI_GROUP | **SPLIT_REQUIRED** | 296 (Finance 60 / CA 236) | 67 | 1 | 1 | 17 | 2 | N |
| 4 | B.Com | 4 | Semester VI | NULL | CA / Finance | AMBIGUOUS_MULTI_GROUP | MANUAL_MAPPING_REQUIRED | 0 | 0 | 0 | 0 | 0 | 0 | **Y** |
| 5 | B.Com | 4 | Semester V | NULL | CA / Finance | AMBIGUOUS_MULTI_GROUP | MANUAL_MAPPING_REQUIRED | 0 | 0 | 0 | 0 | 0 | 0 | **Y** |
| 9 | B.Com | 4 | Semester IV | 2 (CA) | CA / Finance | ALREADY_GROUP_SPECIFIC | ALREADY_GROUP_SPECIFIC | 4 (CA) | 0 | 0 | 0 | 0 | 0 | N |

Conceptual split for Semesters 1–3 (Architect approval required before execution):

```
B.Com → Finance → Semester N
B.Com → COMPUTER APPLICATIONS → Semester N
```

**Not created by this prompt.**

---

## 4. Downstream impact

Primary consumers counted per SemesterId:

- `Student.SemesterId`
- `AttendanceSession.SemesterId` (no soft-delete column in DB)
- `SchedulingSubjectAllocation.SemesterId`
- `SchedulingTimetableEntry.SemesterId`
- `Subject.SemesterId`
- `Sections.SemesterId` (EF)
- `SchedulingTeachingGroup.SemesterId`

No references rewritten.

---

## 5. Ambiguous records

All 5 NULL-group rows on B.Com (multi-Group Course). Highest risk: **Semester III (Id=3)** — students split 60/236 across Groups + Attendance/SA/TT/Subject/TG refs.

---

## 6. Deterministic records

**None** in local DB (no single-Group Course with legacy Semesters).

---

## 7. Duplicate records

`Number=4` appears twice as legacy (Ids 4 and 5: VI and V) while Id 9 is already Group-specific Semester IV with Number=4 → uniqueness cleanup required before any unique index.

---

## 8. Migration blockers

- `HasMigrationBlockers = true` (manual + split actions present)
- No MAP_SINGLE_GROUP candidates locally
- Duplicate Number=4 must be resolved manually before split

---

## 9. Recommended manual decisions

1. Approve split worksheet for Semesters 1, 2, 3 (and decide fate of 4/5 duplicates).
2. For Semester III: create Finance III + CA III; remap Students by `Student.GroupId`; then Attendance/SA/TT/Subject/TG.
3. Do not assign Semesters 4/5 until Number/Name uniqueness is cleaned.
4. Leave Id=9 untouched.

---

## 10. No-mutation verification

- Service uses `AsNoTracking` only; no `SaveChanges` / Add / Update / soft-delete.
- API: `GET /api/semester/legacy-migration-audit` only (CanManageSemesters).
- No Migrate / Split / Assign endpoints or UI buttons.
- Schema `GroupId` remains nullable; AcademicTree null wildcard unchanged.

---

## 11. Tests

| Suite | Result |
| --- | --- |
| Classifier + P1-4/P1-3/TG/CAP filters | **63 passed** |
| API build | **PASS** |
| UI build | **PASS** |

---

## 12. Architecture guards

No auto Group inference, Student/Attendance/SA/TT remap, NOT NULL, TG/CAP/SectionId changes.

---

## 13. Next recommended step

**P1-4 Prompt 3** — Approved split execution + Student semester remapping (fail closed), only after Architect signs the worksheet for each MANUAL/SPLIT row.

---

## Implementation artifacts

- `ILegacySemesterMigrationAuditService` / `LegacySemesterMigrationAuditService`
- `LegacySemesterMigrationClassifier`
- DTOs under `Application/DTOs/Academic`
- `GET api/semester/legacy-migration-audit`
