# AI29.1D.15A — Architecture Discovery (Prompt 1)

**Scope:** Attendance Save Scope Integrity + Faculty Allocation UX  
**Mode:** Discovery only — no business logic changes in this prompt.  
**Baseline:** AI29.1D through Prompt 15 (operational timetable context UI).

---

## Hard architectural rules (do not violate)

| Do NOT | Reason |
|--------|--------|
| Make timetable assignment mandatory | Manual Course→Group→Semester→Subject→Period must remain |
| Remove legacy cascade path | Compatibility constraint |
| Make Section mandatory | Optional population filter only |
| Add Section to Subject Master | Subject remains C+G+S |
| Create another `AttendanceSessionResolver` | Single session authority |
| Create another `SectionGroup` | Existing model + TimetableSections |
| Implement student eligibility in React | Server owns cohort / StudentSections |
| Trust submitted student IDs | Server must re-authorize every write |
| Create a second FacultySectionAssignment model | Use existing entity + `/faculty-sections` |
| Create a second allocation/scoring engine | Scheduling / Allocation engines untouched |
| Bypass existing authorization | Reuse policies / FacultySubjectAccess |
| Partially save when one student is unauthorized | Fail closed — reject entire write |

**Do not modify:** AttendanceSessionResolver architecture, Subject Master, Section / SectionGroup / TimetableSections domain models, Scheduling engine, Allocation engine, allocation scoring, allocation governance.

---

## A. Attendance Save Scope Integrity

### Current flow

```
AttendanceMarking.tsx
  → fetchFullRoster() via getStudentsForMarking (optional sectionId / sectionIds)
  → markAttendance | editAttendance  (subjectId, date, students[])
  → POST /api/attendance/mark | PUT /api/attendance/edit
  → AttendanceController
  → FacultySubjectAccess + inline cohort checks
  → IApplicationDbContext Attendances persistence
```

Timetable / session context (Prompt 11–15) is **read-side only** for save:

```
GET /api/attendance-resolution/current  → AttendanceSessionResolver
  → UI prefills Course/Group/Semester/Subject/Period/Section/Room
  → Roster still loaded via students-for-marking
  → Mark/Edit payload does NOT currently carry section scope
```

### 1. Mark request DTO

`Abhyanvaya.Application/DTOs/MarkAttendanceRequest.cs`

| Property | Type |
|----------|------|
| `SubjectId` | `int` |
| `Date` | `DateTime` |
| `Students` | `List<StudentAttendanceDto>` |

`StudentAttendanceDto`: `StudentNumber`, `Status`  
**No** `sectionId` / `sectionIds` / combined-class fields today.

### 2. Edit / update request DTO

`Abhyanvaya.Application/DTOs/public class EditAttendanceRequest.cs` → `EditAttendanceRequest`

| Property | Type |
|----------|------|
| `SubjectId` | `int` |
| `Date` | `DateTime` |
| `Students` | `List<StudentAttendanceDto>` |

Same student shape as mark. **No** section scope fields.

### 3. AttendanceController endpoints

`Abhyanvaya.API/Controllers/AttendanceController.cs`  
Policy: `[Authorize(Policy = CanManageAttendance)]` on controller.

| Method | Route | Role |
|--------|-------|------|
| `MarkAttendance` | `POST api/attendance/mark` | Create day’s attendance rows |
| `EditAttendance` | `PUT api/attendance/edit` | Update statuses (Admin may override lock) |
| `GetStudentsForMarking` | `GET api/attendance/students-for-marking` | Roster (+ optional section filter) |
| `GetAttendance` | `GET api/attendance` | Read day |
| Lock endpoint | (same controller) | Lock day |

UI service: `abhyanvaya-ui/src/services/attendanceService.ts`  
(`getStudentsForMarking`, `markAttendance`, `editAttendance`).

### 4. Existing student / cohort validation (writes)

**Mark (`MarkAttendance`):**

1. Tenant context required.
2. Reject empty students / future dates.
3. Load subject; `FacultySubjectAccess.FacultyMayAccessSubjectAsync` → Forbid if denied.
4. **Deep-validate only the first** student number: non-elective → Course/Group/Semester + language cohort; elective → `StudentSubjects` mapping.
5. Reject if attendance already exists for subject+day (tenant).
6. Load students by submitted numbers + tenant (+ legacy Faculty JWT course/group if `StaffId <= 0`) + language filter.
7. Insert rows for students found in that query; **silently skip** numbers not found / filtered out.
8. Persist via `_context.AddAttendances` + `SaveChangesAsync`.

**Edit (`EditAttendance`):**

1. Subject + FacultySubjectAccess.
2. Load existing attendance rows for subject+day (+ language filter on attendance query).
3. Lock check (non-Admin blocked).
4. Update status for records whose student number appears in the request map.
5. Does **not** re-validate submitted students against Course/Group/Semester/Section/elective.

**Gap (integrity):** Submitted student numbers are not fully re-authorized as a set. Unauthorized / out-of-scope IDs can be dropped silently (partial effective save) rather than failing the whole request. Section scope used for roster is **not** applied on mark/edit.

### 5. Section filtering in `students-for-marking`

Query params (optional): `sectionId`, `sectionIds[]`.

- `AttendanceSectionScope.NormalizeRequestedIds`
- If non-empty: `ValidateSectionIdsAsync` (Tenant + exactly one current Academic Year + Course/Group/Semester match) → BadRequest on failure
- `ApplyStudentSectionFilter` via current `StudentSections`
- Empty section params → legacy full Course/Group/Semester cohort (AY not required)
- Prompt 13 additive response: `sectionId`/`sectionCode` per student, `isCombinedClass`, `participatingSectionIds/Codes`, `operationalClassLabel`

### 6. AttendanceSectionScope / Section authorization

`Abhyanvaya.Application/Academic/AttendanceSectionScope.cs`

- Academic year authority: ExactlyOne | None | Multiple (fail-closed when section filter used)
- Validates section IDs against Tenant / AY / Course / Group / Semester
- Applies `StudentSections` membership filter
- **Used on roster read path today; not on mark/edit write path**

### 7. AttendanceSessionResolver integration

- Implementation: `Abhyanvaya.Application/Scheduling/Conflicts/AttendanceSessionResolver.cs`
- Consumed via attendance-resolution / session APIs (UI: Prompt 11–15)
- Supplies Timetable mode, subject/period/room, `sectionIds` / `sectionCodes` from TimetableSections
- UI maps to Timetable-derived vs Manual context (`operationalTimetableContext.ts`)
- **Must remain the only session resolver** — 15A must not invent a parallel one in React or a second BE resolver

### 8. Combined-section handling

| Layer | Behavior |
|-------|----------|
| Session | Resolver returns multiple section ids/codes |
| Roster | `sectionIds` query → one combined roster; Prompt 13 banner/metadata |
| Save | Mark/Edit unchanged — no combined-class fields on write DTO |

Combined operational-class metadata is **display/roster additive**; not required on save contract today.

### 9. Authorization rules (attendance writes)

| Rule | Mechanism |
|------|-----------|
| API access | `CanManageAttendance` |
| Subject access | `FacultySubjectAccess` → `StaffSubjectAssignments` when Faculty+StaffId |
| Legacy Faculty | JWT Course/Group cohort filter when Faculty and `StaffId <= 0` |
| Elective | `StudentSubjects` (first student on mark; roster filters differently) |
| Language subjects | Teaching language slot filters |
| Lock | Mark blocked if locked; Edit Admin override |

### 10. Existing tests covering attendance writes / section scope

| Area | Tests |
|------|-------|
| Section scope / AY / roster | `AI29_1D_Prompt11A_AttendanceSectionScopeTests.cs` (includes “Save_Regression_*” that assert **roster** membership used as mark payload — **not** controller write fail-closed) |
| AY authority | `AI29_1D_Prompt11B_AcademicYearAuthorityTests.cs` |
| UI / resolver contract | `AI29_1D_Prompt11_AttendanceUiIntegrationTests.cs` |
| Section behavior | `AI29_1D_Prompt12_AttendanceSectionBehaviorTests.cs` |
| Combined UI | `AI29_1D_Prompt13_CombinedSectionUiTests.cs` |
| Session resolver (scheduling) | `AttendanceSessionResolverTests.cs` |
| FE scope helpers | `attendanceMarkingScope.test.ts`, `attendanceSectionBehavior.test.ts`, `operationalTimetableContext.test.ts` |

**Missing for 15A:** dedicated mark/edit controller/service tests that reject unauthorized submitted students with **no partial persist**.

### Smallest additive write contract

Carry optional scope only (mirror roster query):

| Field | On Mark/Edit DTO | Required? |
|-------|------------------|-----------|
| `SectionId` | optional `int?` | No |
| `SectionIds` | optional `int[]` / list | No |
| Combined class label / flags | **Do not add** | Not required by existing write contract; keep on roster response only |

Server behavior (proposed for later prompts — not implemented here):

1. Resolve subject → Course/Group/Semester (authoritative).
2. Normalize optional section ids via existing `AttendanceSectionScope`.
3. Build authorized student set server-side (same filters as roster: C/G/S, language/elective rules, optional StudentSections).
4. If **any** submitted `StudentNumber` is outside that set → `BadRequest` / Forbid-equivalent; **save nothing**.
5. If section ids omitted → legacy full cohort path (timetable not required; Section not required).

UI may later send the same `sectionIds` already used for roster; eligibility remains server-owned.

---

## B. Faculty Allocation UX

### Current flow

```
SectionsPage (Faculty Allocation tab)
  → FacultySectionAllocationPanel
  → listFacultySections / assignFacultySection (sectionService)
  → GET/POST /api/faculty-sections
  → FacultySectionsController
  → ISectionManagementService / SectionManagementService
  → FacultySectionAssignment + StaffMembers
```

Display enrichment (Prompt 14):

- Subject column ← existing `listSubjectAllocations` (scheduling Subject Allocations)
- Combined SectionGroup display ← existing `listSectionGroups` (no new model)
- Columns: Section, Faculty, Subject, Effective From/To, Allocation Status

### Assignment form today

`FacultySectionAllocationPanel.tsx`:

- Free-text **“Faculty (Staff) Id”** (numeric string)
- Section select, Role (Primary/Secondary), Effective From
- `POST /api/faculty-sections` body: `{ facultyId, sectionId, academicYearId, role, effectiveFrom? }`

### Faculty-section API

| Item | Location |
|------|----------|
| Controller | `FacultySectionsController` in `SectionsController.cs` — route `api/faculty-sections` |
| GET | `CanViewSections` |
| POST Assign | `CanAssignSectionFaculty` |
| DTOs | `FacultySectionDto`, `AssignFacultySectionRequest` in `SectionDtos.cs` |
| Entity | `Abhyanvaya.Domain/Entities/Academic/FacultySectionAssignment.cs` (**single** model) |
| Service | `SectionManagementService.AssignFacultyAsync` — validates Section tenant + `StaffMembers` exists |

### Authoritative Faculty / Staff source

| Concern | Authority |
|---------|-----------|
| Entity | `Staff` / `StaffMembers` (not a separate Faculty entity) |
| FacultyId on assignment | `Staff.Id` |
| Assign validation | `StaffMembers.Any(Id + TenantId)` |
| List options for UX | Existing `GET /api/staff` via `listStaff` in `setupService.ts` → `StaffController.List` |

**Auth note:** `StaffController.List` is `[Authorize(TenantScopedAdmin)]`. Sections Faculty assign uses `CanAssignSectionFaculty`. Discovery recommendation: reuse **Staff** as the option source; if assigners are not always TenantScopedAdmin, a later UX prompt may need a **permission-aligned read** of teaching staff (still StaffMembers — no new entity). Do not invent a Faculty master or second faculty↔section relationship.

Secondary enrichment already used: Subject Allocations (`staffId`) for Subject column — not a substitute for the assign picker.

### Related Prompt 14 artifacts

- `docs/AI29_1D_PROMPT_14_FACULTY_SECTION_ALLOCATION_UI.md`
- `AI29_1D_Prompt14_FacultySectionAllocationUiTests.cs`
- `facultySectionAllocationView.ts` (+ FE tests)

---

## C. Compatibility constraints

Attendance must remain valid when:

1. **No timetable** → Manual Timetable-derived fallback (Prompt 15): Course → Group → Semester → Subject → Period  
2. **No Section** → full C/G/S (+ language/elective) cohort  
3. **Optional Section / SectionGroup ids** → population filter via StudentSections + AttendanceSectionScope  
4. Timetable mode still uses **existing** AttendanceSessionResolver + TimetableSections for context/prefill only  

---

## Proposed additive flow (future prompts — not this prompt)

### Attendance save

```
UI (optional sectionIds already known from roster/session)
  → Mark/Edit DTO + optional SectionId / SectionIds
  → AttendanceController
  → FacultySubjectAccess (unchanged)
  → Resolve authorized cohort server-side (AttendanceSectionScope when sections present)
  → Validate ALL submitted student numbers ⊆ authorized set
  → On any miss: reject entire request (no partial insert/update)
  → Persist
```

### Faculty allocation UX

```
Sections Faculty tab
  → Faculty options from existing Staff API (authorized list)
  → Assign via existing POST /faculty-sections
  → Same FacultySectionAssignment entity
```

---

## Files / services involved

### Attendance (read existing; write later)

| Layer | Files |
|-------|--------|
| UI | `AttendanceMarking.tsx`, `attendanceService.ts`, `attendanceMarkingScope.ts`, `operationalTimetableContext.ts` |
| API | `AttendanceController.cs`, `FacultySubjectAccess.cs` |
| Application | `AttendanceSectionScope.cs`, `MarkAttendanceRequest.cs`, `EditAttendanceRequest.cs`, `AttendanceSessionResolver.cs` |
| Tests | Prompt 11–13 unit tests; FE scope tests |

### Faculty allocation UX

| Layer | Files |
|-------|--------|
| UI | `SectionsPage.tsx`, `FacultySectionAllocationPanel.tsx`, `facultySectionAllocationView.ts`, `sectionService.ts`, `setupService.ts` (`listStaff`) |
| API | `FacultySectionsController`, `StaffController` |
| Application | `SectionManagementService`, `SectionDtos` |
| Domain | `FacultySectionAssignment` |

---

## Risks

| Risk | Impact | Mitigation direction |
|------|--------|----------------------|
| Silent skip of unauthorized students on mark | Partial / incomplete attendance; trust of client roster | Fail-closed set validation on write |
| Section scope only on read | Client can omit filter and submit broader IDs | Optional section ids on DTO + server re-scope |
| First-student-only deep validation on mark | Later students may bypass elective/C/G/S checks if query is loose | Authorize full submitted set against subject-derived cohort |
| Edit updates by number without cohort check | Stale / out-of-scope numbers ignored or wrongly applied | Same authorized-set check before update |
| Faculty Id free-text UX | Wrong Staff Id, poor usability | Dropdown/search over existing Staff list |
| Staff list Admin-only vs AssignFaculty permission | Assigners without admin may lack options API | Align read permission without new Faculty model |
| Over-scoping write DTO with combined-class fields | Unnecessary contract churn | Keep combined metadata on roster only |

---

## Recommended implementation sequence

1. **Prompt 2 — Save contract (additive DTOs)**  
   Add optional `SectionId` / `SectionIds` to Mark/Edit DTOs + UI payload passthrough. No eligibility in React.

2. **Prompt 3 — Server write integrity**  
   Shared authorized-cohort builder reused from roster filters; reject entire mark/edit if any submitted student unauthorized; no partial save. Keep timetable optional; Section optional.

3. **Prompt 4 — Tests for write integrity**  
   Cover: no section (legacy), single section, combined sections, unauthorized student → full reject, FacultySubjectAccess still enforced.

4. **Prompt 5 — Faculty Allocation UX**  
   Replace free-text Faculty Id with authorized Staff options from existing Staff API; still POST `/faculty-sections` only.

5. **Prompt 6 — UX tests + docs hardening**  
   FE/BE smoke for picker + regression that Allocation/Scheduling/Resolver untouched.

---

## Explicit non-goals (this discovery)

- No code / behavior changes in Prompt 1  
- No redesign of AttendanceSessionResolver  
- No Subject Master / Section / SectionGroup / TimetableSections / engine changes  
- No new Faculty entity or second FacultySectionAssignment model  

---

## Discovery verdict

| Track | Current state | Smallest next additive step |
|-------|---------------|-------------------------------|
| Attendance save | Roster section-scoped; **writes are not** | Optional section ids on Mark/Edit + server fail-closed student-set validation |
| Faculty allocation UX | Panel + `/faculty-sections` work; faculty picker is raw Staff Id | Authorized Staff options from existing Staff source |

Compatible with Manual cascade when no timetable and no Section.
