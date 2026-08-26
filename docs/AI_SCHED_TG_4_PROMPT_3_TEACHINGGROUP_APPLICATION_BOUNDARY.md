# AI-SCHED-TG.4 Prompt 3 — TeachingGroup Application Boundary & Explicit Timetable Entry Assignment

**Workstream:** AI-SCHED-TG.4  
**Prompt:** 3 — TeachingGroup application boundary & explicit TimetableEntry assignment  
**Date:** 2026-08-18  
**Predecessor:** AI-SCHED-TG.4 Prompt 2 (PASS — `TimetableEntry.TeachingGroupId` EF)

**STATUS: PASS**

---

## 1. Application boundary

Introduced:

| Component | Location |
|---|---|
| `ITeachingGroupApplicationService` | `Abhyanvaya.Application/Scheduling/` |
| `TeachingGroupApplicationService` | `Abhyanvaya.Application/Scheduling/` |
| DI registration | `DependencyInjection.cs` → scoped |

Flow:

```text
Timetable API (authorized)
        │
        ▼
ITeachingGroupApplicationService
        │
        ├── Load TimetableEntry (tenant-scoped repository)
        ├── EnsureDraft (existing lifecycle)
        ├── Load TeachingGroup (tenant query filters; no bypass)
        ├── TeachingGroupRules.EnsureCompatibleWithTimetableEntry
        │     ├── tenant match
        │     ├── assignable status
        │     ├── SubjectAllocationId match
        │     └── Course/Group/Semester/Subject match
        ├── Assign explicit TeachingGroupId
        └── Persist via existing IUnitOfWork / concurrency helper
```

TimetableApplication (`TimetableService`) remains the timetable CRUD boundary. TeachingGroup assignment is a **dedicated** application operation — not inferred inside `UpdateEntryAsync` / `CreateEntryAsync`.

---

## 2. Assignment contract

```csharp
AssignToTimetableEntryAsync(timetableEntryId, teachingGroupId, ct)
ClearFromTimetableEntryAsync(timetableEntryId, ct)
```

Rules:

- Caller must supply the exact `teachingGroupId`.
- No SubjectAllocation / Section / TimetableSection / Course hierarchy inference.
- No automatic TeachingGroup creation.
- Clear is an **explicit** DELETE operation; unrelated entry updates cannot null the FK.

---

## 3. Validation rules

| Check | Failure |
|---|---|
| Entry / TG ids ≤ 0 | `DomainException` |
| Entry not in tenant | `KeyNotFoundException` (safe) |
| TeachingGroup missing / other tenant | `KeyNotFoundException` (safe; no leakage) |
| Timetable not Draft / frozen | `DomainException` via `TimetableService.EnsureDraft` |
| TG Archived / deleted | `InvalidOperationException` |
| SubjectAllocation mismatch | `InvalidOperationException` |
| Academic scope mismatch | `InvalidOperationException` |

Assignable statuses reuse TG.3: Draft, Active, Locked may attach; Archived/deleted may not. Status is **not** auto-changed by assignment.

---

## 4. Tenant isolation

- Loads use `_db.SchedulingTeachingGroups` with ambient tenant query filters.
- **No** `.IgnoreQueryFilters()` in the application service or assign/clear API actions.
- Cross-tenant TeachingGroup id → not-found (same message as missing).
- Domain double-check: `EnsureCompatibleWithTimetableEntry` → `EnsureTimetableEntryTeachingGroupTenant`.

Regression: `Cross_tenant_TeachingGroup_is_rejected_as_not_found`.

---

## 5. Authorization

API endpoints:

| Method | Route | Policy |
|---|---|---|
| PUT | `api/scheduling/timetables/entries/{entryId}/teaching-group` | `CanManageSchedulingTimetable` |
| DELETE | `api/scheduling/timetables/entries/{entryId}/teaching-group` | `CanManageSchedulingTimetable` |

Same policy as other timetable entry mutations. No new RBAC; Allocation.* / Operations.View untouched.

---

## 6. Lifecycle handling

Reuses `TimetableService.EnsureDraft`:

- Draft → assignment / clear allowed (existing rules).
- Published / Locked / Approved (frozen) → rejected by existing guard.

No new timetable governance model.

---

## 7. API changes

Additive only:

- Request DTO: `AssignTeachingGroupToTimetableEntryRequest { TeachingGroupId }`
- Response: existing `TimetableEntryDto` with additive `TeachingGroupId`
- **Not** added to `UpdateTimetableEntryRequest` / `CreateTimetableEntryRequest` (prevents accidental nulling via DTO mapping)

`TimetableService.MapEntriesAsync` / `CloneEntry` continue to surface / preserve `TeachingGroupId` (from Prompt 2).

Out of scope (untouched):

- UI
- `PUT .../sections`
- Attendance / `AttendanceSessionResolver`
- TimetableSection writes
- TeachingGroupMembership / TeachingGroupSection mutations

---

## 8. Test coverage

`TeachingGroupApplicationBoundaryTests`:

| ID | Scenario | Result |
|---|---|---|
| A | Explicit valid assignment | Pass |
| B | NULL remains supported | Pass |
| C | TG not found | Pass |
| D/N | Cross-tenant → not found | Pass |
| E | Wrong SA / academic scope | Pass |
| F | Multiple TGs under one SA → exact TG | Pass |
| G | No implicit SA resolver methods | Pass |
| H | Missing TG does not create | Pass |
| I | Archived TG rejected | Pass |
| J | Update/Create DTO omit TeachingGroupId | Pass |
| K | Explicit reassignment TG-A → TG-B | Pass |
| L | API requires Manage policy (source guard) | Pass |
| M | Published / Approved lifecycle rejected | Pass |
| O | Membership unchanged | Pass |
| P | TeachingGroupSection unchanged | Pass |
| — | Explicit clear | Pass |
| — | Attendance / sections untouched | Pass |

`TeachingGroupApplicationArchitectureGuardTests` — implicit resolvers, denormalization, DTO nulling, dedicated API, no Prompt 3 migration.

---

## 9. Architecture Guard

Guards prevent:

- SubjectAllocation-only TeachingGroup resolution
- Auto TG create from timetable reads/updates
- TimetableSection ownership of TeachingGroup
- Section-based implicit resolution
- `.IgnoreQueryFilters` tenant bypass in assignment path
- Accidental `TeachingGroupId` on Update/Create entry DTOs

Legitimate `ITeachingGroupApplicationService` remains unconstrained beyond these invariants.

Existing Architecture Guard suite: **48 Passed**.

---

## 10. Regression results

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| TeachingGroup + Prompt 3 + related domain/EF/lifecycle | 96 | 0 | 0 |
| ArchitectureGuard / ArchitectureOwnership / SchedulingFoundation | 48 | 0 | 0 |
| FullyQualifiedName~Scheduling | 249 | 0 | 0 |

**Pre-existing failures:** none observed in the executed filters.  
**New failures:** none.

---

## 11. Build results

| Build | Result |
|---|---|
| API (`Abhyanvaya.API`) | **PASS** (0 errors; existing warnings only) |
| UI (`abhyanvaya-ui`) | **PASS** (`tsc -b && vite build`) |

No new EF migration in this prompt.

---

## 12. Live validation

| Check | Result |
|---|---|
| Explicit assignment via application service | Verified by unit tests (InMemory + tenant filters) |
| Invalid / cross-tenant / archived / wrong scope | Rejected deterministically |
| NULL `TeachingGroupId` remains valid | Verified |
| No silent TG creation | Verified (count unchanged on missing id) |
| Membership / TeachingGroupSection unchanged | Verified |
| TimetableSection / Attendance resolver untouched | Source + architecture guards |
| UI unchanged | No UI files modified; UI build PASS |
| Schema | Relies on Prompt 2 nullable FK; **no Prompt 3 migration** |
| Destructive cleanup | Not performed |

Live HTTP against a running API process was not required for acceptance; the application boundary is independently testable and covered above. Local disposable DB schema for `TeachingGroupId` was established in Prompt 2.

---

## 13. Deferred work

| Item | Phase |
|---|---|
| TeachingGroup designer / timetable UI dropdown | Later UI prompt |
| Legacy `PUT /api/timetable/{id}/sections` → TG bridge | Dedicated bridge prompt |
| TimetableSection projection from TeachingGroupSection | Bridge prompt |
| AttendanceSessionResolver TeachingGroup awareness | Attendance integration prompt |
| Auto-clear TG when entry SubjectAllocation changes | Explicit product decision (currently preserve) |

---

## 14. Deviations and risks

| Item | Notes |
|---|---|
| UpdateEntry + SA change | `TeachingGroupId` is **preserved** (Prompt §15). If SA is changed via UpdateEntry, an incompatible TG may remain until explicit reassignment/clear. Documented; not auto-resolved. |
| Concurrency | Uses existing `ConcurrencyExceptionHelper.SaveChangesAsync`; no second concurrency model. |
| Authorization test | Source-contract guard (unit test project does not reference API assembly). |
| Academic scope | Validated via denormalized Course/Group/Semester/Subject + SubjectAllocationId already on both entities — no hierarchy duplication. |

**Defect register:** empty (STATUS = PASS).

---

## Acceptance criteria checklist

1. Application boundary — **Yes** (`ITeachingGroupApplicationService`)  
2. `TimetableEntry.TeachingGroupId` SoT — **Yes**  
3. No SA implicit resolution — **Yes**  
4. No Section implicit resolution — **Yes**  
5. No automatic TG creation — **Yes**  
6. Tenant isolation — **Yes**  
7. Academic-scope validation — **Yes**  
8. Status validation — **Yes**  
9. Lifecycle preserved — **Yes** (`EnsureDraft`)  
10. Authorization preserved — **Yes**  
11. NULL backward compatible — **Yes**  
12. Membership not modified — **Yes**  
13. TeachingGroupSection not modified — **Yes**  
14. TimetableSection untouched — **Yes**  
15. Attendance untouched — **Yes**  
16. UI unchanged — **Yes**  
17. Architecture Guard — **Pass**  
18. Relevant regressions — **Pass**  
19. API build — **Pass**  
20. UI build — **Pass**  
21. No unrelated schema/migration — **Yes**

**STATUS = PASS**
