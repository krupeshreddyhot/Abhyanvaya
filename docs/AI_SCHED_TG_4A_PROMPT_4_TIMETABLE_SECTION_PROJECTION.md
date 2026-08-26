# AI-SCHED-TG.4A Prompt 4 — TimetableSection Projection

**Workstream:** AI-SCHED-TG.4A  
**Prompt:** 4 — TimetableSection projection / synchronization  
**Date:** 2026-08-18  
**Predecessor:** AI-SCHED-TG.4A Prompt 3 (PASS — TeachingGroupSection SoT)

**STATUS: PASS**

---

## 1. Delivered

| Component | Role |
|---|---|
| `ITimetableSectionProjector` | Approved TimetableSection writer |
| `TimetableSectionProjector` | Idempotent soft-delete + insert sync |
| `ReplaceSectionsAndProjectAsync` | SoT + projection + **one** `SaveChanges` |

### Projection rules

1. Existing matching `(Timetable, Entry, Section)` rows kept  
2. Missing rows created  
3. Obsolete rows soft-deleted (`IsDeleted = true`)  
4. No duplicate active rows  
5. Idempotent  
6. Tenant-filtered loads; no `.IgnoreQueryFilters`  
7. Never creates/infers TeachingGroup  
8. Never changes `TimetableEntry.TeachingGroupId`  
9. Never touches StudentSection / Attendance / SubjectAllocation  
10. Re-projects **all** TimetableEntries with that `TeachingGroupId`

### Transaction (bridge-ready)

```text
Validate
  → Update TeachingGroupSection (staged)
  → Find all affected TimetableEntries
  → Replace their TimetableSections (staged)
  → SaveChanges once
```

- Projector **does not** call `SaveChanges`.  
- `ReplaceSectionsAndProjectAsync` is the single-commit orchestrator for the future `/sections` bridge.  
- Prevents: SoT committed while TimetableSection (Attendance read model) remains stale.

`ReplaceSectionsAsync` (SoT-only) remains for non-bridge callers that do not need projection in the same commit.

---

## 2. Out of scope (held)

- Retrofit `PUT /sections` (Prompt 5)  
- UI / Attendance / RBAC changes  
- Automatic TG creation  

---

## 3. Tests

`TimetableSectionProjectorTests` + updated architecture guards:

one/multi section, remove one/all, idempotent repeat, no duplicates, all entries sharing TG, no TGId mutation, no TG create, entry without TG rejects single sync, cross-tenant, single-commit coherence, no StudentSection writes, projector has no SaveChanges.

---

## 4. DI

`ITimetableSectionProjector` → `TimetableSectionProjector` (scoped).

---

**STATUS = PASS**
