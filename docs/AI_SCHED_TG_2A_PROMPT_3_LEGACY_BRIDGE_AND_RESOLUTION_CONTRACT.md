# AI-SCHED-TG.2A Prompt 3 — Legacy Timetable Bridge & TeachingGroup Resolution Contract

**Workstream:** AI-SCHED-TG.2A  
**Prompt:** 3 — Legacy Timetable Bridge & TeachingGroup Resolution Contract  
**Authority:** Chief Architect  
**Date:** 2026-08-17  
**Depends on:**  
- `docs/AI_SCHED_TG_2_FINAL_ARCHITECTURE_DECISION.md` (READY)  
- `docs/AI_SCHED_TG_2A_PROMPT_1_CAPACITY_AND_MEMBERSHIP_SEMANTICS.md` (PASS)  
- `docs/AI_SCHED_TG_2A_PROMPT_2_SOURCE_OF_TRUTH_TIMETABLESECTION.md` (PASS)  

**Prompt type:** DESIGN CONTRACT (no production code in this prompt).  
**Purpose:** Lock the bridge, projection, and membership-resolution contracts so AI-SCHED-TG.3+ can implement without dual-write ambiguity.

---

## 1. ADL alignment

This contract follows Architecture Documentation Library (ADL) expectations already embodied in Abhyanvaya scheduling/academic docs:

| ADL principle | Application here |
|---|---|
| Clean Architecture | Bridge lives in Application; Domain owns TeachingGroup; Infrastructure persists; API remains thin |
| Bounded context | Extend **Scheduling** + thin Academic facade; do not create a parallel Timetable/Governance/Attendance context |
| Single source of truth | TeachingGroup is SoT; TimetableSection is projection only (2A Prompt 2) |
| Dependency Inversion | `ITeachingGroupMembershipResolver`, `ITimetableTeachingGroupBridge`, `ITimetableSectionProjector` |
| Interface First | Contracts defined before implementation |
| DRY / KISS / YAGNI | One bridge path; no bidirectional sync; no speculative event bus |
| Security First / Multi-tenant | All operations filter `TenantId`; no `IgnoreQueryFilters` |
| Backward compatibility | Legacy `GET/PUT /api/timetable/{id}/sections` retained via translation |
| Testability | Pure resolver + bridge unit tests without UI |

Reuse: SubjectAllocation, Timetable/TimetableEntry, SectionManagement façade, AttendanceSessionResolver (extend), Room capacity validation, ScheduleVersion governance.

---

## 2. Problem statement

Today:

```text
PUT /api/timetable/{id}/sections
  → SectionManagementService.SetTimetableSectionsAsync
  → direct TimetableSection writes
```

After TeachingGroup:

```text
TeachingGroup (SoT)
  → TeachingGroupSection
  → TimetableSection (projection)
```

Without a locked bridge contract, implementers will either:

1. Leave dual write paths, or  
2. Break legacy consumers (UI `setTimetableSections`, attendance enrichment).

This prompt defines the **only** allowed resolution and bridge behaviors.

---

## 3. Canonical runtime model

```text
SubjectAllocation
        ↓
TeachingGroup                          ← SoT (identity, capacity fields, membership mode)
   ├── TeachingGroupSection            ← Section links (when applicable)
   ├── TeachingGroupMembership         ← Explicit Include/Exclude (when applicable)
   └── MembershipResolver              ← Derived ResolvedStudentCount + StudentIds
        ↓
TimetableEntry.TeachingGroupId         ← Required in clean production model
        ↓
TimetableSectionProjector              ← Slave writer (projection only)
        ↓
TimetableSection                       ← Compatibility read model
```

**Capacity fields (2A Prompt 1):** `ExpectedStudentCount`, `MaxTeachingCapacity`; `ResolvedStudentCount` derived only; `Room.Capacity` independent.

---

## 4. TeachingGroup Resolution Contract

### 4.1 Interface (Application)

```csharp
// Conceptual — implement in AI-SCHED-TG.3+
public interface ITeachingGroupMembershipResolver
{
    Task<TeachingGroupResolutionResult> ResolveAsync(
        int teachingGroupId,
        CancellationToken cancellationToken = default);

    Task<TeachingGroupResolutionResult> ResolveAsync(
        TeachingGroupResolutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class TeachingGroupResolutionRequest
{
    public int TeachingGroupId { get; init; }
    /// <summary>When true, materialize snapshot rules for Locked groups if configured.</summary>
    public bool PreferFrozenSnapshot { get; init; }
}

public sealed class TeachingGroupResolutionResult
{
    public int TeachingGroupId { get; init; }
    public int TenantId { get; init; }
    public int SubjectAllocationId { get; init; }
    public int SubjectId { get; init; }
    public string Type { get; init; } = "";
    public string MembershipSource { get; init; } = "";
    public string Status { get; init; } = "";
    public int? ExpectedStudentCount { get; init; }
    public int? MaxTeachingCapacity { get; init; }
    public int ResolvedStudentCount { get; init; }          // derived
    public IReadOnlyList<int> StudentIds { get; init; } = [];
    public IReadOnlyList<int> SectionIds { get; init; } = [];
    public string? ExclusionGroupKey { get; init; }
    public string InstructionalActivityKind { get; init; } = "";
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
```

### 4.2 Resolution algorithm (normative)

```text
INPUT: TeachingGroup G (tenant-scoped)

1. Load G + TeachingGroupSection links + current membership rows (Include/Exclude).
2. Build base set B:
   - MembershipSource.Section / CombinedSections:
       B = current StudentSection.StudentId for all linked SectionIds (IsCurrent, tenant, scope)
   - MembershipSource.StudentSubject:
       B = StudentIds with StudentSubject(SubjectId) AND student in G academic scope
           (Tenant, AcademicYear, Course, Group, Semester)
   - MembershipSource.ExplicitStudents:
       B = {}
   - MembershipSource.Hybrid:
       B = as Section or StudentSubject base per G configuration
3. Apply explicit rows:
       B = (B ∪ IncludeStudentIds) − ExcludeStudentIds
       (only IsCurrent / in-effect dates)
4. If G.Status == Locked AND frozen snapshot exists AND PreferFrozenSnapshot:
       B = snapshot StudentIds
5. Distinct sort ascending StudentIds
6. ResolvedStudentCount = |B|
7. SectionIds = TeachingGroupSection.SectionId list (ordered by DisplayOrder/SectionId)
8. Emit warnings:
       - MaxTeachingCapacity set AND Resolved > Max → warning (caller may elevate to error)
       - Expected set AND Expected != Resolved → advisory warning
9. RETURN result
```

### 4.3 Scope validation (membership add path — normative)

Before accepting an explicit student:

| Check | Failure |
|---|---|
| Student.TenantId == G.TenantId | Error |
| Student Course/Group/Semester match G | Error |
| AcademicYear alignment per platform rules | Error |
| Subject enrollment when required (Elective/StudentSubject modes) | Error |
| ExclusionGroupKey uniqueness vs sibling TGs | Error |
| MaxTeachingCapacity would be exceeded | Error |

Never mutate `StudentSection`.

### 4.4 Zero-member groups

- Draft/Active: allowed; resolver returns empty list + warning.  
- Publish timetable referencing TG: Error if `SubjectAllocation.AttendanceMandatory` **or** if platform publish policy requires non-empty (2A default: hard-block when AttendanceMandatory).

---

## 5. Legacy Timetable Bridge Contract

### 5.1 Interfaces (Application)

```csharp
public interface ITimetableTeachingGroupBridge
{
    /// <summary>Legacy GET semantics — DTO shape unchanged.</summary>
    Task<IReadOnlyList<TimetableSectionDto>> GetSectionsAsync(
        int timetableId, CancellationToken cancellationToken = default);

    /// <summary>Legacy PUT — translates into TeachingGroup SoT + projection.</summary>
    Task<IReadOnlyList<TimetableSectionDto>> SetSectionsAsync(
        int timetableId,
        SetTimetableSectionsRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITimetableSectionProjector
{
    /// <summary>One-way: TeachingGroupSection (+ entry) → TimetableSection rows.</summary>
    Task ProjectEntryAsync(
        int timetableId,
        int timetableEntryId,
        int teachingGroupId,
        CancellationToken cancellationToken = default);

    Task ProjectTimetableAsync(
        int timetableId,
        CancellationToken cancellationToken = default);
}
```

### 5.2 PUT translation (normative)

**Replace** direct mutation in `SectionManagementService.SetTimetableSectionsAsync` with orchestration:

```text
SetSections(timetableId, { TimetableEntryId, SectionIds[] })

1. Authorize (existing Section/Timetable manage policies — do not weaken).
2. Load Timetable + Entry by id; TenantId must match; throw NotFound if missing.
3. If Timetable.Status in {Published, Locked} OR linked TG Locked:
     → reject membership/section reassignment per governance contract
       (unless Unlock workflow — out of scope for silent PUT).
4. Validate each SectionId exists in tenant and academic scope compatible with Entry’s
   Course/Group/Semester (from entry denormalization / allocation).
5. Resolve TeachingGroup:
   a. If Entry.TeachingGroupId set:
        TG = that group; must same SubjectAllocationId as Entry.
   b. Else (transition / clean create):
        Create TeachingGroup under Entry.SubjectAllocationId:
          - SectionIds.Count == 1 → Type=SectionDerived, MembershipSource=Section
          - SectionIds.Count  > 1 → Type=CombinedSections, MembershipSource=CombinedSections
          - SectionIds.Count == 0 → reject OR detach TG policy:
              APPROVED: reject empty PUT that clears sections when entry requires cohort
              (use dedicated TG detach API later if needed).
        Set Entry.TeachingGroupId = TG.Id
6. Replace TeachingGroupSection set for TG with SectionIds (soft-delete removed; add new).
7. Call ITimetableSectionProjector.ProjectEntryAsync(...)
8. Return GET-equivalent TimetableSectionDto list (unchanged contract).
```

**Forbidden:** inserting `TimetableSection` except inside `ITimetableSectionProjector`.

### 5.3 GET behavior (normative)

Preferred:

```text
For entries on timetable:
  if TeachingGroupId present → sections from TeachingGroupSection
  else → fall back to TimetableSection rows (pre-cutover only)
```

DTO shape remains `TimetableSectionDto` for UI compatibility (`listTimetableSections`).

### 5.4 Projection writer rules

| Rule | Requirement |
|---|---|
| Direction | TG → TimetableSection only |
| Scope | Soft-delete prior rows for (TimetableId, TimetableEntryId); insert one row per TeachingGroupSection |
| Tenant | Copy TeachingGroup.TenantId |
| TimetableEntryId | Always set on projected rows for entry-level maps |
| TimetableId | Denormalized as today |
| Idempotent | Re-project yields same logical section set |

---

## 6. Attendance resolution contract (additive)

### Target path

```text
AttendanceSessionResolver (Timetable mode)
  → find TimetableEntry (existing)
  → if TeachingGroupId present:
        result = ITeachingGroupMembershipResolver.ResolveAsync(TeachingGroupId)
        SectionIds = result.SectionIds
        (optional future) StudentIds for session prep — additive, not required to change Attendance schema in this bridge prompt
  → else:
        existing TimetableSection join (legacy)
  → Legacy mode unchanged when no timetable
```

**Non-negotiable:** Course→Group→Semester→Subject→Period fallback remains.

---

## 7. API surface impact (contract only)

| Endpoint | Change type |
|---|---|
| `GET/PUT /api/timetable/{id}/sections` | Behavior change behind same route (bridge) — **no breaking DTO** |
| `api/scheduling/teaching-groups/*` | New (TG.3+) — SoT APIs |
| Timetable entry create/update | Require `teachingGroupId` (clean model) |

Controllers stay thin; bridge invoked from Application services.

---

## 8. Security & tenancy

- Bridge and resolver always constrain `TenantId == ICurrentUserService.TenantId`.  
- Section validation uses tenant-scoped Section queries.  
- Permissions: reuse existing timetable/section manage for legacy PUT; TG manage keys for TG APIs (2A/TG.2).  
- No RBAC weakening; no cross-tenant membership.

---

## 9. Governance hooks

| State | Bridge PUT sections |
|---|---|
| Timetable Draft | Allowed |
| UnderReview / Approved (version) | Allowed until Published per TG.2 defaults |
| Published / Locked | **Rejected** with clear message — use unlock/new version |
| TG Locked | **Rejected** |

---

## 10. Testing contract (for implementers)

Minimum tests (TG.3+):

1. PUT sections creates/updates TeachingGroupSection SoT.  
2. Projection TimetableSection matches SoT.  
3. Second PUT does not leave orphan dual authority.  
4. Direct projector-only writes; no service bypass.  
5. Resolver SectionDerived returns StudentSection students.  
6. Resolver does not change StudentSection.  
7. ExclusionGroupKey blocks dual CapacitySplit membership.  
8. Published timetable PUT sections → error.  
9. Attendance Timetable mode returns SectionIds from TG when TeachingGroupId set.  
10. Legacy attendance path still works without timetable.

Architecture Guard: forbid new TimetableSection writes outside projector namespace.

---

## 11. Explicit non-goals (this prompt)

- Implementing entities/migrations/UI  
- Redesigning Timetable Designer UX (beyond requiring TeachingGroupId later)  
- Changing Attendance database schema  
- Backfilling disposable test timetables  
- Bidirectional TimetableSection → TeachingGroup sync  

---

## 12. Implementation sequence after this contract

| ID | Prompt | Outcome |
|---|---|---|
| **TG.2A.3** | This contract (design) | Locked bridge/resolver semantics |
| **TG.3** | Domain + EF + migration | TeachingGroup tables + Entry.TeachingGroupId |
| **TG.3A** | MembershipResolver + unit tests | Resolution algorithm live |
| **TG.3B** | TimetableSectionProjector + Bridge | Refactor `SetTimetableSectionsAsync` |
| **TG.3C** | AttendanceSessionResolver extension | TG-aware SectionIds |
| **TG.4+** | Public TG APIs + Designer UI | Full product surface |

---

## 13. Open questions

| # | Item | Status |
|---|---|---|
| 1 | Empty SectionIds on PUT | **RESOLVED:** Reject for entries that already require a cohort; no silent detach |
| 2 | StudentIds in attendance DTO now? | **DEFERRED:** SectionIds first; StudentIds additive later without schema break |

---

## 14. Readiness

Bridge and resolution contracts are unambiguous and ADL-aligned.

### STATUS: **PASS — CONTRACT READY FOR TG.3 IMPLEMENTATION**

**Confirmation:** This document is design-only. No production code/schema/API/UI was modified by producing it.

---

# Appendix A — Cursor Prompt Pack (ready to paste)

Use the following prompts **in order**. Prompt A is this contract’s acceptance gate (docs only if not already saved). Prompt B+ are implementation prompts for subsequent workstreams.

---

## Cursor Prompt A — Record / freeze TG.2A Prompt 3 contract (design only)

```text
You are the Senior Developer under the Chief Architect for Abhyanvaya.

Execute AI-SCHED-TG.2A Prompt 3 — Legacy Timetable Bridge & TeachingGroup Resolution Contract.

IMPORTANT: DESIGN ONLY for this prompt.
DO NOT modify C#, TypeScript, EF, DbContext, migrations, database, APIs, UI, permissions, or production logic.

Authoritative inputs:
- docs/AI_SCHED_TG_2_FINAL_ARCHITECTURE_DECISION.md
- docs/AI_SCHED_TG_2A_PROMPT_1_CAPACITY_AND_MEMBERSHIP_SEMANTICS.md
- docs/AI_SCHED_TG_2A_PROMPT_2_SOURCE_OF_TRUTH_TIMETABLESECTION.md
- Existing SectionManagementService.SetTimetableSectionsAsync / GetTimetableSectionsAsync
- AttendanceSessionResolver TimetableSections join
- ADL: Clean Architecture, single SoT, bounded context reuse, tenant isolation, backward compatibility

Deliverable:
Ensure docs/AI_SCHED_TG_2A_PROMPT_3_LEGACY_BRIDGE_AND_RESOLUTION_CONTRACT.md exists and matches the Chief Architect contract for:
1) ITeachingGroupMembershipResolver normative algorithm
2) ITimetableTeachingGroupBridge PUT/GET translation
3) ITimetableSectionProjector one-way projection rules
4) Attendance additive resolution path
5) Governance lock behavior
6) Test/Architecture Guard expectations
7) Explicit non-goals

STATUS must be PASS — CONTRACT READY with confirmation no production code changed.

Copy the deliverable to:
D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI Scheduling Enhancement\AI-SCHED-TG.2A
```

---

## Cursor Prompt B — AI-SCHED-TG.3A Membership Resolver (implementation)

```text
You are the Senior Developer under the Chief Architect.

Implement AI-SCHED-TG.3A — TeachingGroup Membership Resolver
per docs/AI_SCHED_TG_2A_PROMPT_3_LEGACY_BRIDGE_AND_RESOLUTION_CONTRACT.md §4
and capacity rules in docs/AI_SCHED_TG_2A_PROMPT_1_CAPACITY_AND_MEMBERSHIP_SEMANTICS.md.

Prerequisites: TeachingGroup domain entities + EF migration from AI-SCHED-TG.3 must already exist.
If entities do not exist, STOP and report BLOCKED — do not invent a parallel schema.

Implement:
- ITeachingGroupMembershipResolver + TeachingGroupResolutionResult
- Normative resolution algorithm (Section / Combined / StudentSubject / Explicit / Hybrid)
- Full academic-scope checks
- ExclusionGroupKey mutual-exclusion validation helper
- Unit tests for French elective, section-derived, capacity-split exclusion, zero members
- Do NOT mutate StudentSection
- Do NOT wire legacy PUT yet (that is TG.3B)
- Do NOT weaken RBAC or tenant filters

Deliver: implementation report docs/AI_SCHED_TG_3A_MEMBERSHIP_RESOLVER.md with test counts.
Copy artifacts to CursonModifiedFiles\...\AI Scheduling Enhancement\AI-SCHED-TG.3A
```

---

## Cursor Prompt C — AI-SCHED-TG.3B Legacy Timetable Bridge (implementation)

```text
You are the Senior Developer under the Chief Architect.

Implement AI-SCHED-TG.3B — Legacy Timetable Bridge & TimetableSection Projector
per docs/AI_SCHED_TG_2A_PROMPT_3_LEGACY_BRIDGE_AND_RESOLUTION_CONTRACT.md §5.

Refactor SectionManagementService.SetTimetableSectionsAsync so it:
- Does NOT write TimetableSection directly
- Calls ITimetableTeachingGroupBridge.SetSectionsAsync
- Creates/updates TeachingGroup + TeachingGroupSection as SoT
- Projects via ITimetableSectionProjector only

Preserve GET/PUT /api/timetable/{id}/sections DTO contracts.
Reject updates when Timetable Published/Locked or TG Locked.
Add Architecture Guard / unit tests proving no dual-write.
Do not redesign Timetable Designer UI in this prompt.
Do not change Attendance schema.

Deliver: docs/AI_SCHED_TG_3B_LEGACY_BRIDGE_IMPLEMENTATION.md
Copy artifacts to CursonModifiedFiles\...\AI Scheduling Enhancement\AI-SCHED-TG.3B
```

---

## Cursor Prompt D — AI-SCHED-TG.3C Attendance Resolver Extension (implementation)

```text
You are the Senior Developer under the Chief Architect.

Implement AI-SCHED-TG.3C — AttendanceSessionResolver TeachingGroup awareness
per docs/AI_SCHED_TG_2A_PROMPT_3_LEGACY_BRIDGE_AND_RESOLUTION_CONTRACT.md §6
and docs/AI30_PHASE2B_ATTENDANCE_RESOLUTION.md.

When TimetableEntry.TeachingGroupId is present:
- Resolve SectionIds from TeachingGroupSection / membership resolver
When absent:
- Keep existing TimetableSection join
Always preserve Legacy Course→Group→Semester→Subject→Period fallback.

No attendance schema migration.
No forced timetable usage.
Add unit tests for TG path + Legacy path.

Deliver: docs/AI_SCHED_TG_3C_ATTENDANCE_RESOLVER.md
Copy artifacts to CursonModifiedFiles\...\AI Scheduling Enhancement\AI-SCHED-TG.3C
```

---

## Appendix B — Decision summary

| Decision | Value |
|---|---|
| SoT | TeachingGroup |
| Projection | TimetableSection via projector only |
| Legacy PUT | Translate through bridge |
| Resolver | Normative algorithm §4.2 |
| Room capacity | Independent; publish gate per 2A.1 |
| Empty PUT | Reject |
| StudentIds on attendance now | Deferred |
| Production code in 2A.3 | None |
