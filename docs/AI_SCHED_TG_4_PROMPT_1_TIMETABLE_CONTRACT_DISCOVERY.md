# AI-SCHED-TG.4 Prompt 1 — Existing Timetable Contract Discovery

**Workstream:** AI-SCHED-TG.4 — Teaching Group–Timetable Integration  
**Prompt:** 1 — Architecture Discovery & Existing Timetable Contract  
**Date:** 2026-08-18  
**Type:** DISCOVERY ONLY  

**Prior frozen authority:** AI-SCHED-TG.2, TG.2A Prompts 1–3, TG.3 Prompts 1 / 1A / 2 / 3 (PASS)

**Production changes:** **None.** No database, entity, EF, migration, API, UI, Attendance, Allocation, RBAC, or governance code was modified.

---

## Executive Summary

The Scheduling stack already has a mature Timetable designer + governance model. TeachingGroup domain/EF (TG.3) exists and is live in the disposable DB, but **TimetableEntry is not yet linked**.

| Finding | Status |
|---|---|
| `TimetableEntry.TeachingGroupId` | **Absent** today |
| TeachingGroup → TimetableEntry wiring | **Not implemented** |
| `PUT /api/timetable/{id}/sections` | Direct TimetableSection soft-delete + insert (bypass TG) |
| Attendance section IDs | From `TimetableSections` in Timetable mode; Legacy fallback intact |
| Silent TG creation on GET/load | **None today** (no TG references in Timetable services) |
| SubjectAllocation uniqueness | Exists for allocation catalog; **must not** become TG resolver |

### STATUS: **PASS**

Discovery is complete and unambiguous. Recommended implementation boundary for subsequent TG.4 prompts is defined in §12.

---

## 1. Existing timetable architecture

```text
ScheduleVersion (optional)
        │
        ▼
   Timetable  ◄── Status: Draft → Locked → Published → Archived
        │              (+ IsFrozen orthogonal flag)
        │
        ▼
 TimetableEntry  ◄── SubjectAllocation (required)
        │              Staff / Room / Course / Group / Semester / Subject (denorm)
        │
        ├──(today)──► TimetableSection ──► Section   [bridge / compatibility]
        │
        └──(target)─► TeachingGroupId ──► TeachingGroup
                              │
                              ▼
                      TeachingGroupSection ──► Section   [authoritative TG→Section]
```

### Components inventory

| Concern | Entity | Application | API | EF |
|---|---|---|---|---|
| Header | `Timetable` | `TimetableService`, `TimetableLifecycleService` | `api/scheduling/timetables` | `TimetableConfiguration` → `SchedulingTimetable` |
| Placement | `TimetableEntry` | `TimetableService` | same controller (entries/*) | `TimetableEntryConfiguration` → `SchedulingTimetableEntry` |
| Section bridge | `TimetableSection` | `SectionManagementService` | `api/timetable/{id}/sections` | Inline `ApplicationDbContext` → `TimetableSections` |
| Allocation | `SubjectAllocation` | `SubjectAllocationService` | scheduling subject-allocation APIs | `SubjectAllocationConfiguration` |
| Versioning | `ScheduleVersion` | `ScheduleVersionService` | `api/scheduling/versions` | `TimetableGovernanceConfigurations` |
| Approval | Approval request/steps/history | `TimetableApprovalService` | `api/scheduling/approvals` | governance configs |
| Clone | Clone job | `TimetableCloneService` + background worker | `api/scheduling/clone-jobs` | clone configs |
| History | Change history | `TimetableChangeHistoryService` | history endpoints on timetable | history configs |
| Conflicts | Conflict runs/findings | `ConflictDetectionService` | conflict APIs | conflict configs |
| Academic SectionGroup | `SectionGroup` / Member | `SectionGroupService` | AI29 section-group APIs | AI29 inline / migrations |
| TeachingGroup | TG / TGSection / TGMembership | *(no timetable service yet)* | *(none)* | TG.3 configs (frozen) |

---

## 2. TimetableEntry relationships (as-is)

**File:** `Abhyanvaya.Domain/Entities/Scheduling/TimetableEntry.cs`

| Relationship | Cardinality / behavior |
|---|---|
| Timetable | Required FK; Cascade delete with header |
| TimeSlot | Required; Restrict |
| SubjectAllocation | Required; Restrict |
| Staff, Room, Department | Required; Restrict |
| Course, Group, Semester, Subject | Required denorm from allocation; Restrict |
| TimetableSection | Soft association by `(TimetableId, TimetableEntryId?)` — **no EF navigation** |
| TeachingGroup | **Not present** |

Entry creation denormalizes academic scope from SubjectAllocation (`TimetableService.ApplyAllocationDenormalization`). Cohort distinction beyond Course/Group/Semester is **not** modeled on the entry today.

---

## 3. Current section bridge

**Entity:** `Abhyanvaya.Domain/Entities/Academic/TimetableSection.cs`

| Column | Role |
|---|---|
| `TimetableId` | Header scope |
| `TimetableEntryId?` | Null = timetable-wide bucket; value = per-entry mapping |
| `SectionId` | Academic Section reference |

EF: table `TimetableSections`; index only; **no FK constraints** to Timetable/Section in fluent config (int columns). Soft-delete + tenant via BaseEntity global filters.

**Authority today (legacy):** TimetableSection is the operational write/read model for “which sections belong to this class.”  
**Approved target:** TimetableSection becomes a **projection** of TeachingGroupSection; TeachingGroupSection remains the sole TG→Section SoT.

---

## 4. SubjectAllocation relationships

- One SubjectAllocation row is unique per `(TenantId, AcademicYearId, SubjectId, CourseId, GroupId, SemesterId, DepartmentId)`.
- Many TimetableEntries may reference the same SubjectAllocation.
- Many TeachingGroups may belong to the same SubjectAllocation (**TG.3 verified**).
- **Forbidden:** resolving TeachingGroup by SubjectAllocation uniqueness / “first match.”

---

## 5. Lifecycle / governance / conflict / version behavior

### TimetableStatus

`Draft (1) → Locked (2) → Published (3) → Archived (4)`

| Operation | Service | Notes |
|---|---|---|
| Create | `TimetableService` | Starts Draft |
| Lock / Unlock | `TimetableService` | Draft ↔ Locked |
| Entry mutate | `TimetableService` | Draft only (`EnsureDraft`); frozen blocks edits |
| Publish | `TimetableLifecycleService` | Locked or approved ScheduleVersion; one Published per AY+Dept (non-frozen) |
| Freeze / Unlock-frozen | Lifecycle | Orthogonal to status |
| Archive | Lifecycle | Published/Locked → Archived |
| Submit/decide approval | `TimetableApprovalService` | Locks timetable; version UnderReview → Approved / return Draft |
| Soft validation | `TimetableSoftValidationService` | Entry-centric warnings |
| Clone | `TimetableCloneService` | Copies **entries only** — not TimetableSections (gap for future TG links) |
| Conflicts | `ConflictDetectionService` | Student rules use Course/Group/Semester on entries — not section/TG grain |

---

## 6. Existing PUT `/sections` path

### API

| Item | Value |
|---|---|
| Controller | `TimetableSectionsController` in `Abhyanvaya.API/Controllers/SectionsController.cs` |
| GET | `GET /api/timetable/{timetableId}/sections` — `CanViewSchedulingTimetable` |
| PUT | `PUT /api/timetable/{timetableId}/sections` — `CanManageSchedulingTimetable` |
| Service | `SectionManagementService.GetTimetableSectionsAsync` / `SetTimetableSectionsAsync` |
| DTO | `SetTimetableSectionsRequest` / `TimetableSectionDto` (`SectionDtos.cs`) |

### Request payload

```csharp
public sealed class SetTimetableSectionsRequest
{
    public int? TimetableEntryId { get; init; }
    public IReadOnlyList<int> SectionIds { get; init; } = [];
}
```

### Validation / persistence (current)

1. Timetable exists for tenant → else 404.  
2. Soft-delete existing TimetableSection rows for `(TimetableId, TimetableEntryId)`.  
3. Validate each SectionId exists for tenant.  
4. Insert new TimetableSection rows.  
5. Return full timetable section list.

**Does not:** create academic Sections; create SectionGroups; create TeachingGroups; sync TeachingGroupSection; enforce Draft status; check freeze; write Attendance.

### Authorization / governance

- Permission policies only (`CanManageSchedulingTimetable`).
- No timetable lifecycle gate on PUT sections.
- No change-history recording observed on this path.

### UI callers

| Client | Status |
|---|---|
| `abhyanvaya-ui/src/services/sectionService.ts` — `listTimetableSections` / `setTimetableSections` | **Defined** |
| Timetable designer pages | **Do not currently call** these helpers |
| Attendance UI | Consumes resolver section IDs (read), not PUT |

### Approved future rule (do not implement in Prompt 1)

```text
PUT /sections
    → TeachingGroup application boundary (explicit)
    → TeachingGroupSection (SoT)
    → TimetableSection projector (one-way)
```

---

## 7. No silent TeachingGroup creation (current confirmation)

| Path | Creates TeachingGroup? |
|---|---|
| Timetable GET / grid / dashboard | No |
| Entry create/update/move/copy/clone | No |
| GET `/timetable/{id}/sections` | No (reads TimetableSection only) |
| PUT `/sections` | No (writes TimetableSection only) |
| AttendanceSessionResolver | No |
| SubjectAllocation CRUD | No |

**Future MUST preserve:** no TG create on GET, timetable load, or resolution fallback. Creation/conversion only via **explicit** operations (designer attach, legacy PUT façade, cutover tool).

---

## 8. Attendance integration (compatibility — do not modify)

**File:** `Abhyanvaya.Application/Scheduling/Conflicts/AttendanceSessionResolver.cs`

| Mode | Behavior |
|---|---|
| **Timetable** | Resolves Published/Locked faculty period → enriches `SectionIds` / `SectionCodes` from **TimetableSections** matching `TimetableId` and (`TimetableEntryId` null OR entry id) |
| **Legacy** | No timetable period → `Mode = "Legacy"`, empty sections; existing Manual/Legacy attendance selection unchanged |

Classification:

- **Timetable-driven** when a published/locked period exists  
- **Section enrichment** via TimetableSection bridge  
- **Legacy fallback** preserved when no period  

**No TeachingGroup references.** TG.4 must keep Legacy intact and plan Attendance enrichment migration in a later approved prompt (not this discovery).

---

## 9. Authorization / validation map (timetable core)

| Surface | Auth | Validation |
|---|---|---|
| Timetable CRUD / entries | View / Manage scheduling timetable policies | Draft + not frozen for entry mutations; allocation required |
| Publish | `Scheduling.Publish` | Locked or approved version; uniqueness rules |
| Archive / Freeze / Unlock / History | Dedicated scheduling policies | Lifecycle service rules |
| PUT sections | Manage timetable | Tenant timetable + valid section IDs only |
| Approvals | Phase 2A approval policies | Step decision rules |

---

## 10. Proposed insertion point for `TeachingGroupId`

| Layer | Recommended change (later prompts) |
|---|---|
| Domain | Add `int? TeachingGroupId` + navigation on `TimetableEntry` (nullability policy per TG.2A — clean cutover may require non-null after policy flag) |
| EF | `TimetableEntryConfiguration`: FK → TeachingGroup, **Restrict** delete; index `(TenantId, TeachingGroupId)` |
| Migration | Additive column + FK only — **do not** alter TG.3 TeachingGroup tables |
| Application | Explicit attach/validate on entry create/update; resolve TG **only** via `TeachingGroupId` |
| DTO / API | Expose TeachingGroupId on entry DTOs; reject SA-only resolution |
| Legacy PUT | Façade into TG boundary + projector (separate prompt) |
| Attendance | Keep TimetableSection read until dedicated Attendance TG prompt |
| UI | Designer attach/select TeachingGroup (separate prompt) |

**Exact recommended implementation boundary for next prompt(s):**

1. **Schema + EF only:** `TimetableEntry.TeachingGroupId` (+ tests) — no API behavior change yet, **or**  
2. **Schema + entry attach validation** in `TimetableService` without redesigning PUT `/sections` — then  
3. **Legacy PUT façade + projector** — then  
4. **Attendance enrichment** — then  
5. **UI**.

Do **not** combine schema + legacy bridge + Attendance + UI in one Cursor prompt.

---

## 11. Risks

| Risk | Severity | Mitigation direction |
|---|---|---|
| Dual write: PUT sections vs TeachingGroupSection | High | Façade PUT through TG boundary; TimetableSection projection-only |
| Attendance still SoT on TimetableSection | High | Keep fallback; migrate enrichment later |
| Inferring TG from SubjectAllocation | High | Forbidden; require TeachingGroupId |
| Clone omits section/TG mapping | Medium | Extend clone after TG FK exists |
| Null `TimetableEntryId` TimetableSection bucket ambiguity | Medium | Prefer per-entry mappings once TG attached |
| Conflict rules not TG/section-grained | Medium | Document; address in conflict follow-up if needed |
| Designer UI unused section APIs | Medium | New TG-aware designer UX |
| Premature non-null TeachingGroupId without attach UX | Medium | Staged nullability / policy flag |

---

## 12. Files / classes / services involved

### Domain
- `Timetable.cs`, `TimetableEntry.cs`, `ScheduleVersion.cs`, `SubjectAllocation.cs`
- `TimetableSection.cs`, `SectionGroup.cs`, `SectionGroupMember.cs`
- `TeachingGroup.cs`, `TeachingGroupSection.cs`, `TeachingGroupMembership.cs`, `TeachingGroupRules.cs`
- Enums: `TimetableStatus`, `ScheduleVersionStatus`, TeachingGroup* enums

### EF / persistence
- `TimetableConfiguration.cs`, `TimetableEntryConfiguration.cs`
- `SubjectAllocationConfiguration.cs`, `TimetableGovernanceConfigurations.cs`
- `TeachingGroupConfiguration.cs` (+ Section/Membership)
- `ApplicationDbContext.cs` (TimetableSections inline + global filters)
- Migration `20260817153000_AI_SCHED_TG_3_TeachingGroup` (frozen — do not modify in discovery)

### Application
- `TimetableService`, `TimetableLifecycleService`, `TimetableApprovalService`
- `ScheduleVersionService`, `TimetableCloneService`, `TimetableChangeHistoryService`
- `TimetableSoftValidationService`, `ConflictDetectionService`
- `SectionManagementService` (GET/PUT sections)
- `SubjectAllocationService`, `SectionGroupService`
- `AttendanceSessionResolver`

### API
- `TimetableControllers.cs` (`api/scheduling/timetables`)
- `Phase2AControllers.cs` (versions/approvals/clone/governance)
- `SectionsController.cs` → `TimetableSectionsController` (`api/timetable/{id}/sections`)

### DTOs
- Timetable/entry DTOs under Application Scheduling DTOs
- `SetTimetableSectionsRequest`, `TimetableSectionDto` in `SectionDtos.cs`

### UI
- `TimetableHubPage.tsx`, designer/grid/dialog pages
- `schedulingService.ts`, `sectionService.ts` (section helpers unused by designer)

### Prior design contracts (binding)
- `docs/AI_SCHED_TG_2A_PROMPT_2_SOURCE_OF_TRUTH_TIMETABLESECTION.md`
- `docs/AI_SCHED_TG_2A_PROMPT_3_LEGACY_TIMETABLE_BRIDGE_AND_TEACHINGGROUP_RESOLUTION.md`
- `docs/AI_SCHED_TG_3_PROMPT_3_CLEAN_MIGRATION_CUTOVER.md`

---

## 13. Target contract reminder (unchanged)

```text
SubjectAllocation
        │
        ▼
TeachingGroup
        │
        ├──► TimetableEntry   (via TeachingGroupId — authoritative resolve)
        │
        └──► TeachingGroupSection ► Section   (sole TG→Section SoT)

TimetableSection = projection/bridge only
```

Resolution: **`TimetableEntry.TeachingGroupId` only.**  
No SubjectAllocation uniqueness, faculty/subject/section heuristics, or first-match fallback.

---

## 14. Final readiness gate

| Criterion | Met |
|---|---|
| Existing timetable model documented | Yes |
| TimetableEntry relationships documented | Yes |
| Section bridge documented | Yes |
| PUT `/sections` path documented | Yes |
| Governance/version/conflict documented | Yes |
| Attendance compatibility documented | Yes |
| Silent creation risk assessed | Yes — none today |
| TeachingGroupId insertion point proposed | Yes |
| TG.3 frozen model left unmodified | Yes |
| No production code/schema changes | Yes |

### STATUS = **PASS**

Ready for Chief Architect–approved TG.4 Prompt 2 (controlled schema/EF insertion of `TimetableEntry.TeachingGroupId` only — no legacy bridge/UI/Attendance in the same prompt).
