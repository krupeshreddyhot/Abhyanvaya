# AI-SCHED-TG.4A Prompt 5 — Legacy `/sections` Bridge Retrofit

**Workstream:** AI-SCHED-TG.4A  
**Prompt:** 5 — Retrofit `PUT /api/timetable/{id}/sections` through TeachingGroup boundary  
**Date:** 2026-08-18  
**Predecessor:** AI-SCHED-TG.4A Prompt 4 (PASS — projection + single-commit path)

**STATUS: PASS**

---

## 1. External contract (unchanged)

| Element | Value |
|---|---|
| Route | `PUT /api/timetable/{timetableId}/sections` |
| Body | `SetTimetableSectionsRequest { TimetableEntryId?, SectionIds }` |
| Response | `IReadOnlyList<TimetableSectionDto>` |
| Auth | `CanManageSchedulingTimetable` |
| GET | Still reads TimetableSection projection |

No UI redesign. No Attendance changes. No new request fields.

---

## 2. Internal flow (implemented)

```text
PUT /sections
  → Validate timetable (tenant)
  → EnsureDraft
  → Require TimetableEntryId + load entry (tenant + timetable)
  → Require TeachingGroupId (else actionable 400; no auto-create / no SA inference)
  → ReplaceSectionsAndProjectAsync(teachingGroupId, sectionIds)
       → TeachingGroupSection SoT
       → Project all entries with that TG
       → SaveChanges once
  → Return GetTimetableSectionsAsync (projection DTO)
```

**File:** `SectionManagementService.SetTimetableSectionsAsync`  
**Controller:** catches `DomainException` → 400 (additive; contract-compatible)

---

## 3. Explicit non-behaviors

| Forbidden | Enforced |
|---|---|
| Direct `new TimetableSection` in Set path | Removed |
| Auto TeachingGroup create | Reject when `TeachingGroupId` null |
| SubjectAllocation → TG inference | Not present |
| IgnoreQueryFilters | Not used |
| Attendance / UI / RBAC redesign | Untouched |

---

## 4. Tests

`LegacyTimetableSectionsBridgeTests` + updated architecture guards:

- SoT + projection updated via legacy PUT  
- Response shape compatible  
- Null TG rejected, no TG created  
- Missing entry id rejected  
- Locked timetable rejected  
- Source: no direct TimetableSection construction; uses `ReplaceSectionsAndProjectAsync`  
- API auth/route unchanged  

---

**STATUS = PASS**
