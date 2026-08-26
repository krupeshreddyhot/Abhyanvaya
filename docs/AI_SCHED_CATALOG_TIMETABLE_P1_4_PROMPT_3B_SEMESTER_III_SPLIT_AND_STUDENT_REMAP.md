# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3B  
# Controlled Semester III Split & Student Semester Remapping

**Date:** 2026-08-22  
**Type:** Controlled transactional migration (Semester create + Student.SemesterId remap only)  
**Final status: PASS**  
**Stop condition:** Do **not** begin Prompt 3C (Attendance / Subject / Section / SA / Timetable / TG remapping).

---

## 1. Baseline (Prompt 3A revalidated)

Live `abhyanvaya_db` pre-flight via `ILegacySemesterMigrationDecisionPlanService`:

| Check | Result |
| --- | --- |
| `MatchesPrompt2BBaseline` | **True** |
| Semester III decision | **SPLIT** |
| Source | SemId=**3**, Number=3, GroupId=**NULL**, Course=B.Com |
| Finance students | **60** (GroupId=1) |
| CA students | **236** (GroupId=2) |
| Total | **296** |
| Semester 9 | ALREADY_GROUP_SPECIFIC — **must not modify** |
| Semesters 1, 2 | RETAIN_LEGACY_PENDING_DECISION — unchanged |
| Semesters 4, 5 | DUPLICATE_REVIEW — unchanged |

---

## 2. Approved decision

Convert legacy course-wide Semester III into two Group-specific Semester III records:

| Target | Course | Group | Number |
| --- | --- | ---: | ---: |
| A | B.Com | Finance (Id=1) | 3 |
| B | B.Com | Computer Applications / CA (Id=2) | 3 |

**Authoritative mapping key:** `Student.GroupId` → target `Semester.GroupId`.  
No name / roll / majority / UI / heuristic mapping.

---

## 3. Source & targets (executed)

| Role | Id | GroupId | Notes |
| --- | ---: | ---: | --- |
| Source (legacy) | **3** | NULL | **Retained** — not deleted, GroupId unchanged |
| Finance Semester III | **10** | 1 | Created |
| CA Semester III | **11** | 2 | Created |

---

## 4. Migration rules

1. Re-run Prompt 3A decision plan; abort if baseline mismatch or no SPLIT.
2. Resolve Finance/CA Groups by approved counts (60 / 236) from the plan.
3. Validate Group ownership via `SemesterGroupOwnershipRules` (CourseId aligned from Group; fail closed on cross-tenant/cross-course).
4. Validate all 296 affected students **before** any Semester mutation.
5. Create or reuse exactly one Group-specific Semester per Group+Number=3 (abort on duplicates).
6. Remap `Student.SemesterId` only.
7. Post-verify counts and ownership; abort + rollback on any failure.
8. Leave legacy Sem III readable for downstream references.

---

## 5. Transaction boundary

```
BEGIN TRANSACTION  (IUnitOfWork.ExecuteInTransactionAsync)
  Validate Prompt 3A + source + groups + student distribution
  Snapshot downstream reference counts (read-only)
  Create/reuse Finance Sem III + CA Sem III
  Remap 296 students
  Post-verify
COMMIT
```

Any Abort / DomainException → **ROLLBACK** (no partial migration).

---

## 6. Student mapping rule

```
Student.SemesterId == 3 (legacy)
  AND Student.GroupId == 1  =>  SemesterId = Finance Sem III (10)
  AND Student.GroupId == 2  =>  SemesterId = CA Sem III (11)
```

Every remapped student must satisfy:

- `Student.CourseId == TargetSemester.CourseId`
- `Student.GroupId == TargetSemester.GroupId`
- `TargetSemester.GroupId IS NOT NULL`
- `TargetSemester.Number == 3`

---

## 7. Pre-flight checks (executed)

- Prompt 3A SPLIT confirmed  
- Counts 60 / 236 / 296 confirmed  
- Source Sem 3 unique NULL-group Number=3  
- Groups 1 & 2 same Course/tenant  
- Sem 9 Group-specific unchanged  
- No unexpected baseline drift  

---

## 8. Post-flight checks (executed)

| Metric | Expected | Actual |
| --- | ---: | ---: |
| Finance remapped | 60 | 60 |
| CA remapped | 236 | 236 |
| Total remapped | 296 | 296 |
| Remaining on legacy Sem 3 | 0 | 0 |
| Unresolved | 0 | 0 |
| Legacy Sem 3 GroupId | NULL | NULL |
| Semesters created | 2 | 2 (Ids 10, 11) |
| Status | Completed | **Completed** |
| RolledBack | false | false |

---

## 9. Downstream references detected (informational — not mutated)

| Consumer | Refs to legacy Sem 3 |
| --- | ---: |
| AttendanceSession | 67 |
| Subject | 17 |
| Section | 8 |
| SubjectAllocation | 1 |
| TimetableEntry | 1 |
| TeachingGroup | 2 |

Prompt 3B did **not** modify these entities. Remapping is deferred to Prompt 3C+.

---

## 10. Rollback & idempotency

- Fail-closed Abort returns `Status=Aborted`, `RolledBack=true`.
- Transaction wrapper rolls back Semester creates and Student updates together.
- Student count / ownership validation runs **before** Semester create to avoid unnecessary writes.
- Second execution: `TryAlreadyCompletedAsync` runs **before** the Prompt 3A baseline gate, so intentional post-split baseline drift (legacy Sem III student count 0; SemIds 10/11 present) yields `AlreadyCompleted` instead of Abort.

---

## 11. Application surface

| Item | Detail |
| --- | --- |
| Service | `ISemesterIiiSplitStudentRemapMigrationService` / `SemesterIiiSplitStudentRemapMigrationService` |
| API | `POST /api/semester/migrations/semester-iii-split-student-remap` |
| Auth | Existing `CanManageSemesters` |
| Not exposed | Generic `PUT /students/{id}/semester` |
| Local runner | `scripts/P1_4_Prompt3B_Runner` (`--preflight` / `--execute`) |

---

## 12. Tests

Focused unit + architecture guards in  
`SemesterIiiSplitStudentRemapMigrationServiceTests.cs` / `AiSchedCatalogTimetableP14Prompt3BSemesterIiiSplitGuardTests`:

- Target create (Finance + CA)  
- Reuse existing targets  
- Duplicate targets → abort  
- Cross-course Group → abort  
- Invalid GroupId → abort  
- Count mismatch → abort  
- Baseline mismatch → abort  
- 60 / 236 / 296 remap  
- Idempotent AlreadyCompleted path  
- Downstream Subject refs unchanged  
- No TimetableSection / ConflictEngine / PublishAsync in migration source  
- No NULL GroupId on create  
- Explicit migration endpoint only  
- Frozen TG / CAP / Course.DepartmentId / EnablePrograms boundaries  

**Unit result:** 13 passed.

---

## 13. Architecture guards

- Group is Semester ownership SSOT (`SemesterGroupOwnershipRules`)  
- CourseId aligned from Group  
- Student.GroupId determines Semester  
- New Semesters never get NULL GroupId  
- Legacy NULL-group Sem III remains readable  
- No downstream scheduling ownership changes  
- Teaching Group architecture frozen (no create/delete/infer)  
- CAP frozen (no ConflictEngine / Publish / PlacementSize changes)  
- No projector / TimetableSection writes  
- Tenant/college boundaries fail closed  

---

## 14. Known limitations

- Downstream entities still reference legacy Sem III (expected).  
- Semesters 1, 2, 4, 5 remain legacy / duplicate-review (out of scope).  
- DB unique constraint on `(TenantId, GroupId, Number)` and NOT NULL GroupId **not** enforced in this prompt.  
- Protected-semester checks assume local Ids 1, 2, 4, 5, 9 (Prompt 2B/3A baseline).  

---

## 15. Deferred Prompt 3C scope

Do **not** start automatically. Recommended next prompt:

**P1-4 Prompt 3C** — remapping / reconciliation of downstream references still pointing at legacy Sem III:

- AttendanceSession  
- Subject  
- Section / StudentSection  
- SubjectAllocation  
- TimetableEntry  
- TeachingGroup (identify-only unless separately approved; TG remains frozen for inference/auto-create)

Plus later: Semester NOT NULL + DB unique constraint migration (separate approved prompt).

---

## 16. Safety to proceed to Prompt 3C

**Yes — safe to proceed to P1-4 Prompt 3C** for downstream remapping, provided Prompt 3C:

- Continues fail-closed GroupId-based mapping  
- Does not infer/create Teaching Groups  
- Does not alter CAP / ConflictEngine / Publish  
- Does not delete legacy Sem III until all references are resolved  

**STATUS: PASS**
