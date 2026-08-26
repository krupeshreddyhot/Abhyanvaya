# AI-SCHED-TG.5 Prompt 4 — Membership API Contract

**Date:** 2026-08-19  
**Type:** REST contract design — **DO NOT IMPLEMENT in this prompt**  

Aligns with existing Prompt 2 routes under `api/scheduling/teaching-groups`.

---

## 1. Route conventions

Retain plural **`memberships`** (already shipped in Prompt 2) rather than introducing a parallel `/members` resource.

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/api/scheduling/teaching-groups/{id}/memberships` | View | **Existing** — raw overlay rows |
| GET | `/api/scheduling/teaching-groups/{id}/resolved-members` | View | **New** — resolved roster |
| POST | `/api/scheduling/teaching-groups/{id}/memberships` | Manage | Add members (Includes / Hybrid rules) |
| PUT | `/api/scheduling/teaching-groups/{id}/memberships` | Manage | Replace overlays / explicit set |
| DELETE | `/api/scheduling/teaching-groups/{id}/memberships/{studentId}` | Manage | Remove one student (Explicit/Hybrid rules) |
| POST | `/api/scheduling/teaching-groups/{id}/memberships:remove` | Manage | Optional batch remove (alternative to N DELETEs) |

**Status codes**

| Code | When |
|---|---|
| 200 | Success (including idempotent no-op add/remove) |
| 400 | Domain/validation (`DomainException` message) |
| 401/403 | Auth (existing pipeline) |
| 404 | TG or (for DELETE) unknown student link when policy requires |
| 409 | Concurrency / unique current-row conflict |

---

## 2. DTOs (contract)

### Existing (keep)

`TeachingGroupMembershipDto` — raw overlay (Prompt 2).

### New

```text
ResolvedTeachingGroupMemberDto
  studentId: number
  provenance: "Derived" | "ExplicitInclude"
  excludedFromBase: boolean   // Hybrid: true if present in Base but excluded
  // optional display: studentNumber, studentName — only if consistent with existing student DTO patterns

AddTeachingGroupMembersRequest
  studentIds: number[]          // required, distinct, > 0
  effectiveFrom?: string        // DateOnly; default today UTC date

RemoveTeachingGroupMembersRequest
  studentIds: number[]
  effectiveTo?: string

ReplaceTeachingGroupMembershipsRequest
  // ExplicitStudents:
  includeStudentIds: number[]
  // Hybrid:
  includeStudentIds: number[]
  excludeStudentIds: number[]

TeachingGroupMembershipMutationResultDto
  teachingGroupId: number
  resolvedStudentCount: number
  memberships: TeachingGroupMembershipDto[]      // current overlays
  resolvedMembers?: ResolvedTeachingGroupMemberDto[]  // optional echo
```

**Never accept from client:** `tenantId`, audit fields, `resolvedStudentCount` as writable, `isCurrent` overrides that bypass server rules.

---

## 3. Validation errors (examples)

- Membership changes are not supported for this Teaching Group’s membership source.
- One or more students are outside the Teaching Group’s academic scope.
- Adding these students would exceed MaxTeachingCapacity.
- This student already belongs to a mutually exclusive Teaching Group.
- Archived Teaching Group cannot be mutated.

---

## 4. Idempotency

| Op | Behavior |
|---|---|
| POST add existing Include | 200, unchanged |
| DELETE / remove absent | 200, unchanged |
| PUT replace same set | 200, unchanged |

---

## 5. Concurrency

No ETag required in v1. Unique index conflicts → **409** with safe message. Clients should reload resolved + overlays after 409.

---

## 6. Compatibility

| Existing | Impact |
|---|---|
| Prompt 2 GET `/memberships` | Preserved |
| Prompt 3 read-only UI | Continues to work; later UI adds resolved-members + mutations |
| TG.4A section projection | Unaffected |
| Assign/clear TeachingGroup on TimetableEntry | Unaffected |

---

## 7. Implementation gate

No controller/service/DTO code in Prompt 4. Next implementation prompt must:

1. Add `ITeachingGroupMembershipApplicationService`  
2. Implement resolver used by list/detail `ResolvedStudentCount`  
3. Add mutation endpoints above  
4. Keep Attendance / StudentSection / TimetableSection untouched  
