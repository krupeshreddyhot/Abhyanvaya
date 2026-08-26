# AI-SCHED-TG.4A Prompt 3 — TeachingGroupSection Application Boundary

**Workstream:** AI-SCHED-TG.4A  
**Prompt:** 3 — TeachingGroupSection application boundary (source of truth)  
**Date:** 2026-08-18  
**Predecessor:** AI-SCHED-TG.4A Prompt 2 (PASS — bridge contract)

**STATUS: PASS**

---

## 1. Delivered

| Component | Location |
|---|---|
| `ITeachingGroupSectionApplicationService` | `Abhyanvaya.Application/Scheduling/` |
| `TeachingGroupSectionApplicationService` | same |
| `ITimetableSectionProjector` | Interface only (Prompt 4 implements) |
| DTOs | `TeachingGroupSectionDtos.cs` |
| Domain | `TeachingGroupRules.EnsureSectionCompatibleWithTeachingGroup` |
| DI | Scoped registration (projector **not** registered) |

### Operations

| Method | Behavior |
|---|---|
| `GetSectionsAsync` | List active TeachingGroupSection links |
| `ReplaceSectionsAsync` | Soft-delete removed; add missing; type + scope validation |
| `AddSectionAsync` | Single add; duplicate rejected |
| `RemoveSectionAsync` | Soft-delete one; remaining set must still satisfy type rules |

---

## 2. Validation enforced

- Tenant (query filters; no `.IgnoreQueryFilters`)
- TeachingGroup existence / not-found
- `EnsureCanMutate` (Locked / Archived / deleted rejected)
- Section existence (missing/other-tenant → invalid)
- Academic scope: AcademicYear / Course / Group / Semester
- `TeachingGroupRules.ValidateSectionLinks` (0 / 1 / many by type)
- Duplicate active link rejected on Add

**Not done (by design):** TimetableSection writes, `/sections` retrofit, TG create/inference, StudentSection, Attendance, UI, RBAC redesign.

---

## 3. Projection hook

`ITimetableSectionProjector.SyncTeachingGroupSectionsToTimetableEntriesAsync` is defined for Prompt 4/5 same-transaction use. Prompt 3 does **not** implement or register it.

---

## 4. Tests

`TeachingGroupSectionApplicationBoundaryTests` + `TeachingGroupSectionArchitectureGuardTests`:

valid replace, cross-tenant, wrong scope, duplicate, multi/combined, remove one, clear all, SectionDerived clear reject, archived/locked, no TG create, no TimetableSection/StudentSection side effects, no IgnoreQueryFilters, `/sections` still legacy until Prompt 5.

---

## 5. Authorization

Application service relies on ambient tenant context. API authorization for legacy PUT remains `CanManageSchedulingTimetable` (enforced when Prompt 5 wires the bridge). No RBAC weakening.

---

**STATUS = PASS**
