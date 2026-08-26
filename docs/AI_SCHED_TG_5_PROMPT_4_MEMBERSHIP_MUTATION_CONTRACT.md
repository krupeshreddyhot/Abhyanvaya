# AI-SCHED-TG.5 Prompt 4 — Membership Mutation Contract

**Date:** 2026-08-19  
**Type:** Application contract — **DO NOT IMPLEMENT in this prompt**  

---

## 1. Application boundary

```text
UI
 │
 ▼
TeachingGroupsController (membership routes)
 │
 ▼
ITeachingGroupMembershipApplicationService   ← NEW (future prompt)
 │
 ▼
Domain rules (TeachingGroup / TeachingGroupRules)
 │
 ▼
TeachingGroupMembership persistence
```

**Forbidden writers:** Controllers→DbContext, UI→EF, Membership service→Attendance / StudentSection / TimetableEntry / TimetableSection.

**Related existing services (do not overload):**

- `ITeachingGroupManagementApplicationService` — TG CRUD/archive; membership GET may remain or move
- `ITeachingGroupSectionApplicationService` — section SoT + projection

---

## 2. Operations (approved set)

| Operation | Purpose | Allowed MembershipSource |
|---|---|---|
| `GetResolvedMembers` | Resolved roster (+ optional provenance) | All |
| `GetMembershipOverlays` | Raw Include/Exclude rows | Explicit / Hybrid (others empty) |
| `AddMembers` | Add Explicit Includes (idempotent) | ExplicitStudents, Hybrid |
| `RemoveMembers` | End-date / soft-remove Includes; or add Exclude in Hybrid | ExplicitStudents, Hybrid |
| `ReplaceMembers` | Replace full explicit Include set (Explicit) or full overlay set (Hybrid) | ExplicitStudents, Hybrid |

**Not in v1 mutation contract:** auto-sync “derive all section students into Includes”, capacity-split helper, from-section factory (may be later prompts).

**Rejected for dynamic sources (Section / Combined / StudentSubject):** Add/Remove/Replace of materialised rows as the primary way to change resolved membership — change sections or subject enrollment instead.

---

## 3. Operation specifications

### 3.1 GetResolvedMembers

| Aspect | Spec |
|---|---|
| Auth | `Scheduling.TeachingGroup.View` |
| Request | `teachingGroupId` |
| Response | `ResolvedTeachingGroupMemberDto[]` — `studentId`, optional display fields if already available via existing student read patterns, `provenance` (`Derived` \| `ExplicitInclude`), `isExcludedFromBase` (Hybrid only) |
| Validation | TG exists in tenant |
| Side effects | **None** (no write) |
| Idempotency | N/A |

### 3.2 GetMembershipOverlays

| Aspect | Spec |
|---|---|
| Auth | View |
| Behavior | Current Prompt 2 `GET .../memberships` (raw rows) — retain for audit/debug/UI overlay editor |
| Side effects | None |

### 3.3 AddMembers

| Aspect | Spec |
|---|---|
| Auth | `Scheduling.TeachingGroup.Manage` |
| Request | `{ studentIds: int[], effectiveFrom?: DateOnly }` |
| Response | Updated overlays + `resolvedStudentCount` |
| Validation | TG mutable; source Explicit/Hybrid; eligibility; MaxTeachingCapacity; ExclusionGroupKey; duplicate current Include → idempotent success or 409 per ADR below |
| Transaction | Single SaveChanges |
| Idempotency | Re-adding an already-current Include is **idempotent success** (no duplicate row) |
| Audit | `IAuditService` + BaseEntity fields |
| Capacity | Reject if post-add Resolved > MaxTeachingCapacity (when Max set) |
| Attendance / sections / timetable | Untouched |

**Duplicate decision:** Idempotent success (HTTP 200) preferred over 409 for Add of existing Include.

### 3.4 RemoveMembers

| Aspect | Spec |
|---|---|
| Auth | Manage |
| Request | `{ studentIds: int[], effectiveTo?: DateOnly }` |
| ExplicitStudents | End current Include (`IsCurrent=false`, `EffectiveTo`, optionally soft-delete) |
| Hybrid | If student only in Base: create/update **Exclude** overlay; if student was Explicit Include: end Include; if both, end Include and ensure Exclude as needed so Resolved drops |
| Idempotency | Removing absent member → idempotent success |
| Capacity | Recompute Resolved; never auto-edit Expected/Max |

### 3.5 ReplaceMembers

| Aspect | Spec |
|---|---|
| Auth | Manage |
| ExplicitStudents | Replace desired Include StudentId set (diff add/end) |
| Hybrid | Body includes `includes[]` and `excludes[]` desired current overlays |
| Validation | Same as Add/Remove aggregated after apply |
| Transaction | Single SaveChanges |
| Concurrency | See §5 |

---

## 4. Error contract (user-safe)

Examples:

- Teaching Group was not found.
- Archived or locked Teaching Groups cannot change membership.
- This student is outside the Teaching Group’s academic scope.
- Adding this student would exceed MaxTeachingCapacity.
- This student already belongs to another Teaching Group in the same exclusion group.
- Membership changes are not supported for section-derived Teaching Groups; change section links instead.

No stack traces, SQL, paths, or tenant dumps.

---

## 5. Concurrency

| Finding | Decision |
|---|---|
| RowVersion on membership | **Not present** |
| Unique current index | Prevents duplicate current rows |
| Strategy (v1) | **Optimistic conflict via unique violation → 409**; no last-write-wins silent merge of Replace |
| ReplaceMembers | Load current overlays in transaction; apply diff; on unique failure → conflict error |
| Stale Resolved from section moves | Expected — Resolved is live; UI should refresh after section or membership ops |

Do **not** invent a checksum field in this contract phase unless a later implementation prompt adds a standard concurrency token consistent with ADL.

---

## 6. Audit requirements

Reuse `IAuditService.RecordAsync` for each successful mutating operation:

| Field | Value |
|---|---|
| entityName | `TeachingGroupMembership` / `TeachingGroup` |
| entityId | TG id and/or membership id |
| action | Create / Update / Delete (soft) |
| oldValues / newValues | studentIds, inclusion, effective dates, resolved count before/after |

Plus BaseEntity `CreatedBy` / `UpdatedBy` from ambient user.

---

## 7. Authorization summary

| Op | Permission |
|---|---|
| Get resolved / overlays | View |
| Add / Remove / Replace | Manage |

UI checks are convenience only.

---

## 8. Future UI boundary (Prompt 6+)

Allowed after implementation:

- Show resolved members
- Show/edit explicit overlays when source Explicit/Hybrid
- Hide mutation for dynamic-only sources (guide user to Sections)

**Not in Prompt 4:** any React editor implementation.
