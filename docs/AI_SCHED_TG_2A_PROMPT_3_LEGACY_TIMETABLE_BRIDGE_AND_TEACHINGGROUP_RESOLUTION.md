# AI-SCHED-TG.2A Prompt 3 — Legacy Timetable Bridge & TeachingGroup Resolution Contract

**Workstream:** AI-SCHED-TG.2A  
**Prompt:** 3 — Legacy Timetable Bridge & TeachingGroup Resolution Contract  
**Date:** 2026-08-17  
**Type:** DESIGN ONLY  

**Binding inputs:** AI-SCHED-TG.2 Final Decision; AI-SCHED-TG.2A Prompts 1–2  

**No production code, schema, migrations, APIs, UI, permissions, timetable data, or attendance logic were modified.**

---

## 1. Executive Summary

This contract locks how TimetableEntries resolve and attach **TeachingGroups**, how legacy `GET/PUT /api/timetable/{id}/sections` translate into the TeachingGroup application boundary, and how **TimetableSection** remains a one-way projection.

| Rule | Statement |
|---|---|
| SoT | TeachingGroup (not Section, not TimetableSection, not SubjectAllocation) |
| Primary resolve key | `TimetableEntry.TeachingGroupId` |
| SubjectAllocation | Many TeachingGroups allowed; **never** a unique TG lookup key |
| Legacy PUT | Compatibility façade → TeachingGroupSection → project TimetableSection |
| Legacy GET | Read projection / TeachingGroupSection-derived data — **no silent TG create** |
| Auto-create TG | Only on **explicit** operations (legacy PUT with sections, designer create, cutover job) — never on mere GET/read |
| Capacity split | Creates TeachingGroups only — never Sections |
| Pre-production | Disposable TT test data → **clean cutover**, not permanent backfill architecture |

### STATUS: **PASS**

All fifteen readiness criteria in the Final Readiness Gate are unambiguous (§27).

---

## 2. Current legacy timetable behavior

### Inspected implementation

| Artifact | Behavior today |
|---|---|
| `GET /api/timetable/{timetableId}/sections` | `SectionsController` → `GetTimetableSectionsAsync` reads `TimetableSections` by `TimetableId` + tenant |
| `PUT /api/timetable/{timetableId}/sections` | `SetTimetableSectionsAsync` soft-deletes rows for `(TimetableId, TimetableEntryId)`, then **directly** `Add`s new `TimetableSection` rows |
| Request | `SetTimetableSectionsRequest`: `TimetableEntryId`, `SectionIds[]` (`SectionDtos.cs`) |
| UI callers | `sectionService.listTimetableSections` / `setTimetableSections` |
| Attendance | `AttendanceSessionResolver` joins `TimetableSections` for additive `SectionIds` |
| TimetableEntry | No `TeachingGroupId` today; no SectionId on entry |

### Defect relative to target architecture

Legacy PUT is an **independent write model** for operational cohort sections. That must become a façade over TeachingGroup.

---

## 3. Canonical TeachingGroup resolution

### Preferred path (normative)

```text
1. Load TimetableEntry with TenantId == current tenant (fail closed).
2. Read TeachingGroupId.
3. If TeachingGroupId has value:
     a. Load TeachingGroup (tenant-safe).
     b. Verify TenantId match.
     c. Verify TeachingGroup.SubjectAllocationId == Entry.SubjectAllocationId
        (compatibility validation — NOT identity lookup).
     d. Verify academic scope (AcademicYear/Course/Group/Semester/Subject)
        matches entry denormalized fields / allocation.
     e. Verify TeachingGroup not Archived (and not Locked when mutation requested).
     f. USE this TeachingGroup. Do not create another.
4. If TeachingGroupId is null:
     Do NOT invent a TeachingGroup on read.
     Proceed only if the *operation* explicitly permits create/attach (§5, §6).
```

### Forbidden resolution sources (alone)

SubjectAllocationId, SubjectId, SectionId, RoomId, FacultyId/StaffId, timetable text, section code, student counts.

SubjectAllocationId may **validate** that a candidate TG belongs to the same allocation as the entry.

### Multiple compatible TeachingGroups

If `TeachingGroupId` is null and more than one TG under the entry’s SubjectAllocation could fit:

**MUST NOT guess.** Require:

- explicit `TeachingGroupId` in the operation payload, **or**
- an explicit create operation with defined Type/MembershipSource/sections/students, **or**
- validation error: `"TeachingGroupId is required when multiple Teaching Groups exist for this Subject Allocation."`

---

## 4. TimetableEntry → TeachingGroup relationship

| Cardinality | Rule |
|---|---|
| TimetableEntry → TeachingGroup | **Exactly one** (required after clean cutover) |
| TeachingGroup → TimetableEntries | **Zero or many** |

```text
Monday 09:00  → TG-CA-A (Room 101)
Wednesday 09:00 → TG-CA-A (Room 101)
Friday 09:00 → TG-CA-A (Room 203)   // same TG, different occurrences — VALID
```

Do **not** create a new TeachingGroup per timetable occurrence.

---

## 5. Legacy entry conversion

Entries may have `TeachingGroupId = null` during development.

| Trigger | Create/attach TG? |
|---|---|
| GET timetable / GET sections | **No** |
| Attendance resolve (read) | **No** create; may read TimetableSection only |
| Legacy PUT `/sections` with SectionIds | **Yes** — explicit compatibility write (§6) |
| Timetable Designer save entry with TG selected | **Yes** — attach supplied TeachingGroupId |
| Controlled cutover command (ops/admin, pre-prod) | **Yes** — batch explicit conversion |
| Publish timetable | **Fail** if any entry lacks TeachingGroupId after cutover policy enabled |

### Preferred pre-production strategy (APPROVED)

**Clean controlled cutover** before first college:

1. Delete or archive disposable test timetables / recreate under TeachingGroup model, **or**  
2. Run a one-time **explicit** cutover tool (not silent GET inference) that converts remaining draft entries.

Do **not** permanently encode “infer TG on every read.”

---

## 6. Legacy PUT `/sections` translation

### Required flow

```text
Legacy PUT { TimetableEntryId, SectionIds[] }
    ↓
ITimetableTeachingGroupBridge.SetSectionsAsync
    ↓
Resolve TimetableEntry (tenant-safe)
    ↓
If TeachingGroupId != null → use that TG (no new TG)
Else if SectionIds valid for SectionDerived/Combined → explicitly create TG + attach
Else → validation error
    ↓
Replace TeachingGroupSection set
    ↓
ITimetableSectionProjector.ProjectEntryAsync (ONLY writer of TimetableSection)
    ↓
Return existing TimetableSectionDto contract
```

### PUT MUST NOT

- Call `DbContext` Add/Update TimetableSection except inside projector  
- Guess elective/lab/capacity-split from SectionIds alone  
- Bypass Published/Locked governance  
- Mutate `StudentSection`

### GET flow

```text
Prefer: for each entry with TeachingGroupId → SectionIds from TeachingGroupSection
Else (pre-cutover): TimetableSection rows
Never create TeachingGroup on GET
```

---

## 7. SubjectAllocation vs TeachingGroup semantics

| Concept | Role |
|---|---|
| SubjectAllocation | Who teaches which subject in which academic scope (faculty assignment) |
| TeachingGroup | Which operational cohort is taught for that assignment |

**INVALID assumption:** SubjectAllocation → exactly one TeachingGroup  

**VALID:** one SubjectAllocation → many TeachingGroups (lecture bands, labs, electives, splits)

`SubjectAllocationId` on TeachingGroup is required FK for consistency — **not** a unique key for resolution.

---

## 8. Multiple TGs per SubjectAllocation

Example (conceptual test case):

```text
SubjectAllocation = Computer Applications

TeachingGroups:
  TG-CA-A, TG-CA-B, TG-CA-LAB-01, TG-CA-LAB-02, CA-Elective

TimetableEntries:
  Mon 9:00  → TG-CA-A
  Tue 10:00 → TG-CA-A
  Wed 14:00 → TG-CA-LAB-01
  Thu 14:00 → TG-CA-LAB-02
```

System **must not** collapse these into one TeachingGroup because they share SubjectAllocationId.

---

## 9. Multiple TimetableEntries per TG

Valid and expected for recurring periods. Validation:

- Entry.TeachingGroupId required (post-cutover)  
- Entry.SubjectAllocationId must equal TG.SubjectAllocationId  
- Room/slot conflicts remain existing conflict engine concerns  

---

## 10. SectionDerived behavior

| Aspect | Rule |
|---|---|
| Type | `SectionDerived` |
| TeachingGroupSection | **Required** — exactly one Section |
| Membership | Dynamic from `StudentSection` (IsCurrent) |
| Legacy PUT with one SectionId + null TG | Create SectionDerived TG, attach, project |
| StudentSection | Unchanged |

---

## 11. CombinedSections behavior

| Aspect | Rule |
|---|---|
| Type | `CombinedSections` |
| TeachingGroupSection | **Required** — two or more Sections |
| Membership | Union of current StudentSection members |
| Legacy PUT with multiple SectionIds + null TG | Create CombinedSections TG |
| Optional | SectionGroupId reference for admin UX |

---

## 12. StudentSubset behavior

| Aspect | Rule |
|---|---|
| Type | `StudentSubset` |
| TeachingGroupSection | **Optional** parent Section link(s) |
| Membership | Explicit `TeachingGroupMembership` |
| Legacy PUT | **Must not** invent subset from SectionIds alone — validation error unless dedicated subset API used |

---

## 13. Elective behavior

| Aspect | Rule |
|---|---|
| Type | `Elective` |
| TeachingGroupSection | **Prohibited or empty** (no Section required) |
| Membership | StudentSubject (± explicit) with **full scope** validation |
| Scope | Tenant, AcademicYear, Course, Group, Semester, Subject (+ college/tenant conventions) |
| StudentId+SubjectId alone | **Insufficient** |
| Legacy PUT sections | Cannot create elective TG |

Example: French 10 students → SubjectAllocation → French TeachingGroup → 10 students; **no Section**.

---

## 14. Laboratory behavior

| Aspect | Rule |
|---|---|
| Type | `Laboratory` |
| TeachingGroupSection | **Optional** parent Section (often one) |
| Membership | Explicit/Hybrid partition |
| Mutual exclusion | Same `ExclusionGroupKey` among lab batches |
| Lecture + Lab | Compatible for same student (different activity / exclusion keys) |

Example: Section CA-A 60 students → TG-LAB-01 (30) + TG-LAB-02 (30); same SubjectAllocation; **no new Sections**.

---

## 15. CapacitySplit behavior

| Aspect | Rule |
|---|---|
| Type | `CapacitySplit` |
| ExclusionGroupKey | **Required** — shared across siblings |
| ExpectedStudentCount / MaxTeachingCapacity | Per TG (2A Prompt 1) |
| Membership | Explicit/Hybrid partition; one student per ExclusionGroupKey set |
| Sections | **Never** auto-created |
| Room.Capacity | Per TimetableEntry placement |

Example: TG-01 Expected/Max 40; TG-02 Expected/Max 30; academic Section unchanged.

---

## 16. TeachingGroupSection semantics

**Purpose:** express TeachingGroup ↔ academic Section association.  
**Not** a student membership table.

| TeachingGroupType | Section links |
|---|---|
| SectionDerived | **Required** (exactly 1) |
| CombinedSections | **Required** (≥2) |
| StudentSubset | **Optional** parent |
| Elective | **Prohibited** (empty) |
| Laboratory | **Optional** parent |
| CapacitySplit | **Optional** (often inherit parent section for UX) |
| Custom | Optional |

Student sets come from membership resolver / StudentSection / StudentSubject / explicit rows — never from inventing students on TeachingGroupSection.

---

## 17. TimetableSection projection

```text
TeachingGroup → TeachingGroupSection → TimetableSection (projection)
```

For `TimetableEntry.TeachingGroupId = TG-123`:

- SectionIds = TG-123.TeachingGroupSections only  
- Projection **must not invent** SectionIds  
- If TG has zero section links (e.g. Elective): projection may contain **zero** section rows; DTO contract allows empty lists  

Only `ITimetableSectionProjector` writes TimetableSection.

---

## 18. Projection consistency

### Invariant

For every TimetableEntry with TeachingGroupId:

```text
set(TimetableSection.SectionId where EntryId/TimetableId match and not deleted)
  ==
set(TeachingGroupSection.SectionId for Entry.TeachingGroupId and not deleted)
```

### Detect

| Anomaly | Detection |
|---|---|
| Missing projection | SoT has sections; projection empty |
| Stale projection | Sets differ |
| Extra projection rows | Projection ⊃ SoT |
| Missing section links | Entry has TG but SoT empty while legacy expected sections |
| Duplicate projection rows | Same (Entry, Section) twice |
| Cross-tenant projection | TenantId mismatch |
| TeachingGroup mismatch | Projection exists for entry without TG / wrong TG |

**Architecture Guard + automated tests** MUST detect dual-write and set mismatch (implementation in TG.3B+).

---

## 19. Attendance integration

### Preferred future path

```text
TimetableEntry → TeachingGroupId → MembershipResolver → Students
              → TeachingGroupSection → SectionIds (session/UI metadata)
```

### Transition

- If TeachingGroupId present: prefer TG for SectionIds (and later students).  
- If null: existing TimetableSection join (pre-cutover only).  
- **Never** create TG during attendance resolve.

### Unchanged fallback

```text
Course → Group → Semester → Subject → Period
```

No attendance schema break; no forced timetable usage.

---

## 20. Tenant / security rules

Every resolve/bridge/project operation:

- Filters `TenantId == current user tenant`  
- Validates Section/Student/TG/Entry tenant match  
- **Never** uses `IgnoreQueryFilters` to “make resolution work”  
- No cross-tenant TeachingGroup attachment, section links, student membership, or entry attachment  

RBAC: legacy PUT retains existing section/timetable manage authorization; TG APIs use TeachingGroup permissions when introduced. Do not weaken policies.

---

## 21. Governance / lifecycle rules

| TeachingGroup \ Timetable | Draft TT | Published/Locked TT |
|---|---|---|
| Draft / Active TG | PUT sections / membership allowed | N/A until publish attaches |
| Locked TG | Mutations rejected | Mutations rejected |
| Archived TG | Cannot attach to new entries | — |

| Question | Answer |
|---|---|
| Change TeachingGroupId on entry | Draft TT only; Published/Locked → reject (new version/unlock) |
| Change TeachingGroupSection | Same as membership: blocked when TG Locked or TT Published/Locked |
| Legacy PUT when Published | **Rejected** — no governance bypass |
| New ScheduleVersion required? | Yes for published timetable cohort changes (existing governance) |
| Re-approval? | Follow existing ScheduleVersion approval rules |

---

## 22. Idempotency rules

### Must NOT create duplicates on repeat PUT

Repeated identical PUT `{ EntryId, SectionIds=[A] }` when entry already has TG with same single section:

- Update/replace TeachingGroupSection to same set  
- Re-project  
- **Do not** create TG-002, TG-003  

### Must NOT use

`UNIQUE(SubjectAllocationId)` — invalid (many TGs per allocation).

### Approved identity / idempotency

| Situation | Behavior |
|---|---|
| Entry.TeachingGroupId set | Always reuse that TG |
| Entry.TeachingGroupId null + PUT sections | Create **one** TG for this explicit operation; attach to entry; subsequent PUTs reuse entry’s TG |
| Designer selects existing TG | Attach ID; no create |
| Capacity-split API | Creates N new TGs with shared ExclusionGroupKey — intentional, not duplicates |
| Optional natural key | `(TenantId, SubjectAllocationId, Type, Name)` unique among non-deleted — prevents accidental same-name clones; does **not** collapse labs vs lectures |

---

## 23. Test-data cutover strategy

| Item | Guidance |
|---|---|
| Existing TT rows | Disposable; may be deleted/recreated before first college |
| Permanent migration to preserve test TT | **Not required** |
| Old TimetableSection rows | Recreated by projector after TG attach; orphans removable in cutover |
| Entries without TeachingGroupId | Invalid after cutover flag; convert via explicit PUT/cutover or delete |
| Production after onboarding | Never mass-delete live timetables; versioning/archive only |

**This prompt performs no deletion.**

---

## 24. Architecture Guard rules

### FORBIDDEN

```text
Controller / feature Application code
  → DbContext.TimetableSections.Add / Update / hard-delete / soft-delete
```

except inside designated `ITimetableSectionProjector` implementation.

### ALSO FORBIDDEN

- Treating SubjectAllocationId as unique TeachingGroup  
- Post-cutover TimetableEntry without TeachingGroupId  
- Dual TimetableSection mutation outside projector  
- Cross-tenant resolution  
- Silent TG create on GET  
- TeachingGroup membership mutating StudentSection  
- Auto-creating academic Sections from capacity  

### ALLOWED

```text
TeachingGroup application services
  → TeachingGroup / TeachingGroupSection / Membership
  → ITimetableSectionProjector
```

---

## 25. Rejected alternatives

| Alternative | Why rejected |
|---|---|
| Infer TG from SubjectAllocation on GET | Non-unique; silent side effects |
| Bidirectional TimetableSection ↔ TG sync | Dual SoT |
| Keep direct PUT TimetableSection writes | Dual write |
| Unique SubjectAllocation → TG | Breaks labs/splits/multiple lectures |
| New TG per TimetableEntry occurrence | Explodes cohorts |
| Capacity split → new Sections | Violates Section ≠ TG |
| Permanent legacy inference layer for test data | YAGNI pre-prod |

---

## 26. Implementation impact on AI-SCHED-TG.3+

| Slice | Work |
|---|---|
| TG.3 | Entities: TeachingGroup, TeachingGroupSection, Membership; Entry.TeachingGroupId; capacity fields per 2A.1 |
| TG.3A | `ITeachingGroupMembershipResolver` |
| TG.3B | `ITimetableTeachingGroupBridge` + Projector; refactor `SetTimetableSectionsAsync` |
| TG.3C | AttendanceSessionResolver TG path |
| TG.4+ | Public TG APIs + Designer TeachingGroupId selection |
| Guards/tests | Dual-write + projection consistency + multi-TG-per-allocation cases |

---

## 27. Final Readiness Gate

| # | Criterion | Met? |
|---|---|---|
| 1 | TeachingGroup sole operational cohort SoT | **Yes** |
| 2 | TimetableSection projection/compatibility only | **Yes** |
| 3 | SubjectAllocation not unique TG key | **Yes** |
| 4 | Entry resolves TG via TeachingGroupId | **Yes** |
| 5 | Multiple TGs per SubjectAllocation | **Yes** |
| 6 | Multiple Entries per TG | **Yes** |
| 7 | Legacy null TeachingGroupId explicit conversion path | **Yes** (PUT / cutover / designer — not GET) |
| 8 | Legacy PUT cannot directly mutate TimetableSection | **Yes** (façade + projector only) |
| 9 | TeachingGroupSection not student-membership source | **Yes** |
| 10 | Attendance fallback preserved | **Yes** |
| 11 | Tenant isolation preserved | **Yes** |
| 12 | Governance cannot be bypassed | **Yes** |
| 13 | Idempotent application semantics (no SubjectAllocation unique) | **Yes** |
| 14 | Capacity split never creates Sections | **Yes** |
| 15 | Disposable TT data does not force migration architecture | **Yes** |

---

## STATUS: **PASS**

All gate criteria are unambiguous for AI-SCHED-TG.3 implementation planning.

**Explicit confirmation:** This prompt produced **design documentation only**. No migrations, database changes, API changes, UI changes, or production code changes were made.
