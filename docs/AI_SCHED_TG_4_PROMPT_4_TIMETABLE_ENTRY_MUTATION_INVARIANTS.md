# AI-SCHED-TG.4 Prompt 4 — Timetable Entry Mutation Invariants & TeachingGroup Compatibility Enforcement

**Workstream:** AI-SCHED-TG.4  
**Prompt:** 4 — Mutation invariants & TeachingGroup compatibility  
**Date:** 2026-08-18  
**Predecessor:** AI-SCHED-TG.4 Prompt 3 (PASS — explicit assignment boundary)

**STATUS: PASS**

---

## 1. Existing mutation paths discovered

| Path | Service | Can change SA / scope? | Can retain TeachingGroupId? |
|---|---|---|---|
| `CreateEntryAsync` | `TimetableService` | Sets from SA (TG always null via DTO) | No (null) |
| `UpdateEntryAsync` | `TimetableService` | **Yes** via `ApplyAllocationDenormalization` | **Yes (gap closed)** |
| `BulkUpsertEntriesAsync` | `TimetableService` | **Yes** | **Yes (gap closed)** |
| `MoveEntryAsync` | `TimetableService` | No (day/slot/room) | Yes — revalidated |
| `CopyEntryAsync` | `TimetableService` | No (clone + day/slot/room) | Yes — revalidated |
| `DuplicateEntryAsync` | `TimetableService` | No | Yes — revalidated |
| `TimetableCloneService.ExecuteJobAsync` | Clone | Day remap only | Yes — revalidated |
| `ScheduleVersionService` version clone | Version | Same SA copied | Yes — revalidated |
| `AssignToTimetableEntryAsync` | `TeachingGroupApplicationService` | No | Sets TG (already validated in P3) |
| `ClearFromTimetableEntryAsync` | `TeachingGroupApplicationService` | No | Clears TG explicitly |

**Not modified (out of scope):** `PUT .../sections`, Attendance, UI, TimetableSection projection.

**Prompt 3 gap:** `UpdateEntry` / bulk upsert could change `SubjectAllocationId` + denormalized Course/Group/Semester/Subject while leaving `TeachingGroupId` attached → incompatible persisted state. Closed by proposed-state validation.

---

## 2. Compatibility rule

Single authoritative rule:

```csharp
TeachingGroupRules.EnsureCompatibleWithTimetableEntry(teachingGroup, entry)
```

Checks:

| Dimension | Source |
|---|---|
| Tenant | `TimetableEntry.TenantId` == `TeachingGroup.TenantId` |
| Assignable status | `EnsureCanAttachToTimetableEntry` (Draft/Active/Locked; not Archived/deleted) |
| SubjectAllocation | `TeachingGroup.SubjectAllocationId` == `TimetableEntry.SubjectAllocationId` |
| Academic scope | CourseId, GroupId, SemesterId, SubjectId match (denormalized on both) |

**Not duplicated:** College is not on these entities. AcademicYear exists on TeachingGroup/SubjectAllocation but not on TimetableEntry; SA identity (`SubjectAllocationId`) is the authoritative SA contract (one SA has one AcademicYear).

Actionable error constant:

```text
TeachingGroupRules.TimetableEntryTeachingGroupIncompatibleMessage
```

Cross-tenant failures use the same safe message (no tenant id leakage).

---

## 3. Proposed-state validation design

```text
Authorization / tenant / lifecycle (existing)
        ↓
Build proposed TimetableEntry state
        ↓
EnsureProposedTeachingGroupCompatibleAsync
        ├── TeachingGroupId == null → allow
        └── TeachingGroupId != null
              → tenant-scoped load (no IgnoreQueryFilters)
              → EnsureCompatibleWithTimetableEntry(proposed)
              → FAIL → DomainException (no persist / no silent clear)
        ↓
Persist
```

Helper: `TimetableService.EnsureProposedTeachingGroupCompatibleAsync` (instance + static overload for clone/version services).

**Does not:** clear TG, replace TG, infer TG, or create TG.

---

## 4. Create semantics

| Case | Behavior |
|---|---|
| A — `TeachingGroupId = null` | Allowed (current Create DTO never sets TG) |
| B/C/D — TG on create | Not exposed via Create DTO (Prompt 3). If somehow set, invariant validates before persist. |

---

## 5. Update semantics

| Scenario | Result |
|---|---|
| Existing TG + unchanged SA | PASS if still compatible |
| Existing TG + SA change to match TG | PASS |
| Existing TG + incompatible SA change | **REJECT** (no persist) |
| No TG + SA change | PASS |
| Explicit clear then SA change | PASS |

---

## 6. Clone semantics

`CloneEntry` now copies `TenantId` (required for pre-SaveChanges validation).

| Scenario | Result |
|---|---|
| Same scope + compatible TG | Preserved + validated |
| Scope mutated + incompatible TG | **REJECT** |
| No TG | PASS |

Clone/version jobs call `EnsureProposedTeachingGroupCompatibleAsync` before `AddEntriesAsync`.

---

## 7. Clear semantics

Unchanged: `DELETE .../entries/{entryId}/teaching-group` remains the only supported clear. Update/create DTOs still omit `TeachingGroupId` (no accidental nulling).

---

## 8. Tenant behavior

- TeachingGroup load uses query filters only.
- No `.IgnoreQueryFilters()`.
- Cross-tenant → incompatible message (safe not-available semantics).

---

## 9. Lifecycle behavior

`EnsureDraft` still runs before mutation. Locked/Published/Archived still reject edits. Compatibility is additive, not a bypass.

---

## 10. API error behavior

`UpdateEntry` / bulk / create already map `DomainException` → `400 BadRequest` with message body. No new error framework. No stack/SQL/tenant leakage.

**Future UI note:** when SubjectAllocation changes while a TeachingGroup is assigned, the API will return the incompatible message; UI should prompt clear or re-assign TG before retry. **UI not changed in this prompt.**

---

## 11. Test matrix

`TimetableEntryTeachingGroupMutationInvariantTests` + updated Prompt 3 boundary tests:

| Area | Coverage |
|---|---|
| Domain compatible / incompatible / cross-tenant | Pass |
| Update unchanged SA + TG | Pass |
| Update incompatible SA + TG | Reject + not persisted |
| Update repair SA to match TG | Pass |
| Update without TG + SA change | Pass |
| Create null TG | Pass |
| Clear then SA change | Pass |
| Clone same / changed / null | Pass |
| Copy preserves TG | Pass |
| Lifecycle locked still blocks | Pass |
| No silent create/clear | Pass |
| Mutation path source guards | Pass |

---

## 12. Architecture Guard coverage

Extended `TeachingGroupApplicationArchitectureGuardTests`:

- No SA→First/Create TG inference in TimetableService
- `ApplyAllocationDenormalization` does not touch TeachingGroupId
- `EnsureProposedTeachingGroupCompatibleAsync` present
- Dedicated assign API + DTO nulling guards retained

---

## 13. Discovered gaps / limitations

| Gap | Notes |
|---|---|
| AcademicYear on entry | Not modeled on TimetableEntry; validated via SA identity |
| College | Not on TG/Entry/SA model for this contract |
| Create with TG in one request | Deferred; explicit assign endpoint remains the path |
| Pre-existing incompatible rows | Rejected on next mutation until clear or repair |

---

## 14. Confirmations

| Item | Status |
|---|---|
| No UI changes | **Confirmed** |
| No automatic TG inference/creation | **Confirmed** |
| No TimetableSection mutation | **Confirmed** |
| No Attendance changes | **Confirmed** |
| No new migration | **Confirmed** |
| No RBAC weakening | **Confirmed** |

---

## Acceptance invariant

A persisted TimetableEntry may have no TeachingGroup, or exactly one explicitly assigned TeachingGroup that is compatible with the entry's current SubjectAllocation, tenant, and academic scope. Any mutation that would violate this invariant is rejected rather than silently clearing, replacing, inferring, or creating a TeachingGroup.

**STATUS = PASS**
