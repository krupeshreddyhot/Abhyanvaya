# AI-SCHED-TG.5 Prompt 5 — Membership Resolver & Mutation Implementation

**Workstream:** AI-SCHED-TG.5  
**Prompt:** 5 — Membership Resolver & Mutation Implementation  
**Date:** 2026-08-19  
**Type:** Application + API implementation (no UI mutation editor)  
**Status:** CONDITIONAL PASS

---

## 1. Resolver architecture

| Component | Role |
|---|---|
| `ITeachingGroupMembershipResolver` / `TeachingGroupMembershipResolver` | Side-effect-free Model B resolution |
| `ITeachingGroupMembershipApplicationService` / `TeachingGroupMembershipApplicationService` | Overlay reads + Explicit/Hybrid mutations |
| `TeachingGroupManagementApplicationService` | List/detail `ResolvedStudentCount` via resolver |

**Formula (Hybrid Model B):**

```text
Resolved = (Base ∪ ExplicitIncludes) − ExplicitExcludes
```

Exclude always wins. Distinct StudentIds. Deterministic order by StudentId ascending.

---

## 2. Membership source rules

| Source | Base | Overlays | Mutation |
|---|---|---|---|
| ExplicitStudents | empty | Include only | Add/Remove/Replace Includes |
| Section / CombinedSections | TeachingGroupSection → StudentSection | ignored | **Rejected** |
| StudentSubject | StudentSubject ∩ academic scope | ignored | **Rejected** |
| Hybrid | Sections if linked, else StudentSubject | Include + Exclude | Add/Remove/Replace overlays |

---

## 3. Model B formula

```text
Effective = (Base ∪ Includes) − Excludes
```

- Duplicate Includes/Excludes collapse to one StudentId.
- Exclude wins over Include and over Base.
- Excluded students are omitted from resolved membership (not returned as active members).

---

## 4. Mutation rules

| Operation | Explicit | Hybrid | Section-derived |
|---|---|---|---|
| Add Include | Yes | Yes | Reject |
| Remove | Ends Include | Ends Include; if in Base, adds Exclude | Reject |
| Replace | Replace Include set | Replace Include + Exclude overlay set | Reject |

Replace runs in one UoW (`SaveChanges` once after full validation). Capacity validated against proposed resolved count before commit.

---

## 5. Provenance

- `Derived` — from Base  
- `ExplicitInclude` — from Include overlay (wins label when also in Base)  
- Excluded students are **not** returned in resolved roster  

---

## 6. Capacity

- `ResolvedStudentCount` = resolver count (not persisted)  
- Mutation rejected when proposed Resolved > `MaxTeachingCapacity` (if set)  
- `ExpectedStudentCount` / `Room.Capacity` not mutated or checked here  

---

## 7. Lifecycle / RBAC

- Mutation gated by existing `TeachingGroup.EnsureCanMutate()` (allows Draft/Active; rejects Locked/Archived/deleted)  
- Domain has no separate `Published` / `Frozen` status — Locked/Archived cover non-mutable lifecycle  
- View: `Scheduling.TeachingGroup.View` → GET memberships + resolved-members  
- Manage: `Scheduling.TeachingGroup.Manage` → POST/PUT/DELETE memberships  

---

## 8. Tenant isolation

- Ambient query filters; no `IgnoreQueryFilters`  
- Student.TenantId must match TeachingGroup.TenantId  
- Academic scope: Course / Group / Semester (+ Section AcademicYear for derived)  

---

## 9. Concurrency / idempotency

- Unique current membership index `(Tenant, TG, Student)` filtered `IsCurrent && !IsDeleted`  
- Duplicate Add Include → idempotent success  
- Remove missing → idempotent success  
- `DbUpdateException` → `ConcurrencyConflictException` → HTTP **409**  

---

## 10. API contract

| Method | Path |
|---|---|
| GET | `/api/scheduling/teaching-groups/{id}/memberships` |
| GET | `/api/scheduling/teaching-groups/{id}/resolved-members` |
| POST | `/api/scheduling/teaching-groups/{id}/memberships` |
| PUT | `/api/scheduling/teaching-groups/{id}/memberships` |
| DELETE | `/api/scheduling/teaching-groups/{id}/memberships/{studentId}` |

DTOs only — no EF entities. Client cannot set TenantId, ResolvedStudentCount, audit, lifecycle, or academic scope fields.

---

## 11. Frozen boundaries preserved

- No TeachingGroup auto-create / SA inference  
- No StudentSection / StudentSubject / Attendance / TimetableSection writes  
- TeachingGroupSection remains section SoT; TG.4A projector untouched  
- No UI membership editor (Prompt 3 banner remains)  
- No migration  
- No hosted/startup reconciliation  

---

## 12. Test results

| Suite filter | Passed | Failed | Skipped |
|---|---:|---:|---:|
| `AiSchedTg5Prompt5*` | 16 | 0 | 0 |
| Combined Prompt5 + Prompt2 + Prompt4 + TG.4A Prompt8/10 | 58 | 0 | 0 |

Exact combined run (2026-08-19): **Passed: 58, Failed: 0, Skipped: 0**.

| Build | Result |
|---|---|
| `Abhyanvaya.API` | **0 Error(s)** |
| `abhyanvaya-ui` (`npm run build`) | **✓ built** |

---

## 13. Known limitations

1. AcademicYear eligibility uses Section.AcademicYearId for derived paths; Student has no AcademicYearId column.  
2. ExclusionGroupKey peer check focuses on explicit Include overlays on peer TGs (not full peer resolved sets).  
3. UI mutation editor deferred to a later prompt.  
4. Lock/Publish roster snapshot still deferred.  
5. No separate Published/Frozen enum values — use Locked/Archived via `EnsureCanMutate`.  
6. No live host RBAC integration suite (policies wired on controller; unit/architecture coverage only).  
7. InMemory provider does not enforce filtered unique indexes; race protection relies on PostgreSQL unique index + 409 translation.

---

## 14. Deferred UI work

Prompt 3 Teaching Groups page stays read-only for membership until a dedicated UI prompt consumes `resolved-members` + mutation APIs.

---

## 15. Artifacts

Implementation docs + sources copied under:

`D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI Scheduling Enhancement\AI-SCHED-TG.5\Prompt 5`
