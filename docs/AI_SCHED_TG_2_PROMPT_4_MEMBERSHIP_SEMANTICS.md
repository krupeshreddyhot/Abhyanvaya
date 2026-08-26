# AI-SCHED-TG.2 Prompt 4 — Teaching Group Membership Semantics

**Workstream:** AI-SCHED-TG.2  
**Prompt:** 4 — Membership semantics (design only)  
**Date:** 2026-08-17  

**Principle:** Academic membership (`StudentSection`) ≠ operational teaching membership (`TeachingGroup`).

A student may simultaneously have:

- Academic Section: CA-A  
- Teaching Group: French-01  
- Teaching Group: ComputerApplications-Lecture-01  
- Teaching Group: ComputerApplications-Lab-02  

**No production code/schema/API/UI changes.**

---

## Membership modes

### A. Section-derived (`SectionDerived` + source `Section`)

| Aspect | Rule |
|---|---|
| Source of truth | `StudentSection` where `SectionId` = TG’s primary section and `IsCurrent` |
| Materialization | **None** (dynamic) |
| Refresh | Always live at resolve time |
| Conflict | N/A for duplicates; student leaving section leaves TG automatically |
| Audit | TG section link audited; student roster not copied |
| Effective date | StudentSection EffectiveFrom/To |
| Timetable impact | Entry references TG; cohort follows section |
| Attendance | Resolve StudentIds via StudentSection for linked section |

### B. Explicit student membership (`ExplicitStudents`)

| Aspect | Rule |
|---|---|
| Source of truth | `TeachingGroupMembership` Include rows, IsCurrent |
| Materialization | **Materialized** |
| Refresh | Manual (or admin “sync from selection” action) |
| Conflict | Unique (TG, Student, Include); Exclude rows for Hybrid |
| Audit | Membership row Created/Updated |
| Effective date | On membership row |
| Timetable | Authoritative roster for entry |
| Attendance | Membership → StudentIds |

### C. Combined-section (`CombinedSections`)

| Aspect | Rule |
|---|---|
| Source of truth | Union of `StudentSection` for all `TeachingGroupSection` rows |
| Materialization | Dynamic |
| Refresh | Live |
| Optional | `SectionGroupId` reference for admin UX |
| Attendance | Union of section students; SectionIds list for session |

### D. Rule-derived (`StudentSubject` source)

| Aspect | Rule |
|---|---|
| Source of truth | `StudentSubject` ∩ academic scope filters (Course/Group/Semester via Student) |
| Materialization | Dynamic until Locked; optional snapshot on Lock |
| Refresh | Live while Active/Draft |
| Use | Elective default |
| Attendance | Resolve enrolled students |

### E. Hybrid

| Aspect | Rule |
|---|---|
| Base | Section or StudentSubject dynamic set |
| Overlay | Explicit Include / Exclude membership rows |
| Resolve order | (Base ∪ Includes) − Excludes |
| Use | Lab batches, capacity splits, section subsets |

---

## Dynamic vs materialized vs hybrid

| Mode | Calculation |
|---|---|
| SectionDerived / Combined / StudentSubject | Dynamic |
| ExplicitStudents | Materialized |
| Hybrid | Hybrid |
| On TeachingGroupStatus.Locked | Optionally **freeze snapshot** into membership Includes for audit reproducibility (recommended for Elective/StudentSubject when first published) |

**Attendance must never rely on timetable text/UI.** Resolution path:

```text
TimetableEntry → TeachingGroupId → Membership resolver → Student[]
(+ SectionIds when section-linked for operational class UI)
```

Fallback unchanged:

```text
Course → Group → Semester → Subject → Period → Attendance
```

---

## Concrete examples

### French — 10 students, no Section

- Type: `Elective`
- Source: `StudentSubject` (or Explicit if roster curated)
- TeachingGroupSection: empty
- PlannedCapacity: 10

### CA — 30 students, room 40

- Type: `SectionDerived`
- One TeachingGroup; PlannedCapacity 30
- Room.Capacity 40 → validation OK

### CA — 70 students, teaching capacity 40 → 40 + 30

- Two TeachingGroups, Type `CapacitySplit`, same SubjectAllocation
- Source: `Hybrid` or `ExplicitStudents`
- **Do not** create two Sections
- Two TimetableEntries (possibly different rooms/slots)

### Laboratory — 60 students → Lab-01 / Lab-02 (30+30)

- Type: `Laboratory`
- Parent section linked via TeachingGroupSection
- Membership: Hybrid/Explicit partition
- Same SubjectAllocation; possibly different slots/rooms

### Combined sections A+B

- Type: `CombinedSections`
- TeachingGroupSection: A, B
- Dynamic union
- Aligns with today’s TimetableSection multi-map

### Section subset

- Type: `StudentSubset`
- TeachingGroupSection: parent Section
- Explicit Includes of subset students
- Does not change StudentSection of non-members

---

## Conflict / duplicate rules

1. Same student twice in one TG (active Include) → validation error.
2. Same student in two CapacitySplit groups of **same SubjectAllocation** for overlapping effective dates → **allowed only if** product policy permits (default: **forbidden** for same subject split sets to avoid double-teaching).
3. Same student in Lecture TG + Lab TG for same subject → **allowed**.
4. Cross-tenant / cross-scope student → forbidden.

---

## Confirmation

**No implementation performed.**
