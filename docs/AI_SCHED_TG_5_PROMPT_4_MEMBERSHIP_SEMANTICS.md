# AI-SCHED-TG.5 Prompt 4 — Teaching Group Membership Semantics

**Date:** 2026-08-19  
**Type:** Architecture / semantics (no mutation implementation)  
**Supersedes ambiguity for implementation;** aligns with TG.2 Prompt 4 + TG.2A Prompt 1  

**STATUS: DEFINED** (Hybrid: Model B — APPROVED)

---

## 1. Supported membership sources

Persisted on `TeachingGroup.MembershipSource`:

### 1.1 Explicit (`ExplicitStudents`)

Students are assigned only via `TeachingGroupMembership` rows with `Inclusion = Include` and `IsCurrent = true`.

```text
Resolved = distinct StudentId from current Include memberships
           (Exclude rows are invalid / ignored for pure Explicit — see §3)
```

### 1.2 Section-derived (`Section`)

Membership is derived from the single linked section (`TeachingGroupSection` cardinality enforced by Type rules for `SectionDerived`).

```text
Resolved = distinct StudentId from StudentSection
           where SectionId ∈ TeachingGroupSection
             and IsCurrent and not deleted
             and tenant/academic filters pass
```

No materialization of the full roster into `TeachingGroupMembership` for this source (unless Lock snapshot — §8).

### 1.3 Combined-sections (`CombinedSections`)

```text
Resolved = union of StudentSection for all TeachingGroupSection.SectionIds
           (distinct StudentId)
```

### 1.4 Student-subject (`StudentSubject`)

```text
Resolved = distinct StudentId from StudentSubject matching TeachingGroup.SubjectId
           AND student satisfies full academic-scope eligibility (§6)
```

### 1.5 Hybrid (`Hybrid`) — Model B (APPROVED)

```text
Base =
  if TeachingGroupSection has ≥1 active link:
      union of StudentSection for those sections
  else:
      StudentSubject pool for TeachingGroup.SubjectId + academic scope

Resolved = distinct ( (Base ∪ ExplicitIncludes) − ExplicitExcludes )
```

Where:

- **ExplicitIncludes** = current `TeachingGroupMembership` with `Inclusion = Include`
- **ExplicitExcludes** = current `TeachingGroupMembership` with `Inclusion = Exclude`

**Entity sufficiency:** Existing `Inclusion` enum + unique current-row index **is sufficient**. No schema change required for Hybrid Model B.

**Rejected:** Model A (`Base ∪ Explicit` without removals) — insufficient for lab/capacity-split overlays already anticipated by TG.2.

---

## 2. ResolvedStudentCount

```text
ResolvedStudentCount = count(distinct StudentId from MembershipResolver(TeachingGroup))
```

| Rule | Decision |
|---|---|
| Persist? | **No** |
| Duplicates | Deduplicate by StudentId |
| Ordering | Undefined for count; list APIs may order by StudentId ascending |
| Soft-deleted / inactive students | Excluded (respect Student soft-delete / IsDeleted filters) |
| Non-current StudentSection / membership | Excluded |
| Tenant | Ambient tenant filters only |
| Academic scope | Eligibility filter on derived + explicit acceptance |
| Section overlap | Union then distinct |
| Multiple TGs | Allowed subject to exclusion rules (§4) |

**Implementation note:** TG.5 Prompt 2 list/detail currently counts Include rows only. Future membership implementation **must** align ResolvedStudentCount with this resolver for all sources.

---

## 3. Explicit Include / Exclude usage

| MembershipSource | Include rows | Exclude rows |
|---|---|---|
| ExplicitStudents | Required for members | **Not used** — reject create of Exclude |
| Section / CombinedSections / StudentSubject | **Not used** for normal ops (dynamic) | **Not used** |
| Hybrid | Optional additions | Optional removals from Base |

---

## 4. Multiple Teaching Groups & uniqueness

### Confirmed valid

```text
SubjectAllocation
       ├── TeachingGroup A
       ├── TeachingGroup B
       └── TeachingGroup C
```

### Student uniqueness decision (APPROVED)

| Scope | Rule |
|---|---|
| Global Student + SubjectAllocation = one TG | **REJECTED** — not a business invariant |
| Per Teaching Group | At most one **current** membership row per Student (DB unique index) |
| Per ExclusionGroupKey | When `ExclusionGroupKey` is non-null: student may belong to **at most one** Active/Locked TG sharing `(TenantId, SubjectAllocationId, ExclusionGroupKey)` |
| Lecture + Lab | **Allowed** when ExclusionGroupKey is null or different (ActivityKind may differ) |
| Unrestricted otherwise | Student may appear in multiple TGs for same SA when exclusion key does not forbid it |

Authoritative helper: `TeachingGroupRules.EnsureStudentNotInMutuallyExclusiveGroup`.

---

## 5. Section change effects (by source)

| Event | Explicit | Section / Combined | Hybrid |
|---|---|---|---|
| Section added to TG | No roster change | Resolved **increases** (live) | Base grows; Excludes still apply |
| Section removed from TG | No roster change | Derived students **leave** resolved set | Base shrinks; Explicit Includes may keep student if still included |
| Student moves Section A→B | Unchanged | Follows StudentSection live | Follows Base live; Explicit overlay unchanged |
| Student leaves all linked sections | Unchanged | Leaves resolved set | Leaves Base; remains if Explicit Include |

**Never** rewrite StudentSection from TG membership APIs.

---

## 6. Eligibility (who may be a member)

Server-authoritative checks before accepting an **explicit** Include (and when validating derived eligibility for display/ops):

| Dimension | Rule |
|---|---|
| Tenant | Student.TenantId = TeachingGroup.TenantId |
| AcademicYear | Compatible with TeachingGroup.AcademicYearId (existing student/enrollment conventions) |
| Course | Match TeachingGroup.CourseId |
| Group (curriculum) | Match TeachingGroup.GroupId |
| Semester | Match TeachingGroup.SemesterId |
| Subject | Match TeachingGroup.SubjectId (StudentSubject and/or allocation context) |
| College | Follow existing tenant/college student scoping; TeachingGroup has no CollegeId column |

**Forbidden inference from:** room, timetable, TG name/code alone, section code alone.

---

## 7. Capacity integration (TG.2A preserved)

| Field | Role | Membership mutation |
|---|---|---|
| ExpectedStudentCount | Planning intent | Must **not** auto-change |
| MaxTeachingCapacity | Teaching ceiling | Add that would make Resolved > Max → **Error** |
| ResolvedStudentCount | Derived | Recomputed; never written by client |
| Room.Capacity | Physical | **N/A at membership-time**; schedule warn / publish error (TG.2A) |

`Resolved > Room.Capacity` does **not** block membership save.  
Room never creates sections or memberships.

---

## 8. Membership lifecycle

| Concern | Decision |
|---|---|
| Physical delete | **Avoid** for current→history; prefer soft lifecycle |
| Soft delete | `IsDeleted = true` allowed (BaseEntity) |
| Current flag | Set `IsCurrent = false`, set `EffectiveTo`, soft-delete prior current when replacing |
| History | Retain prior rows (unique index only on current) |
| Active/Removed/Inactive enums | **Do not invent** new status enum — use IsCurrent + EffectiveTo + IsDeleted |
| Locked / Published TG | Mutation **rejected** (`EnsureCanMutate` / governance) — TG.2 |
| Lock snapshot | **Deferred implementation**; design default from TG.2: optional freeze of dynamic roster into Include rows at first Publish/Lock — not required to start Explicit/Hybrid mutation |

---

## 9. Attendance & Timetable boundaries

| Boundary | Rule |
|---|---|
| Attendance | Membership mutation **must not** create/update/delete Attendance |
| StudentSection | Membership mutation **must not** mutate StudentSection |
| TimetableEntry / TimetableSection | Membership mutation **must not** mutate; section link changes remain TG.4A projector path |

Attendance may later **read** resolved membership when architecture extends the resolver — out of scope for mutation writes.

---

## 10. Type vs MembershipSource

| Concept | Controls |
|---|---|
| `TeachingGroupType` | Section-link cardinality / capacity-split key rules |
| `MembershipSource` | How Resolved set is computed |

Create-time pairing should be validated for obvious incompatibilities (e.g. Elective Type with empty sections + StudentSubject source). Exact matrix enforcement is an implementation validation task; semantics above remain authoritative for resolution.
