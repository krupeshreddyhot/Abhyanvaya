# AI-SCHED-TG.6 Prompt 4 / Prompt 2A — Backend Compatible Teaching Group Query

**Workstream:** AI-SCHED-TG.6 Prompt 4  
**Sub-prompt:** 2A — Backend Compatible Teaching Group Query  
**Date:** 2026-08-19  
**Type:** Backend read-only API implementation  
**Status:** **PASS**

---

## 1. Architecture discovery

### Chain

```text
TimetableEntry
      ↓  (SubjectAllocationId + CourseId/GroupId/SemesterId/SubjectId)
SubjectAllocation scope (denormalized on entry)
      ↓
Compatible Teaching Groups (same SA + academic scope + attachable lifecycle)
```

### Authoritative compatibility rule

`TeachingGroupRules.EnsureCompatibleWithTimetableEntry` validates:

| Dimension | Fields |
|---|---|
| Tenant | `entry.TenantId` vs `teachingGroup.TenantId` |
| Lifecycle attach | `EnsureCanAttachToTimetableEntry` — rejects **Deleted** and **Archived** |
| SubjectAllocation | `SubjectAllocationId` |
| Academic scope | `CourseId`, `GroupId`, `SemesterId`, `SubjectId` |

**Not on TimetableEntry:** AcademicYear, College (AcademicYear lives on TeachingGroup/SA; College not in TG↔entry contract).

### Assignment / clear (unchanged)

- `PUT …/entries/{entryId}/teaching-group` → `TeachingGroupApplicationService.Assign…` → domain rule
- `DELETE …/entries/{entryId}/teaching-group` → clear only

### Status enum

`Draft=1`, `Active=2`, `Locked=3`, `Archived=4`

Attachable for **new** assignment: Draft, Active, Locked (`EnsureCanAttachToTimetableEntry` / `CanAttachToTimetableEntry`).

---

## 2. Query design

**Service:** `ICompatibleTeachingGroupQueryService` / `CompatibleTeachingGroupQueryService`  
**Controller:** `GET api/scheduling/timetables/entries/{entryId}/compatible-teaching-groups`  
**Auth:** `CanViewSchedulingTimetable` (controller-level View; method reaffirms View)

### Query phase (EF, `AsNoTracking`)

Predicate mirrors domain scope (tenant via global filters):

```text
SubjectAllocationId == entry.SubjectAllocationId
AND CourseId/GroupId/SemesterId/SubjectId match entry
AND Status != Archived
```

### Assigned TG transparency

If `entry.TeachingGroupId` points to a TG not in the assignable set (e.g. **Archived**), that TG is **still returned** once with `IsAssignedToEntry = true`.  
**Never silently cleared.**

### Projection

Direct map to `CompatibleTeachingGroupOptionDto` + `ResolvedStudentCount` via existing `ITeachingGroupMembershipResolver.ResolveCountAsync` (authoritative; no second resolver).

### Mutation phase

Unchanged — assign still calls `EnsureCompatibleWithTimetableEntry`.

**Principle:** Query predicate is a faithful translation of the same dimensions as the domain rule, not a competing business definition.

---

## 3. API contract

```http
GET /api/scheduling/timetables/entries/{entryId}/compatible-teaching-groups
→ 200 OK CompatibleTeachingGroupOptionDto[]
→ 404 when entry missing / not visible in tenant
→ 401/403 via existing auth
→ 200 [] when zero compatible (not an error)
```

DTO fields: `Id`, `Code`, `Name`, `Type`, `Status`, `ResolvedStudentCount`, `ExpectedStudentCount`, `MaxTeachingCapacity`, `IsAssignedToEntry`.

JSON uses existing camelCase serialization.

---

## 4. Authorization

Read: `CanViewSchedulingTimetable`  
No new permission. Server remains authoritative.

---

## 5. Tenant isolation

- Entry load: `ITimetableRepository.GetEntryByIdAsync(TenantId, …)`
- TG query: EF tenant query filters on `SchedulingTeachingGroups`
- **No** `IgnoreQueryFilters()`
- Cross-tenant entry → `KeyNotFoundException` → 404 (no leakage)

---

## 6. Lifecycle handling

| Status | In assignable query? | If currently assigned |
|---|---|---|
| Draft / Active / Locked | Yes | `IsAssignedToEntry` when matches |
| Archived | No | Still returned for transparency; entry **not** cleared |

---

## 7. Capacity semantics

- Returns Expected / Max / Resolved from TG + resolver
- **Never** filters by room capacity
- No `PlannedCapacity`

---

## 8. Performance considerations

- Single candidate TG query (not load-all + filter in memory across tenants/SAs)
- `AsNoTracking`
- No `SaveChanges` on GET path
- Resolved counts: per-candidate `ResolveCountAsync` (same pattern as TG management summaries). **Known limitation:** not a single SQL aggregate across heterogeneous membership sources; still uses the authoritative resolver rather than inventing a second count algorithm.

---

## 9. Test results

| Suite | Result |
|---|---|
| `CompatibleTeachingGroupQueryServiceTests` | **PASS** |
| `AiSchedTg6Prompt4Prompt2AArchitectureGuardTests` | **PASS** |
| Combined Prompt 2A filter (17 tests) | **PASS** |
| TG application boundary / EF / domain / section / TG.5 membership filters | **PASS** (included in focused regression) |
| API build | **PASS** |
| Frontend `tsc -b` (no UI changes) | **PASS** |

**Note:** Legacy `AiSchedTg5Prompt1UxArchitectureDiscoveryTests` still fail (outdated “no TG UI/controller yet” assertions from TG.5 Prompt 1 discovery). Pre-existing; not introduced by Prompt 2A.

---

## 10. Architecture guard results

Guards assert:

- Entry-scoped GET endpoint + View auth
- Assign/clear remain mutation owners
- No IgnoreQueryFilters / SaveChanges / TimetableSection / StudentSection / Attendance on query service
- DTO shape matches UI contract
- Create/Update/Upsert still omit TeachingGroupId
- No migration

---

## 11. Regression results

- Assign/clear controllers unchanged in behavior
- No schema/migrations
- No UI modifications
- No TeachingGroupSection / TimetableSection / Attendance / StudentSection / membership semantic changes

---

## 12. Known limitations

1. Resolved count uses per-TG resolver calls (documented; avoids second membership algorithm).
2. Live HTTP auth integration (WebApplicationFactory) not added; unit + source guards cover policy wiring.
3. Stale TG.5 Prompt 1 discovery tests remain red until separately retired.

---

## 13. Final readiness decision

**PASS**

Criteria met:

- [x] API works (application + controller)
- [x] Tenant isolation
- [x] Authorization wiring (View)
- [x] Compatibility tests
- [x] No client-side filtering in this backend
- [x] No automatic TG creation / SA→TG inference
- [x] No TimetableSection writes / Attendance changes
- [x] No schema migration
- [x] Assign/clear unchanged
- [x] Focused regression + API build + UI typecheck pass
