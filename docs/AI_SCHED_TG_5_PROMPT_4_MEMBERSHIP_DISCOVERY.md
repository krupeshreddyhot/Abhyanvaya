# AI-SCHED-TG.5 Prompt 4 — Membership Architecture Discovery

**Workstream:** AI-SCHED-TG.5  
**Prompt:** 4 — Membership Semantics & Mutation Contract (discovery)  
**Date:** 2026-08-19  
**Type:** DISCOVERY ONLY — no production mutation implementation  

**STATUS: PASS**

**Frozen predecessors:** TG.2 / TG.2A · TG.3 · TG.4 · TG.4A FROZEN · TG.5 Prompt 2 (API) · TG.5 Prompt 3 (UI foundation)

---

## 1. Entities & relationships (as implemented)

| Entity | Role | Notes |
|---|---|---|
| `TeachingGroup` | Scheduling cohort under `SubjectAllocation` | Owns `MembershipSource`, `Type`, capacity, `ExclusionGroupKey`, lifecycle |
| `TeachingGroupSection` | **SoT** for TG ↔ academic Section links | Projection feeds `TimetableSection` via projector |
| `TeachingGroupMembership` | Explicit Include/Exclude operational rows | Does **not** replace `StudentSection` / `StudentSubject` |
| `Student` | Person master | FK from membership (Restrict) |
| `StudentSection` | Academic section membership | Separate domain; must not be mutated by TG membership ops |
| `StudentSubject` | Subject enrollment | Designed source for elective/dynamic resolution (TG.2); **no resolver service yet** |
| `SubjectAllocation` | Parent academic scope for TG | 1 SA → many TGs |
| `TimetableEntry.TeachingGroupId` | Explicit TG assignment | Frozen TG.4 |
| `TimetableSection` | Projection only | Frozen TG.4A — **never** membership SoT |
| Attendance | Consumes scheduling session resolution | Must not be written by membership mutation |

---

## 2. TeachingGroupMembership fields (implemented)

| Field | Type | Meaning |
|---|---|---|
| `Id` | int | PK |
| `TenantId` | int | From `BaseEntity`; query-filtered |
| `TeachingGroupId` | int | FK → TeachingGroup (Cascade) |
| `StudentId` | int | FK → Student (Restrict) |
| `Inclusion` | `TeachingGroupMembershipInclusion` | Include=1, Exclude=2 |
| `EffectiveFrom` | DateOnly | Membership interval start |
| `EffectiveTo` | DateOnly? | Interval end |
| `IsCurrent` | bool | Current row flag (default true) |
| `CreatedDate` / `CreatedBy` / `UpdatedDate` / `UpdatedBy` | audit | BaseEntity |
| `IsDeleted` | bool | Soft delete |

**No** `StudentSectionId`, origin/source-kind, or concurrency token on membership rows.

---

## 3. Enums (implemented)

### `TeachingGroupMembershipSource`

| Value | Code | Implemented persistence | Resolver implemented? |
|---|---|---|---|
| Section | 1 | Yes (on TG) | **No** — UNDEFINED at application layer |
| CombinedSections | 2 | Yes | **No** |
| StudentSubject | 3 | Yes | **No** |
| ExplicitStudents | 4 | Yes | Partial: count Includes only in list/detail |
| Hybrid | 5 | Yes | **No** |

### `TeachingGroupMembershipInclusion`

| Value | Code | Entity support |
|---|---|---|
| Include | 1 | Yes |
| Exclude | 2 | Yes (entity + conversion) |

**Design intent (TG.2):** Exclude used for Hybrid overlay.  
**Application mutation:** UNDEFINED — no write API.

---

## 4. EF constraints & indexes (implemented)

Table: `SchedulingTeachingGroupMembership`

| Constraint | Detail |
|---|---|
| Unique filtered index | `(TenantId, TeachingGroupId, StudentId)` where `IsCurrent = TRUE AND IsDeleted = FALSE` |
| Index | `(TenantId, TeachingGroupId)` |
| Index | `(TenantId, StudentId)` |
| Delete | TeachingGroup → Cascade memberships; Student → Restrict |

Temporal history is **allowed** by design (non-current rows). Exactly one current row per student per TG.

---

## 5. Domain rules present (partial)

| Rule | Location | Status |
|---|---|---|
| `ComputeResolvedStudentCount` | `TeachingGroup` | Exists — distinct StudentId count helper |
| `EnsureResolvedWithinMaxCapacity` | `TeachingGroup` | Exists — membership-time ceiling |
| `EnsureStudentNotInMutuallyExclusiveGroup` | `TeachingGroupRules` | Exists — ExclusionGroupKey peers |
| Full membership resolver | Application | **UNDEFINED / not implemented** |
| Membership mutation service | Application | **UNDEFINED / not implemented** |

---

## 6. API behavior (TG.5 Prompt 2)

| Endpoint | Behavior |
|---|---|
| `GET .../teaching-groups/{id}/memberships` | Returns raw `TeachingGroupMembership` rows (read-only DTO) |
| Mutation membership endpoints | **Absent** (intentional Prompt 2 gap) |
| List/Detail `ResolvedStudentCount` | Counts **current Include** membership rows only — **does not** resolve Section/StudentSubject/Hybrid |

**Gap:** Displayed ResolvedStudentCount ≠ full TG.2A membership resolver for dynamic sources.

---

## 7. UI assumptions (TG.5 Prompt 3)

| Assumption | Status |
|---|---|
| Membership panel is read-only | Correct |
| Banner: “Membership management is not yet available” | Correct |
| Shows raw membership table (StudentId, Inclusion, IsCurrent, EffectiveFrom) | Correct |
| Does not call TimetableSection / StudentSection mutation | Correct |
| Treats ResolvedStudentCount as non-editable | Correct |
| Does not claim Section-derived roster display | Correct (shows only stored membership rows) |

---

## 8. Authorization

| Key | Membership relevance |
|---|---|
| `Scheduling.TeachingGroup.View` | GET memberships |
| `Scheduling.TeachingGroup.Manage` | Future mutations (not implemented) |

Dedicated keys exist (API + UI). Not granted via Attendance alone.

---

## 9. Audit infrastructure

| Mechanism | Availability |
|---|---|
| `BaseEntity` Created/Updated fields | On membership entity |
| `IAuditService.RecordAsync` | Generic cross-module audit exists |
| Membership-specific audit events | **UNDEFINED** — must specify in mutation contract |

---

## 10. Prior design authority (must not contradict)

- `docs/AI_SCHED_TG_2_PROMPT_4_MEMBERSHIP_SEMANTICS.md` — modes & Hybrid formula  
- `docs/AI_SCHED_TG_2A_PROMPT_1_CAPACITY_AND_MEMBERSHIP_SEMANTICS.md` — capacity + exclusion + eligibility  
- TG.4A freeze — TimetableSection projection only  

Where Prompt 2 implementation under-delivers (resolver), Prompt 4 **defines** the target contract; implementation comes later.

---

## 11. UNDEFINED items requiring architectural decision in this prompt

| # | Topic | Notes |
|---|---|---|
| U1 | Hybrid base population when no sections | Infer StudentSubject vs reject |
| U2 | Resolved roster GET vs raw membership rows | Need both views? |
| U3 | Concurrency token | None on entity today |
| U4 | Lock/Publish membership freeze snapshot | TG.2 open default Yes — not implemented |
| U5 | StudentSubject academic-year binding details | Student/enrollment conventions |

These are decided in the Prompt 4 contract docs (not left hanging without a call).

---

## Discovery gate

**PASS** — existing model is sufficient to design mutation without inventing a parallel membership store. Schema appears adequate for Hybrid Include/Exclude (Model B). No production changes in this prompt.
