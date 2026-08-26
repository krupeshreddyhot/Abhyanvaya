# AI29.1D.15A — Attendance Save Scope Integrity & Faculty Allocation UX

**Final architecture documentation (Prompt 10)**  
**Workstream:** Attendance write-scope integrity + Faculty Allocation UX hardening  
**Baseline:** AI29.1D through Prompt 15 (operational timetable context UI)  
**Status:** Implemented and regression-verified (Prompt 9)

---

## Canonical model ownership (explicit)

| Concern | Owner | Remains |
|---------|--------|---------|
| **Subject Master** | Course → Group → Semester → Subject | No Section on Subject |
| **Section** | Operational student grouping (`Section` + `StudentSection`) | Optional population filter for attendance |
| **Faculty allocation** | Existing `FacultySectionAssignment` | No second FacultySection entity |
| **Combined classes** | Existing `SectionGroup` / `TimetableSections` | No parallel combined-class model |
| **Attendance resolver** | `AttendanceSessionResolver` | Single session authority; 15A does not fork it |
| **Legacy attendance** | Course → Group → Semester → Subject → Period | Works **without** timetable or Section |

**Hard rules preserved:** timetable not mandatory; Section not mandatory; React never owns eligibility; unauthorized student ⇒ reject entire write; no second allocation/scoring engine.

---

## 1. Attendance Save Scope architecture

Save scope is an **optional request-level filter** on mark/edit. It reuses roster scope helpers; it does **not** resolve timetable sessions.

```
UI (AttendanceMarking)
  → buildAttendanceWritePayload({ selectedSectionIds, students })
  → POST /api/attendance/mark | PUT /api/attendance/edit
       body: subjectId, date, students[{studentNumber,status}],
             optional sectionId / sectionIds
  → AttendanceController
       1) FacultySubjectAccess (existing)
       2) AttendanceSaveScope.ValidateWriteSectionScopeAsync
            → AttendanceSectionScope.Normalize + ValidateSectionIdsAsync
       3) If scope non-empty:
            AttendanceSaveScope.ValidateEverySubmittedStudentInSectionScopeAsync
            (Submitted → current StudentSection → validated sections)
       4) Persist only via ExecuteInTransactionAsync (atomic)
```

| Component | Role |
|-----------|------|
| `AttendanceSectionScope` | Normalize ids; AY ExactlyOne when sections present; Tenant+AY+C/G/S section validation; StudentSection filter |
| `AttendanceSaveScope` | Mark/edit contract wrappers; every-student authorization; atomic build helpers |
| `AttendanceSessionResolver` | **Unchanged** — read-side session/timetable context only |

Empty / omitted `sectionId` + `sectionIds` ⇒ **no section scope** (legacy path).

---

## 2. Legacy no-Section compatibility

Clients that omit section fields behave as before:

1. Normalize yields empty scope → `HasSectionScope == false`.
2. No Academic Year ExactlyOne requirement for the write.
3. No StudentSection membership gate for submitted students (subject/cohort access still applies via existing FacultySubjectAccess / student resolution).
4. Full Course→Group→Semester→Subject→Period cascade remains usable without timetable.

This preserves AI22-era and non-section tenants.

---

## 3. Section-scoped attendance

When one positive section id is supplied (via `sectionId` and/or single-element `sectionIds`):

1. Current Academic Year must resolve to **ExactlyOne**.
2. Section must match Tenant + that AY + subject's Course/Group/Semester.
3. Roster (`GET students-for-marking`) and writes both restrict to students with current membership in that section.
4. Unauthorized section ⇒ clear reject (`SectionOutOfScopeMessage` / equivalent).

Section remains an **operational grouping**, not a Subject Master attribute.

---

## 4. Combined-section attendance

Multiple positive section ids = combined operational class (e.g. A+B):

1. Same AY + C/G/S validation for **every** id.
2. Student authorization is **OR** membership across the validated set (student in A **or** B is allowed for A+B).
3. Student only in C when scope is A+B ⇒ unauthorized ⇒ entire write rejected.
4. Combined identity continues to come from existing `SectionGroup` / timetable participation UI; 15A does **not** invent a new combined entity for attendance writes.

---

## 5. Timetable-derived attendance

```
GET /api/attendance-resolution/current  → AttendanceSessionResolver
  → UI prefills Course / Group / Semester / Subject / Period / Section(s) / Room
  → Roster via students-for-marking (optional sectionIds from session)
  → Mark/Edit may include those section ids at request level
```

- Resolver remains the **only** session authority.
- 15A does not call the resolver during mark/edit persistence.
- Combined timetable participation still uses existing `TimetableSections` / `SectionGroup` behavior (AI29.1D Prompts 13–15).

---

## 6. Manual attendance

Faculty **without** a timetable assignment can still:

**Course → Group → Semester → Subject → Period → Students → Mark / Edit**

Section selectors remain optional. Manual mode does not require session resolution. Edit uses the same optional section + atomic student checks as mark.

---

## 7. Academic Year authority

Reuse of `AttendanceSectionScope.ResolveAuthoritativeCurrentAcademicYearAsync`:

| Status | Section-scoped attendance |
|--------|---------------------------|
| `ExactlyOne` | Allowed |
| `None` | Rejected (`NoCurrentAcademicYearMessage`) |
| `Multiple` | Rejected (`MultipleCurrentAcademicYearsMessage`) — never guess |

Legacy (no section ids) does **not** require ExactlyOne for the write path.

Faculty assign additionally requires `request.AcademicYearId` to match the section’s authoritative `AcademicYearId` (no client drift).

---

## 8. Server-side student authorization

When section scope is present:

1. Resolve authorized students from current `StudentSection` rows for validated section ids (tenant-scoped).
2. Every submitted `studentNumber` must appear in that authorized set.
3. UI must **not** filter as a security mechanism (`buildAttendanceWritePayload` documents server authority).
4. Injected / out-of-scope students ⇒ fail-closed reject message:

> *Attendance rejected: one or more students are outside the authorized section scope. No attendance was saved.*

---

## 9. Atomic attendance save behavior

- Mark/edit persistence runs inside `ExecuteInTransactionAsync`.
- Partial success is forbidden: if any submitted student fails scope checks, **zero** rows are committed (e.g. 99 valid + 1 invalid ⇒ 0 saved).
- Helpers: `ValidateEverySubmittedStudentInSectionScopeAsync`, `BuildAtomicMarkRows`, `CountAtomicCommitOrZero`.

---

## 10. Faculty selector architecture

| Layer | Detail |
|-------|--------|
| UI | `FacultyStaffSelector` — search/select over `GET /api/staff` (paged, typically pageSize 25) |
| Panel | `FacultySectionAllocationPanel` — replaced free-text “Faculty (Staff) Id” |
| Display helpers | `facultyStaffSelector.ts` |
| Assign payload | Still posts existing `facultyId` (Staff id) to `POST /api/faculty-sections` |

No new staff/faculty API; selector is a UX layer over existing Staff catalog.

---

## 11. Faculty authorization

`FacultySectionAssignmentAuthorization.ValidateAssignAsync` (wired in `SectionManagementService.AssignFacultyAsync`):

Validates **Tenant + Faculty (Staff) + Academic Year + Course + Group + Semester + Section**.

Rejects: missing section, AY mismatch, out-of-scope C/G/S, other-tenant / deleted / inactive staff.  
**Never** substitutes another faculty id for a rejected request.

Policies (unchanged ownership):

| Endpoint | Policy |
|----------|--------|
| `GET /api/faculty-sections` | `CanViewSections` |
| `POST /api/faculty-sections` | `CanAssignSectionFaculty` |
| Attendance mark/edit | `CanManageAttendance` (+ FacultySubjectAccess in controller) |

---

## 12. Existing FacultySectionAssignment reuse

- Domain entity: `FacultySectionAssignment` only.
- DTO: `FacultySectionDto` / `AssignFacultySectionRequest` (`FacultyId`, `SectionId`, `AcademicYearId`, `Role`, `EffectiveFrom`).
- No `FacultySection` entity, no `SectionGroupId` / `SubjectId` on the assignment entity for combined/subject binding.
- Combined operational display is a **UI/DTO projection**, not a new persistence model.

---

## 13. Combined SectionGroup presentation

Faculty Allocation panel projects existing assignments + `SectionGroup` membership:

- Operational class label (e.g. `Combined · A + B`)
- Underlying section codes / ids
- Retained assignment ids

Helpers: `facultySectionAllocationView.ts`. Still SectionGroup + existing per-section assignments — no parallel assignment graph.

---

## 14. APIs changed

| API | 15A change |
|-----|------------|
| `POST /api/attendance/mark` | Additive optional `sectionId` / `sectionIds`; section-scope + atomic student auth when present |
| `PUT /api/attendance/edit` | Same as mark |
| `GET /api/attendance/students-for-marking` | Prior AI29.1D section query params; reused by 15A write alignment (not redesigned) |
| `POST /api/faculty-sections` | **Contract unchanged**; stronger server validation via authorization helper |
| `GET /api/staff` | Unchanged; consumed by Faculty selector |
| `GET /api/attendance-resolution/current` | Unchanged; resolver remains authoritative for session prefill |

No new public routes introduced solely for 15A.

---

## 15. DTO changes

### `MarkAttendanceRequest` / `EditAttendanceRequest`

| Property | Notes |
|----------|-------|
| `SubjectId`, `Date`, `Students` | Unchanged core |
| `SectionId` (`int?`) | Optional convenience |
| `SectionIds` (`List<int>?`) | Optional; one = single, many = combined |
| `StudentAttendanceDto` | Still `{ StudentNumber, Status }` only — no per-student section fields |

### Faculty assign

`AssignFacultySectionRequest` / `FacultySectionDto` — **shape unchanged**.

---

## 16. Tests

### Backend (`Abhyanvaya.Application.UnitTests`)

| File | Focus |
|------|-------|
| `AI29_1D_15A_Prompt2_AttendanceSaveScopeContractTests.cs` | Normalize / optional contract |
| `AI29_1D_15A_Prompt3_AttendanceSaveSectionAuthorizationTests.cs` | Section + AY write auth |
| `AI29_1D_15A_Prompt4_AttendanceStudentWriteScopeIntegrityTests.cs` | Student membership integrity |
| `AI29_1D_15A_Prompt4_AtomicAttendanceWriteIntegrationTests.cs` | Atomic reject / commit |
| `AI29_1D_15A_Prompt7_FacultySectionAssignmentAuthorizationTests.cs` | Faculty assign auth |
| `AI29_1D_15A_Prompt8_CombinedFacultyAllocationTests.cs` | Combined projection model |
| `AI29_1D_15A_Prompt9_RegressionArchitectureGuardTests.cs` | Architecture guards |

Related AI29.1D Prompt 11–14 attendance/faculty UI tests remain the roster/behavior baseline.

### Frontend (`abhyanvaya-ui`)

| File | Focus |
|------|-------|
| `attendanceMarkingScope.test.ts` | Save scope + write payload |
| `facultyStaffSelector.test.ts` | Selector helpers |
| `facultySectionAllocationView.test.ts` | Combined display |

### Prompt 9 verification snapshot

- API build PASS · UI build PASS  
- `AI29` 274 · `AI29_1D` 136 · Prompt 9 guards 11 · Scheduling 165 · AI22/AI31/Optimization filter 145 · UI 15A vitest 35 — all passed  

---

## 17. Security considerations

| Risk | Mitigation |
|------|------------|
| Client-forged student list | Server re-authorizes every student against StudentSections when scope present |
| Client-forged section ids | Tenant + ExactlyOne AY + C/G/S section validation |
| Partial write after injection | Transactional reject; 0 committed |
| React eligibility as security | Explicitly forbidden; UI payload builder does not filter for security |
| Faculty id spoof / swap | Assign auth never substitutes faculty; inactive/other-tenant rejected |
| Privilege escalation on assign | Existing `CanAssignSectionFaculty` + scope checks |
| Timetable bypass of scope | Resolver not used to skip write auth; write path independent |

Attendance controller policy remains `CanManageAttendance`.

---

## 18. Performance considerations

| Area | Note |
|------|------|
| Section normalize | In-memory distinct/positive filter — O(n) small n |
| AY resolve | Single indexed query for current years per tenant |
| Section validate | Batched section lookup constrained by C/G/S/AY |
| Student membership | Query StudentSections for scope ids; set membership check in memory |
| Atomic write | One transaction; fail-fast before mutate when unauthorized |
| Faculty selector | Paged `GET /api/staff` (UI pageSize ~25) — avoid loading full staff list |
| Combined display | Client-side projection over already-fetched assignments + section groups |

No new heavy engines or N+1 patterns introduced by design; keep mark/edit student sets bounded to the on-screen roster.

---

## 19. Rollback / compatibility considerations

| Concern | Guidance |
|---------|----------|
| Old clients omitting section fields | Fully compatible (legacy path) |
| New clients sending section fields | Require ExactlyOne AY + valid membership |
| DTO additive fields | Safe for older serializers that ignore unknown JSON; omitting fields = legacy |
| Faculty selector UI | API still accepts numeric `facultyId`; rollback UI can restore text entry without API change |
| Assign authorization tightening | Behavior-only; may reject previously invalid/out-of-scope assigns that slipped through — intentional hardening |
| Database | **No 15A migrations** — rollback is code/deploy only |
| Resolver / Subject / SectionGroup / Allocation engines | Untouched — no schema/domain rollback for those |

---

## Implementation manifest

### Files created (15A)

**Application**

- `Abhyanvaya.Application/Academic/AttendanceSaveScope.cs`
- `Abhyanvaya.Application/Academic/FacultySectionAssignmentAuthorization.cs`  
  *(Note: `AttendanceSectionScope.cs` originated in AI29.1D Prompt 11A/11B and is reused by 15A.)*

**UI**

- `abhyanvaya-ui/src/components/sections/FacultyStaffSelector.tsx`
- `abhyanvaya-ui/src/components/sections/FacultySectionAllocationPanel.tsx` *(15A.6–8 enhancements; panel lineage from Prompt 14)*
- `abhyanvaya-ui/src/utils/facultyStaffSelector.ts`
- `abhyanvaya-ui/src/utils/facultyStaffSelector.test.ts`
- `abhyanvaya-ui/src/utils/facultySectionAllocationView.ts` *(Prompt 14 / 15A.8)*
- `abhyanvaya-ui/src/utils/facultySectionAllocationView.test.ts`
- `abhyanvaya-ui/src/utils/attendanceMarkingScope.ts` *(15A write helpers atop Prompt 11+ scope utils)*
- `abhyanvaya-ui/src/utils/attendanceMarkingScope.test.ts`

**Tests**

- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt2_AttendanceSaveScopeContractTests.cs`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt3_AttendanceSaveSectionAuthorizationTests.cs`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt4_AttendanceStudentWriteScopeIntegrityTests.cs`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt4_AtomicAttendanceWriteIntegrationTests.cs`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt7_FacultySectionAssignmentAuthorizationTests.cs`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt8_CombinedFacultyAllocationTests.cs`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt9_RegressionArchitectureGuardTests.cs`

**Docs**

- `docs/AI29_1D_15A_ARCHITECTURE_DISCOVERY.md`
- `docs/AI29_1D_15A_PROMPT_2_ATTENDANCE_SAVE_CONTRACT.md`
- `docs/AI29_1D_15A_PROMPT_3_ATTENDANCE_SAVE_SECTION_AUTHORIZATION.md`
- `docs/AI29_1D_15A_PROMPT_4_ATTENDANCE_STUDENT_WRITE_SCOPE_INTEGRITY.md`
- `docs/AI29_1D_15A_PROMPT_5_ATTENDANCE_UI_WRITE_SCOPE.md`
- `docs/AI29_1D_15A_PROMPT_6_FACULTY_SELECTOR.md`
- `docs/AI29_1D_15A_PROMPT_7_FACULTY_ASSIGNMENT_AUTHORIZATION.md`
- `docs/AI29_1D_15A_PROMPT_8_COMBINED_FACULTY_ALLOCATION.md`
- `docs/AI29_1D_15A_PROMPT_9_REGRESSION_ARCHITECTURE_GUARD.md`
- `docs/AI29_1D_15A_ATTENDANCE_SAVE_SCOPE_AND_FACULTY_ALLOCATION.md` *(this document)*

### Files modified (15A core)

- `Abhyanvaya.API/Controllers/AttendanceController.cs` — mark/edit scope + atomic student auth
- `Abhyanvaya.Application/DTOs/MarkAttendanceRequest.cs` — optional section fields
- `Abhyanvaya.Application/DTOs/public class EditAttendanceRequest.cs` — optional section fields
- `Abhyanvaya.Application/Academic/SectionManagementService.cs` — assign authorization wiring
- `abhyanvaya-ui/src/pages/AttendanceMarking.tsx` — write payload / optional section
- `abhyanvaya-ui/src/services/attendanceService.ts` — optional section on mark/edit types
- `abhyanvaya-ui/src/pages/setup/SectionsPage.tsx` — Faculty Allocation panel integration
- `abhyanvaya-ui/src/services/sectionService.ts` — faculty-sections client usage (as needed by panel)

### Migrations

**None.** AI29.1D.15A introduces no EF Core migrations and no schema changes.

### APIs (summary)

| Method | Path | 15A impact |
|--------|------|------------|
| POST | `/api/attendance/mark` | Additive section scope + atomic auth |
| PUT | `/api/attendance/edit` | Additive section scope + atomic auth |
| GET | `/api/attendance/students-for-marking` | Consumed; section query from prior 1D |
| GET | `/api/attendance-resolution/current` | Unchanged |
| GET | `/api/faculty-sections` | Unchanged contract |
| POST | `/api/faculty-sections` | Stronger validation; same body |
| GET | `/api/staff` | Unchanged; selector source |
| GET | `/api/section-groups` | Unchanged; combined display source |

### Permissions / authorization policies

| Policy | Usage in 15A surface |
|--------|----------------------|
| `CanManageAttendance` | AttendanceController (mark/edit/roster) |
| `CanViewSections` | FacultySectionsController GET |
| `CanAssignSectionFaculty` | FacultySectionsController POST |
| FacultySubjectAccess (application check) | Existing mark/edit subject access — retained |
| No new permission keys | 15A did not add new policy names |

### Configuration changes

**None.** No new `appsettings` keys, feature flags, or environment variables introduced by AI29.1D.15A.

---

## Related prompt docs

| Prompt | Document |
|--------|----------|
| 1 Discovery | `AI29_1D_15A_ARCHITECTURE_DISCOVERY.md` |
| 2–8 Design/impl notes | `AI29_1D_15A_PROMPT_2_…` through `…_PROMPT_8_…` |
| 9 Regression | `AI29_1D_15A_PROMPT_9_REGRESSION_ARCHITECTURE_GUARD.md` |
| 10 Final (this) | `AI29_1D_15A_ATTENDANCE_SAVE_SCOPE_AND_FACULTY_ALLOCATION.md` |

---

*ADL volumes were not inspected for this document; architecture claims are based on repository source, tests, and prior AI29.1D.15A prompt artifacts available in this workspace.*
