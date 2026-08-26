# AI-SCHED-TG.2 Prompt 6 — Teaching Group Application / API Contract

**Workstream:** AI-SCHED-TG.2  
**Prompt:** 6 — API/application contract (design only)  
**Date:** 2026-08-17  

Follow existing Controller → Application → Domain → Infrastructure boundaries. DTOs only — never expose EF entities.

**No implementation.**

---

## Route prefix

`api/scheduling/teaching-groups`

Align with `api/scheduling/subject-allocations`, `api/scheduling/timetables`.

---

## Permissions (new keys following existing pattern)

| Permission | Use |
|---|---|
| `Scheduling.TeachingGroup.View` | List/get/members |
| `Scheduling.TeachingGroup.Manage` | Create/update/membership/derive/split |
| Fallback | Until provisioned, gate Manage under `Scheduling.Timetable.Manage` + View under `Scheduling.Timetable.View` **or** `Scheduling.Manage` — **prefer dedicated keys** at implementation |

Timetable attach uses existing `Scheduling.Timetable.Manage`.

---

## Endpoints (contracts only)

| Method | Path | Purpose |
|---|---|---|
| POST | `/` | Create TeachingGroup |
| PUT | `/{id}` | Update metadata/capacity/dates/status (not membership bulk) |
| GET | `/{id}` | Get detail |
| GET | `/` | List (filter academicYearId, courseId, groupId, semesterId, subjectId, subjectAllocationId, type, status) |
| GET | `/{id}/members` | Resolved members (dynamic + explicit) |
| PUT | `/{id}/members` | Replace explicit Includes/Excludes (Explicit/Hybrid) |
| POST | `/{id}/members:add` | Add students |
| POST | `/{id}/members:remove` | Remove / end-date |
| POST | `/from-section` | Derive SectionDerived |
| POST | `/combine-sections` | CombinedSections |
| POST | `/student-subset` | StudentSubset |
| POST | `/lab-batch` | Laboratory partition helper |
| POST | `/capacity-split` | Create N CapacitySplit groups from pool |
| POST | `/{id}/validate` | Validate invariants |
| GET | `/by-allocation/{subjectAllocationId}` | List for designer |

Timetable entry create/update DTOs gain `teachingGroupId` (required).

---

## DTO sketches

### TeachingGroupDto

`id, tenantId, academicYearId, courseId, groupId, semesterId, subjectId, subjectAllocationId, sectionGroupId?, type, membershipSource, status, code?, name, displayOrder, plannedCapacity, maxCapacity?, effectiveFrom, effectiveTo?, notes?, sectionIds[], memberCount, createdDate, updatedDate`

### CreateTeachingGroupRequest

Scope ids + `subjectAllocationId`, `type`, `membershipSource`, `name`, `code?`, `plannedCapacity`, `sectionIds?`, `studentIds?`, `effectiveFrom`

### CapacitySplitRequest

`subjectAllocationId`, `sourceSectionId?`, `sourceStudentIds?`, `splits: [{ name, plannedCapacity, studentIds[] }]`

### TeachingGroupMemberDto

`studentId, studentNumber?, studentName?, inclusion, effectiveFrom, effectiveTo?, isCurrent, source` (`Derived`|`Explicit`)

### ValidationProblem

Use existing API error shape (BadRequest message / problem details). User-facing messages only — no stack traces, no SQL, no internal claim dumps.

---

## Validation errors (examples)

| Code / message | Cause |
|---|---|
| Subject allocation not found / wrong scope | Mismatch |
| Student not in tenant / academic scope | Cross-tenant/scope |
| Duplicate student in group | Unique violation |
| Section not in Course/Group/Semester | Invalid combo |
| Membership change denied while Locked | Publication lock |
| Planned capacity exceeded by explicit roster | Soft or hard per policy |
| TeachingGroupId required on timetable entry | Missing FK |
| Allocation mismatch entry vs TG | Inconsistent |

---

## Tenant isolation

- All queries filter `TenantId`
- Membership add verifies student.TenantId
- No IgnoreQueryFilters

---

## Concurrency / idempotency

- Updates: If-Match / UpdatedDate check where peers do
- `capacity-split` / `combine-sections`: idempotent on identical payload → return existing groups when Codes match (document)

---

## Confirmation

**No endpoints implemented.**
