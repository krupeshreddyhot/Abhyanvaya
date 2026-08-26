# AI-SCHED-TG.5 Prompt 2 — Teaching Group Management Application/API Contract

**Workstream:** AI-SCHED-TG.5 — Teaching Group Management & Scheduling UX  
**Prompt:** 2 — Application/API Contract  
**Date:** 2026-08-19  
**Type:** Backend application + HTTP contract only (no UI)

**STATUS: CONDITIONAL PASS**

**Predecessors (preserved):**
- AI-SCHED-TG.2 / TG.2A — Domain + capacity SoT
- AI-SCHED-TG.3 — Domain/EF/Clean migration
- AI-SCHED-TG.4 — TimetableEntry TeachingGroup integration
- AI-SCHED-TG.4A — **FULL PASS — FROZEN**
- AI-SCHED-TG.5 Prompt 1 — UX Architecture Discovery

---

## Implementation summary

Introduced an explicit Teaching Group management application boundary and HTTP API that the Prompt 3 React UX can consume without bypassing `TeachingGroup` / `TeachingGroupSection` / `TimetableSectionProjector`, without SubjectAllocation→TG inference, and without auto-create on list/get.

---

## 1. Application interfaces

| Interface | Responsibility |
|---|---|
| `ITeachingGroupManagementApplicationService` | List by SubjectAllocation, Get, Create, Update, Archive, GetMemberships (read) |
| `ITeachingGroupSectionApplicationService` | Existing SoT + **projecting** HTTP mutations: `ReplaceSectionsAndProjectAsync`, `AddSectionAndProjectAsync`, `RemoveSectionAndProjectAsync` |
| `ITimetableSectionProjector` | Unchanged — sole TimetableSection writer |

**DI:** `TeachingGroupManagementApplicationService` registered in `Abhyanvaya.Application/DependencyInjection.cs`.

**Not placed in:** Controllers, EF config, DbContext, TimetableService, SubjectAllocationService, React.

---

## 2. Endpoint contract

Base: `api/scheduling/teaching-groups`  
Controller: `TeachingGroupsController`

| Method | Route | Auth | Notes |
|---|---|---|---|
| GET | `?subjectAllocationId=` | View | List; never auto-creates |
| GET | `{id}` | View | Detail |
| POST | `/` | Manage | Explicit create |
| PUT | `{id}` | Manage | Allowed fields only |
| POST | `{id}/archive` | Manage | Soft archive via status |
| GET | `{id}/memberships` | View | Read-only membership |
| GET | `{id}/sections` | View | Via TeachingGroupSection SoT |
| PUT | `{id}/sections` | Manage | → `ReplaceSectionsAndProjectAsync` |
| POST | `{id}/sections/{sectionId}` | Manage | → `AddSectionAndProjectAsync` |
| DELETE | `{id}/sections/{sectionId}` | Manage | → `RemoveSectionAndProjectAsync` |

Existing assign/clear remain:

- `PUT/DELETE api/scheduling/timetables/entries/{id}/teaching-group`

---

## 3. DTO definitions

File: `Abhyanvaya.Application/DTOs/Scheduling/TeachingGroupManagementDtos.cs`

- `TeachingGroupSummaryDto` — list row (identity, type, status, SA + academic scope ids, capacity, resolved count, section/entry counts)
- `TeachingGroupDetailDto` — summary + notes, display order, membership count, `Sections`
- `CreateTeachingGroupRequest` — SA, name/code, type, membership source, activity, capacity, exclusion key, dates, notes
- `UpdateTeachingGroupRequest` — name/code/activity/capacity/dates/notes/display order (**no** SA / type / membership source / tenant)
- `TeachingGroupMembershipDto` — read model
- `AddTeachingGroupSectionRequest` — `{ isPrimary }`
- Reuses `ReplaceTeachingGroupSectionsRequest` / `TeachingGroupSectionDto`

**Server-owned / derived values not accepted from client:** TenantId, audit fields, ResolvedStudentCount.

---

## 4. Authorization model

| Permission key | Policy |
|---|---|
| `Scheduling.TeachingGroup.View` | `CanViewSchedulingTeachingGroup` |
| `Scheduling.TeachingGroup.Manage` | `CanManageSchedulingTeachingGroup` |

- View: list, get, sections read, memberships read  
- Manage: create, update, archive, section mutations  
- Not granted via Attendance or generic Timetable permissions alone.

---

## 5. Validation rules

- Tenant from authenticated ambient context only (query filters; no `IgnoreQueryFilters`)
- SubjectAllocation must exist in tenant; academic scope copied from SA on create
- Code uniqueness within (Tenant + SubjectAllocation) when code provided
- Capacity: Expected ≥ 0 or null; Max null or > 0; Expected ≤ Max when both set
- CapacitySplit requires ExclusionGroupKey
- Section links validated via `TeachingGroupRules` + academic scope compatibility
- Update cannot change SubjectAllocation / Type / MembershipSource / tenant
- Archive uses `TransitionTo(Archived)` — no hard delete
- Archived TG rejects subsequent property mutations (`EnsureCanMutate`)

---

## 6. Section mutation flow

```
Controller
  → ITeachingGroupSectionApplicationService
      → TeachingGroupSection (SoT)
      → ITimetableSectionProjector
      → TimetableSection (projection)
  → single SaveChanges
```

PUT uses `ReplaceSectionsAndProjectAsync` (TG.4A frozen).  
POST/DELETE use new `*AndProjectAsync` variants so HTTP section changes also re-project.

Controller never executes `TimetableSections.Add` / DbContext TeachingGroup mutation.

---

## 7. Membership boundary status

| Capability | Status |
|---|---|
| GET `{id}/memberships` | **Implemented** (read) |
| ResolvedStudentCount | **Derived** from current Include memberships (not persisted) |
| Membership mutation (POST/PUT/DELETE) | **Not implemented** |

**Gap (intentional):** Domain models `MembershipSource` (SectionDerived / Combined / Elective / Explicit / Hybrid) and Inclusion/ActivityKind, but Explicit/Hybrid write orchestration (resolve dynamic members, mutual exclusion under ExclusionGroupKey, MaxTeachingCapacity enforcement on resolve) is not yet a safe, documented application mutation contract. Inventing mutation semantics here would conflict with TG.2/TG.2A.

**Prompt 3 implication:** Membership UI may display read-only membership / counts; mutation UX waits for a dedicated membership write prompt.

---

## 8. Tenant isolation

- All loads via tenant-filtered `IApplicationDbContext` sets
- TeachingGroup.TenantId set from `ICurrentUserService.TenantId` on create
- Section compatibility enforces matching tenant + academic year/course/group/semester
- Client-supplied TenantId is never authoritative

---

## 9. Error contract

User-safe `DomainException` / `KeyNotFoundException` mapped to 400 / 404 with short messages, e.g.:

- Teaching Group / Subject Allocation / section link not found
- Code already exists for the Subject Allocation
- Section already linked / outside academic scope
- Capacity rule violations
- Archived Teaching Group cannot be mutated

No stack traces, SQL, or tenant-sensitive diagnostics in responses.

---

## 10. Compatibility assessment

| Surface | Impact |
|---|---|
| TimetableEntry TeachingGroup assign/clear | Unchanged |
| Legacy `/timetable/{id}/sections` bridge | Unchanged (still → ReplaceSectionsAndProjectAsync) |
| Attendance / StudentSection | Unchanged |
| Timetable clone/version | Unchanged |
| React UI | **Untouched** (Prompt 3) |
| Migrations | **None** |

---

## 11. Tests executed

| Suite | Result |
|---|---|
| `AiSchedTg5Prompt2TeachingGroupManagementTests` | **Passed** |
| `AiSchedTg5Prompt2ArchitectureGuardTests` | **Passed** |
| TG.4A Prompt 8 / 10 + related TG architecture guards (filter run) | **49/49 Passed** |
| `Abhyanvaya.API` build | **Succeeded** |
| UI build | Untouched (no UI changes) |

---

## 12. Migrations

**None.** No schema change required for this contract.

---

## 13. Known limitations

1. **Membership mutation API deferred** (documented gap above).
2. **College** not on TeachingGroup entity — detail DTO exposes SA academic scope ids only (College omitted / N/A).
3. Create leaves Status = **Draft**; callers may archive or later activate via domain transitions (no separate activate endpoint in this prompt).
4. Type / MembershipSource immutable after create via Update API (by design for this contract).
5. SectionDerived/CombinedSections may be created with zero sections; cardinality enforced on section mutation.

---

## 14. Readiness for Prompt 3 (Teaching Group Management UI)

| Prompt 3 need | Ready? |
|---|---|
| List by SubjectAllocation | Yes |
| Detail / create / update / archive | Yes |
| Section link management | Yes (SoT + projection) |
| Dedicated View/Manage RBAC | Yes |
| Membership display (read) | Yes |
| Membership edit UX | **No** — wait for mutation contract |
| Generated OpenAPI client (if used) | Regenerate after API deploy |

**Verdict for Prompt 3:** Backend contract is sufficient to build the Teaching Groups hub (list/detail/CRUD/sections) without violating TG.4A. Membership editors should be read-only or deferred.

---

## Final gate

**CONDITIONAL PASS**

Unresolved items preventing FULL PASS:

1. Membership mutation semantics unresolved (read-only boundary only).
2. Live HTTP/RBAC integration tests against a running host were not part of this prompt’s automated suite (unit + architecture guards cover application behavior and static auth wiring).

All other Prompt 2 success criteria for the application/API boundary are met.
