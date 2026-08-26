# AI-SCHED-TG.1 — Existing Scheduling Architecture & Teaching Group Integration Assessment

**Workstream:** AI Scheduling Enhancement — Teaching Group  
**Prompt:** 1 — Assessment only (read-only)  
**Date:** 2026-08-17  
**Status:** Assessment complete — **no production behavior changed**

---

## 1. Executive Summary

Abhyanvaya already has a large, tenant-scoped **AI30 Scheduling** stack (configuration hub, resources, subject allocation, timetable designer, faculty/student/room views, governance, validation, conflicts, optimization) plus **AI29 academic Section** operations.

**Critical finding:** `TimetableEntry` does **not** carry `SectionId`. Sections attach through the junction **`TimetableSection`**. Combined teaching is already partially modeled by **`SectionGroup` + `SectionGroupMember` + `TimetableSection`**. There is **no** entity named Teaching Group, Student Group, Delivery Group, Lab Batch, or Allocation Group.

| Principle | Implication for this codebase |
|---|---|
| Section = academic/student organization | Keep `Section` / `StudentSection` / section capacity / allocation engine unchanged |
| Teaching Group = scheduling/teaching construct | Belongs in the **scheduling / operational-class** layer, not as a synonym for Section |
| Timetable targets Teaching Group | Today the entry targets **SubjectAllocation + resources**; section membership is a **side map** |

**Minimum-change recommendation:** Introduce a first-class **Teaching Group** aggregate in a later prompt that:

1. Becomes the **authoritative teaching cohort** for a timetable placement.
2. **Reuses** SubjectAllocation (who teaches what in which academic scope), Timetable/TimetableEntry (when/where), TimetableSection/SectionGroup (section-derived & combined cases), StudentSubject (elective membership).
3. **Does not** duplicate Timetable Designer, Governance, Validation, Optimization, Subject Allocation, or Section Management modules.
4. Supports room-capacity splits as **multiple Teaching Groups**, never as automatic Section creation.

**Do not** treat `SectionGroup` alone as Teaching Group — it is Section-centric and cannot represent “French with 10 students and no Section.”

---

## 2. Existing Scheduling Architecture

Scheduling is organized as **AI30 phases** under `Abhyanvaya.Domain/Entities/Scheduling`, Application services under `Abhyanvaya.Application/Scheduling`, API under `Abhyanvaya.API/Controllers/Scheduling`, UI under `abhyanvaya-ui/src/pages/setup/scheduling`.

| Area | Existing implementation |
|---|---|
| Configuration hub | `/setup/scheduling` → `SchedulingHub` |
| Academic Years / Terms | `AcademicYear`, `AcademicTerm` + calendar controllers |
| Working Days | `WorkingDay` |
| Holiday Calendar | `Holiday`, `HolidayTypeCatalog` |
| Campus Facilities | `Campus`, `Building`, `Floor` |
| Rooms / Features / Availability | `Room`, `RoomFeature`, `RoomFeatureAssignment`, `RoomAvailability` |
| Time Slots / Templates | `TimeSlot`, `TimeSlotSet`, `TimeSlotTemplate` |
| Subject Categories / Delivery | `SubjectCategory`, `SubjectDeliveryType` (+ fields on `Subject`) |
| Room Rules | `RoomAllocationRule` |
| Faculty Availability / Preferences / Workloads | `FacultyAvailability`, preference entities, `FacultyWorkload` |
| Subject Allocation | `SubjectAllocation` + `/setup/scheduling/subject-allocations` |
| Schedule Versions | `ScheduleVersion` + governance APIs |
| Timetable Designer | `Timetable` / `TimetableEntry` + `/setup/scheduling/timetables/:id` |
| Faculty / Student / Room Timetable | Projection pages under `/setup/scheduling/timetable-*` |
| Governance | Approvals, publish/freeze/archive, change history, clone jobs |
| Validation | Soft validation + conflict engine |
| Optimization | Engine + sandbox + scenarios |

ADL documentation is primarily `docs/AI30_*`, `docs/AI29_*`, and in-app `abhyanvaya-ui/public/docs/scheduling/modules/*`. No ADL document defines a **Teaching Group** entity; combined teaching is documented via **SectionGroup / TimetableSections**.

---

## 3. Existing Timetable Data Model

### Aggregates

| Entity | Table / role |
|---|---|
| `Timetable` | Versioned schedule container (status, freeze/archive, `ScheduleVersionId`) |
| `TimetableEntry` | One cell: day + slot + allocation + staff + room + denormalized Course/Group/Semester/Subject |
| `ScheduleVersion` | Version numbering, publish/archive, current flag |
| `TimetableSection` | Maps timetable / optional entry → **Section** |

### `TimetableEntry` references (A)

| Concern | How referenced |
|---|---|
| Subject | `SubjectId` (also via `SubjectAllocationId`) |
| Course / Group / Semester | Denormalized FKs from allocation (`CourseId`, `GroupId`, `SemesterId`) |
| Section | **Not on entry** — via `TimetableSection` |
| Faculty | `StaffId` |
| Room | `RoomId` |
| TimeSlot | `TimeSlotId` |
| SubjectAllocation | **Required** `SubjectAllocationId` |

File: `Abhyanvaya.Domain/Entities/Scheduling/TimetableEntry.cs`

### `TimetableSection`

```text
TimetableId + optional TimetableEntryId + SectionId
```

Supports one entry → many sections (combined A+B+C). File: `Abhyanvaya.Domain/Entities/Academic/TimetableSection.cs`

---

## 4. Existing Student / Subject / Section Relationships

| Model | Role |
|---|---|
| `StudentSection` | Authoritative administrative Section membership (effective dates, current flag) |
| `StudentSubject` | Simple student ↔ subject enrollment (`StudentId`, `SubjectId`) |
| `Subject.IsElective` / `ElectiveGroupId` | Elective catalog flags; elective **group** is curriculum, not teaching cohort |
| `ElectiveGroup` | Elective bucket under Course/Group/Semester |
| Lab batches | **No** first-class entity; lab signaled by `LabRequired`, delivery types (e.g. PRACT), room type rules |
| Combined classes | `SectionGroup` + members + `TimetableSection` + attendance multi-section |

**Answers (B):**

- Section membership: **`StudentSection`**
- Subject enrollment: **`StudentSubject` exists** (simple; not rich elective enrollment history)
- Electives: **catalog + `StudentSubject`**, not Teaching Group
- Lab batches: **not modeled** as cohorts
- Combined classes: **supported** via SectionGroup / TimetableSections / attendance session sections

---

## 5. Existing Subject Allocation Model

`SubjectAllocation` binds:

```text
AcademicYear + Subject + Staff + Course + Group + Semester + Department
+ WeeklyHours + PreferredRoom? + LabRequired + AI/Attendance flags + effective dates
```

**No Section, no student list, no room-capacity cohort size.**

**Answer (C):** Subject Allocation answers “who teaches which subject in which curriculum scope,” **not** “which students sit in which teaching cohort.” It is a **necessary input** to derive a default Teaching Group (scope + subject + faculty), but **not sufficient** for electives-without-section, lab batches, capacity splits, or combined teaching membership.

---

## 6. Existing Timetable Designer Flow

UI: `/setup/scheduling/timetables/:id` (`TimetableDesignerPage`, grid, entry dialog).

Typical flow:

1. Choose Subject Allocation (implies Course/Group/Semester/Subject/Staff).
2. Place on Day + TimeSlot + Room.
3. Soft warnings / conflicts (capacity, double booking, etc.).
4. Optional section mapping via timetable sections API (`GET/PUT /api/timetable/{id}/sections`).
5. Lifecycle: draft → review/approve → publish/freeze via governance.

**Answer (D):** The designer has an **implicit operational class** (entry + optional multi-section map) but **no** named Teaching Group / Batch / Delivery Group entity. Closest equivalents:

| Concept | Fit as Teaching Group? |
|---|---|
| `TimetableEntry` | Slot placement — not membership |
| `SubjectAllocation` | Teaching assignment — not membership |
| `TimetableSection` | Section membership bridge — **partial** (section-only) |
| `SectionGroup` | Combined **sections** — **partial** (section-only) |
| Curriculum `Group` | Specialization stream — **not** a teaching cohort |
| Conflict “student batch” | Course+Group+Semester identity — **not** an entity |

**Do not** rename SectionGroup to TeachingGroup without extending membership beyond Sections.

---

## 7. Existing Attendance Integration

Documented in `docs/AI30_PHASE2B_ATTENDANCE_RESOLUTION.md`; implemented by `AttendanceSessionResolver`.

| Mode | Path |
|---|---|
| Legacy (fallback) | Faculty → Course → Group → Semester → Subject → Period → Students |
| Timetable (optional) | Published timetable → Staff + day + slot → Course/Group/Semester/Subject (+ room) → additive `SectionIds` from TimetableSections |

**Non-negotiable for Teaching Group work:**

- Must **not** break Legacy fallback.
- Timetable enrichment must remain **additive**.
- Manual override of attendance context must remain.
- Combined classes already use TimetableSections / `AttendanceSessionSection`.

Teaching Group membership should eventually **feed** attendance student resolution (sections and/or enrolled students) without replacing Course→Group→Semester→Subject→Period as the fallback spine.

---

## 8. Existing Room Capacity Handling

| Layer | Behavior |
|---|---|
| `Room.Capacity` | Integer capacity on room |
| `Subject.ExpectedCapacity` | Optional expected class size |
| Soft validation | `ROOM_CAPACITY` if room capacity &lt; expected |
| Conflict rule | `RoomCapacityExceededRule` with margin percent |
| Room rules | Min/Max capacity + features/types |
| Section capacity | AI29 `Section` strengths — **not** wired into TimetableEntry |

**Answer (F):** Room capacity is already a **scheduling constraint**. Capacity pressure must drive **Teaching Group splits** (or room choice), **never** automatic Section creation. Today there is no first-class way to express “CA Group 1 = 40 / CA Group 2 = 30” under one subject without inventing Sections or overloading ExpectedCapacity alone.

---

## 9. Existing Governance Model

| Capability | Existing |
|---|---|
| Schedule versions | `ScheduleVersion` |
| Approvals | `TimetableApprovalRequest` / steps / history / comments |
| Publish / freeze / unlock / archive | Timetable lifecycle services |
| Change history | `TimetableChangeHistory`, decision history |
| Clone | `TimetableCloneJob` + background worker |
| Validation / conflicts | Soft validation + conflict runs |
| Optimization | Separate engine/sandbox — reads schedule, does not redefine cohorts |

Teaching Group CRUD and membership changes that affect published placements must eventually flow through **existing** versioning/approval/history — not a parallel governance stack.

---

## 10. Existing Authorization / Tenant Model

- Scheduling entities inherit `BaseEntity.TenantId`.
- Repositories/services filter by current tenant.
- Permissions: `Scheduling.*`, Timetable, Version, Review, Approve, Publish, Archive, Clone, Conflict, Freeze/Unlock, etc. (`PermissionKeys` / `AuthorizationPolicies`).
- Section operations use separate `Section.*` / lifecycle permissions.
- Attendance resolution uses `CanManageAttendance`.

Teaching Group must be **tenant-scoped** and permission-gated under Scheduling (and, where membership touches Sections/Students, respect those policies). No `IgnoreQueryFilters` / cross-tenant shortcuts.

---

## 11. Existing Equivalent Concepts (if any)

| Requirement example | Today’s coverage |
|---|---|
| 1. Section-derived subject (SCA-01 / Financial Accounting) | SubjectAllocation + TimetableEntry + optional TimetableSection(SCA-01) |
| 2. Small elective, no Section | SubjectAllocation possible; **no** teaching cohort; attendance via subject enrollment / manual |
| 3. 30 students, room 40 — one cohort | Implicit via ExpectedCapacity vs Room.Capacity; **no** named TG |
| 4. 70 students, room 40 — two cohorts | **Not modeled** without two Sections or two allocations/entries without shared membership semantics |
| 5. Combined A+B | **SectionGroup + TimetableSection** |
| 6. Lab batches inside a Section | **Not modeled** as cohorts |

**Partial equivalent:** `TimetableSection` + `SectionGroup` cover **section-linked** teaching only.  
**Missing:** subject/elective-only cohorts, lab batches, capacity-based multi-cohorts without Sections.

---

## 12. Teaching Group Integration Options

### Option A — Overload Section / invent “fake sections”

Reject. Violates Section ≠ Teaching Group; pollutes academic allocation/attendance.

### Option B — Treat SubjectAllocation as Teaching Group

Reject as sole model. No membership, no multi-cohort splits, no combined sections.

### Option C — Extend only TimetableSection / SectionGroup

Insufficient for electives/lab/capacity splits without Sections. Keep as **adapters** for section-derived/combined cases.

### Option D — New Teaching Group aggregate (recommended)

Introduce scheduling-layer `TeachingGroup` (+ membership) that TimetableEntry references.

| Type | Membership source |
|---|---|
| `SectionDerived` | One Section (wraps today’s TimetableSection pattern) |
| `CombinedSections` | Many Sections / SectionGroup |
| `SubjectElective` | Students via `StudentSubject` (and/or explicit roster) |
| `LabBatch` | Subset of a Section’s students (explicit roster or rule) |
| `CapacitySplit` | Explicit roster or size cap under same SubjectAllocation |

SubjectAllocation remains the faculty/subject/scope assignment; Teaching Group is **who is taught in the slot**.

### Option E — Entry-only “virtual” groups (no entity)

Reject for governance/audit/attendance reproducibility.

---

## 13. Recommended Integration Point

**Primary integration point:** between **SubjectAllocation** and **TimetableEntry**, with membership bridging academic identities.

```text
SubjectAllocation (who teaches what / scope)
        ↓
TeachingGroup (teaching cohort: type + membership + planned size)
        ↓
TimetableEntry (when / where / which TG)
        ↓
Attendance resolution (Legacy unchanged; Timetable mode resolves students via TG membership → SectionIds and/or StudentIds)
```

**Reuse:**

- Timetable Designer grid/lifecycle
- Governance / versions / conflicts / optimization
- Subject Allocation UI/API
- SectionGroup / TimetableSection as **section membership backends** for SectionDerived/Combined
- StudentSubject for elective membership
- Room capacity rules (validate TG planned size vs room)

**Minimum necessary change (future prompts):**

1. Define TeachingGroup domain contract aligned to ADL (no new parallel scheduler).
2. Add TeachingGroup (+ membership) persistence.
3. Add optional/required `TeachingGroupId` on TimetableEntry (backward compatible defaulting from TimetableSection).
4. Designer: select/create Teaching Group when placing entry.
5. Attendance resolver: enrich students from TG without removing Legacy.
6. Capacity split: create N Teaching Groups under one allocation — **never** N Sections.

---

## 14. Entities / Tables / APIs That Would Need Modification (future)

| Area | Likely change |
|---|---|
| Domain | New `TeachingGroup`, `TeachingGroupMember` (or equivalent) |
| `TimetableEntry` | Add `TeachingGroupId` (nullable initially) |
| Timetable services / DTOs / validators | Accept TG on create/update/move |
| Timetable Designer UI | TG picker / create wizard |
| Attendance resolver | Map TG → students/sections |
| Soft validation / conflicts | Compare Room.Capacity to TG planned size (not only Subject.ExpectedCapacity) |
| Permissions | Extend Scheduling permissions for TG manage/view |
| Projections | Faculty/Student/Room timetable labels show TG name |

Exact schema is deferred to an approved implementation prompt.

---

## 15. Entities / Tables / APIs That MUST NOT Be Duplicated

Do **not** create parallel modules for:

- Scheduling configuration hub
- Timetable Designer / Timetable / TimetableEntry engines
- Faculty / Student / Room Timetable products
- Schedule Version / Governance / Approval / Publish
- Conflict Validation / Optimization engines
- Subject Allocation engine
- Section Management / Section Allocation engine
- Campus / Room / TimeSlot masters (except TG references them)

Do **not** duplicate Section as Teaching Group.

---

## 16. UI Changes Likely Required (future)

| Surface | Likely change |
|---|---|
| Timetable entry dialog | Select Teaching Group (or auto-suggest SectionDerived from sections) |
| New lightweight TG admin | Create SectionDerived / Elective / Lab / Capacity-split groups |
| Subject Allocation page | Optional “default Teaching Groups” — **not** a second allocation module |
| Attendance marking | Continue soft pre-fill; show operational class / TG label if present |
| Student timetable | Filter by TG membership when available |

No UI changes in this assessment prompt.

---

## 17. Backward Compatibility Considerations

1. Existing entries without TeachingGroup continue to work (nullable FK + TimetableSection path).
2. Legacy attendance fallback unchanged.
3. Published timetables remain valid; TG backfill can create SectionDerived groups from TimetableSection rows.
4. Section Allocation / AI29 section capacity workflows unchanged.
5. Curriculum `Group` naming remains distinct from Teaching Group in UI copy.

---

## 18. Migration / Data Considerations (future only)

| Topic | Guidance |
|---|---|
| This prompt | **No migrations** |
| Future backfill | For each TimetableSection set on an entry → TeachingGroup type SectionDerived/Combined |
| Elective history | May lack section maps; TG created when colleges adopt elective cohorts |
| Capacity splits | New data only; no rewrite of Sections |
| Tenant isolation | All new tables `TenantId` + indexes |

---

## 19. Test Impact (future)

| Suite area | Impact |
|---|---|
| Timetable designer / entry CRUD | Extend with TG |
| Soft validation / ROOM_CAPACITY | Planned size from TG |
| Attendance resolution | Timetable mode membership; Legacy unchanged |
| Combined section / TimetableSections | Still pass; TG wraps or coexists |
| Architecture Guard | New entity must respect tenant + layering |
| Scheduling/AI30/AI22/AI31 regressions | Must stay green |
| Section Allocation | **No** behavioral change expected |

---

## 20. Risks

| Risk | Mitigation |
|---|---|
| Conflating Teaching Group with Section | Explicit types; forbid auto-Section creation from capacity |
| Conflating with curriculum Group | Naming/UI: “Teaching Group” vs “Specialization Group” |
| Breaking attendance | Additive resolver only; preserve Legacy |
| Dual membership sources (Section vs StudentSubject) | Clear precedence rules in design prompt |
| Governance bypass | TG changes on published TT require existing approval path |
| Over-building TG admin | Start with Timetable-entry-linked creation |

---

## 21. Open Questions (for Chief Architect)

1. Should `TeachingGroupId` on `TimetableEntry` become **required** after migration, or remain optional indefinitely?
2. For electives, is membership **always** `StudentSubject`, or also explicit roster overrides?
3. Should lab batches be subsets of one Section only, or cross-section?
4. Is planned size on Teaching Group authoritative for ROOM_CAPACITY vs `Subject.ExpectedCapacity`?
5. Should capacity splits share one SubjectAllocation or require distinct allocations per faculty load accounting?
6. How should Student Timetable resolve students who belong to multiple TGs for the same subject?
7. Does Operations.View / Technical Details need TG diagnostics, or admin UX only?

---

## 22. Recommended Next Implementation Prompt

**AI-SCHED-TG.2 — Teaching Group Domain Contract & Backward-Compatible Schema Design**

Scope (design + approved schema only after Architect sign-off):

1. Formal ADL-aligned Teaching Group types and membership rules.
2. Mapping matrices for all six examples in this prompt.
3. Exact relationship to SubjectAllocation, TimetableEntry, TimetableSection, SectionGroup, StudentSubject.
4. Attendance resolution contract (additive).
5. Capacity-split rules without Section creation.
6. Authorization matrix.
7. Migration/backfill strategy from TimetableSection.
8. Explicit non-goals: no new Timetable Designer product, no Governance fork, no Section Allocation changes.

**Stop here.** Do not implement without Chief Architect approval.

---

## Success Criteria Checklist

| Criterion | Assessment answer |
|---|---|
| Where TG belongs | Scheduling/operational layer between SubjectAllocation and TimetableEntry |
| Existing partial equivalent | TimetableSection + SectionGroup (section-only) |
| Minimum changes | New TG aggregate + entry FK + designer/attendance adapters |
| Reuse | Entire AI30 stack, Subject Allocation, SectionGroup/TimetableSection, StudentSubject, room capacity rules |
| Must not change | Duplicate modules listed in §15; Legacy attendance; Section semantics |
| Support Section/Elective/Combined/Lab | Via TG types + membership sources |
| Room capacity → multiple TGs | Yes; never auto-create Sections |
| Backward compatible | Nullable TG + TimetableSection backfill + Legacy attendance |

---

## Explicit Statement

**No production code, database schema, APIs, UI, attendance behavior, or migrations were created or modified.** This document is an architectural assessment only.
