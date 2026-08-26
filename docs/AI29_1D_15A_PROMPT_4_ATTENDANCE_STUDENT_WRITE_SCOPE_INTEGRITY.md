# AI29.1D.15A Prompt 4 — Attendance Student Write-Scope Integrity

## Risk addressed

Roster/read is Section-aware, but mark/edit must **never trust** the browser’s student list.

## Required chain (section-scoped writes)

```
Submitted Student
      ↓
Current StudentSection (IsCurrent)
      ↓
Academic Year (exactly one IsCurrent — via section validation)
      ↓
Selected Section(s)  [A] or [A,B] …
      ↓
Authorized (every submitted student) OR reject entire write
```

| Client `sectionIds` | Rule |
|---------------------|------|
| `[A]` | Every submitted student must belong to **A** |
| `[A, B]` | Every submitted student must belong to **A OR B** |
| omitted / empty | Legacy path — this Prompt 4 validator is a no-op |

## Behavior

- `AttendanceSaveScope.ValidateEverySubmittedStudentInSectionScopeAsync` is the write-path authority for student membership.
- Re-validates section academic scope (Tenant + AY + Course/Group/Semester from subject).
- Requires **current** `StudentSection` rows only (`IsCurrent`).
- **Fail closed:** any unauthorized student → `BadRequest` with a clear message; **no partial save**; **no silent drop**.
- `BuildAtomicMarkRows` plans **all** rows or **none**.
- Mark/edit persist via `IUnitOfWork.ExecuteInTransactionAsync` (commit all or roll back).
- Language cohort (and elective `StudentSubjects` on mark) still applied on top of section membership.

### Atomic example

| Submitted | Valid | Unauthorized | Committed |
|-----------|-------|--------------|-----------|
| 100 | 99 | 1 | **0** |

Response includes: `No attendance was saved.`

### Combined Section

`sectionIds = [A, B]` ⇒ one operational class: student ∈ A **OR** B. Student ∈ C ⇒ reject entire write.

### Legacy (no Section)

Course → Group → Semester → Subject (+ Period in UI) preserved. **No** Academic Year requirement.

## Non-goals

- No second eligibility model in React
- No `AttendanceSessionResolver` changes
- Legacy no-Section writes unchanged (AY not required)

## Tests

- `AI29_1D_15A_Prompt4_AttendanceStudentWriteScopeIntegrityTests.cs`
- `AI29_1D_15A_Prompt4_AtomicAttendanceWriteIntegrationTests.cs` — Section A / A+B / C / legacy / wrong AY / 100→0 / edit valid & unauthorized / transaction wiring

## Files

- `Abhyanvaya.Application/Academic/AttendanceSaveScope.cs`
- `Abhyanvaya.API/Controllers/AttendanceController.cs`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt4_AttendanceStudentWriteScopeIntegrityTests.cs`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt4_AtomicAttendanceWriteIntegrationTests.cs`
- `docs/AI29_1D_15A_PROMPT_4_ATTENDANCE_STUDENT_WRITE_SCOPE_INTEGRITY.md`
