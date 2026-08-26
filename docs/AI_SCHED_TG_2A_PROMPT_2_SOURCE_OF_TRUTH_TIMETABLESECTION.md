# AI-SCHED-TG.2A Prompt 2 — Teaching Group Source-of-Truth & TimetableSection Projection

**Workstream:** AI-SCHED-TG.2A  
**Prompt:** 2 — Source-of-truth & TimetableSection clarification  
**Date:** 2026-08-17  
**Type:** DESIGN ONLY  

**No production code, schema, API, or UI was changed in this prompt.**

---

## 1. Executive Summary

AI-SCHED-TG.2 correctly placed TeachingGroup above TimetableEntry but left a dual-write risk:

- Write path A: TeachingGroup → TeachingGroupSection → (sync) TimetableSection  
- Write path B: existing `PUT /api/timetable/{id}/sections` → **direct** TimetableSection mutation (`SectionManagementService.SetTimetableSectionsAsync`)

**Approved rule:** TeachingGroup is the **only** authoritative write model for operational teaching cohorts. TimetableSection is a **derived compatibility projection**. Legacy section APIs must translate through the TeachingGroup application boundary — never bypass it.

### STATUS: **PASS**

---

## 2. Authoritative model

```text
SubjectAllocation
        ↓
TeachingGroup                 ← authoritative operational cohort
        ↓
TeachingGroupSection          ← TG ↔ academic Section link (when applicable)
        ↓
TimetableSection              ← projection / compatibility read model only
        ↓
TimetableEntry.TeachingGroupId
```

| Concept | Role |
|---|---|
| Section | Academic structure (`StudentSection`) |
| TeachingGroup | Operational teaching cohort (SoT) |
| TeachingGroupSection | Section association for SectionDerived / Combined / lab parent |
| TeachingGroupMembership | Explicit operational students |
| TimetableSection | Projection for legacy APIs + current AttendanceSessionResolver enrichment |
| TimetableEntry | Placement; references TeachingGroupId |

---

## 3. Source-of-truth problem

### Current code (as inspected)

| Path | Behavior |
|---|---|
| `GET /api/timetable/{id}/sections` | Reads `TimetableSections` (`SectionManagementService.GetTimetableSectionsAsync`) |
| `PUT /api/timetable/{id}/sections` | Soft-deletes existing rows for entry; inserts new `TimetableSection` rows **directly** |
| `AttendanceSessionResolver` | Joins `TimetableSections` for SectionIds enrichment |
| TG.2 design | Proposed TG as SoT + optional sync to TimetableSection |

If both PUT TimetableSections and TG services write independently:

```text
TeachingGroup  ←→  TimetableSection
     ↑                    ↑
   TG API            Legacy PUT
```

…cohort identity diverges (classic dual authority).

---

## 4. Canonical direction (APPROVED)

```text
TeachingGroup
      ↓  (owns identity + membership semantics)
TeachingGroupSection
      ↓  (application projects)
TimetableSection projection
```

### Explicit statements

1. **TeachingGroup owns** operational teaching-group identity.  
2. **TeachingGroup membership** (resolver + explicit rows) is authoritative for “who is taught.”  
3. **TeachingGroupSection** expresses TG ↔ Section relationships where they exist.  
4. **TimetableSection must not** independently redefine TeachingGroup membership.  
5. **Legacy section-oriented APIs**, if retained, **must** enter the TeachingGroup application boundary.

**Forbidden:**

```text
Legacy request → direct TimetableSection mutation (bypassing TeachingGroup)
```

**Required:**

```text
Legacy/compatibility request
        ↓
Application translation (ITeachingGroupTimetableBridge / equivalent)
        ↓
TeachingGroup operation (create/update CombinedSections / SectionDerived links)
        ↓
TeachingGroupSection
        ↓
TimetableSection projection writer (internal only)
```

---

## 5. Write vs read matrix

| Operation | Allowed writer | Result |
|---|---|---|
| Create/update TG | TeachingGroup application service | SoT |
| Change TG sections | TeachingGroup application service | Updates TeachingGroupSection → projects TimetableSection |
| Change TG students | TeachingGroup membership APIs | SoT; never StudentSection |
| `PUT /timetable/{id}/sections` | **Facade only** → TG service | No direct DbContext TimetableSection writes from controller path |
| `GET /timetable/{id}/sections` | Read projection (TimetableSection **or** derive from TG) | Read-only |
| Attendance resolver | Read TG first (preferred); fallback TimetableSection during transition | Must converge |
| Timetable designer | Selects TeachingGroupId | Entry write; may trigger projection refresh |

**Internal projection writer** is the only component allowed to insert/update TimetableSection rows, and only as a slave of TeachingGroupSection + TimetableEntry linkage.

---

## 6. Legacy API compatibility

### Existing endpoints

| Endpoint | Today | After TG introduction |
|---|---|---|
| `GET /api/timetable/{timetableId}/sections` | List TimetableSection DTOs | Keep contract; populate from projection **or** map from TeachingGroupSection for entries on that timetable |
| `PUT /api/timetable/{timetableId}/sections` | Direct mutate | **Translate**: resolve/create TeachingGroup for the entry’s SubjectAllocation; set TeachingGroupSection to request.SectionIds; set entry.TeachingGroupId; project TimetableSection; return same DTO shape |

### Translation rules for PUT body (`TimetableEntryId` + `SectionIds`)

1. Load TimetableEntry (tenant-safe); require SubjectAllocationId.  
2. If entry has TeachingGroupId: update that TG’s TeachingGroupSection set to SectionIds (type CombinedSections if count>1 else SectionDerived).  
3. If entry lacks TeachingGroupId (transition only): create SectionDerived/Combined TG under allocation, attach to entry, then set sections.  
4. Rebuild TimetableSection projection for that entry.  
5. Do not edit StudentSection.  
6. Respect Locked TG / Published timetable membership rules from TG.2/2A.

### GET behavior

Prefer: for each entry, sections = TeachingGroupSection of entry.TeachingGroupId; if projection table retained, it must match.  
Mismatch detection in Architecture Guard / validation tests: projection vs SoT → fail build/acceptance.

---

## 7. Attendance integration

**Target:**

```text
TimetableEntry → TeachingGroupId → membership resolver → students
                 → TeachingGroupSection → SectionIds (UI / session metadata)
```

**Transition:** AttendanceSessionResolver may keep reading TimetableSection **only if** projection is guaranteed in sync. Prefer switching SectionIds source to TeachingGroupSection in the same implementation slice as the bridge.

Legacy attendance fallback (Course→Group→Semester→Subject→Period) unchanged.

---

## 8. Dual-write prohibition (enforcement guidance for TG.3+)

| Control | Guidance |
|---|---|
| Code | `SetTimetableSectionsAsync` becomes orchestration calling TG services; no raw Add TimetableSection except projection helper |
| Architecture Guard | Disallow new direct TimetableSection writes outside projection helper namespace |
| Tests | PUT sections → assert TeachingGroupSection SoT; assert projection matches |
| Docs | Mark TimetableSection as “projection” in ADL module notes |

---

## 9. Relationship to capacity semantics (2A Prompt 1)

Source-of-truth for **who** is membership; capacity fields (Expected/Max/Resolved) do not create a second write path. TimetableSection never stores capacity.

---

## 10. Explicit architecture decisions

1. TeachingGroup is sole write SoT for operational cohorts.  
2. TimetableSection is projection/compatibility only.  
3. `PUT /api/timetable/{id}/sections` must go through TG application translation.  
4. Direct TimetableSection mutation from feature code is forbidden after TG cutover.  
5. Projection sync is one-way: TG → TimetableSection.  
6. No TimetableSection → TeachingGroup inference as a write path.  
7. Reuse Scheduling/Timetable/Governance engines; no parallel scheduler.

---

## 11. Rejected alternatives

| Alternative | Why |
|---|---|
| Keep TimetableSection as co-equal SoT | Dual authority |
| Drop TimetableSection immediately without facade | Breaks existing UI/API/attendance consumers |
| Infer TG from TimetableSection on every read without owning writes | Hidden second model |
| Bidirectional sync | Conflict-prone; violates single writer |

---

## 12. Impact on AI-SCHED-TG.3+

| Slice | Work |
|---|---|
| TG.3 | Entities include TeachingGroupSection; TimetableEntry.TeachingGroupId |
| TG.4–5 | TeachingGroup services + APIs |
| TG.6 | **Critical:** refactor `SetTimetableSectionsAsync` to TG bridge; projection writer; designer uses TeachingGroupId |
| TG.7 | Attendance prefers TG; projection fallback only if sync proven |
| Tests / Guard | Dual-write regression tests |

---

## 13. Open questions

| # | Question | Resolution |
|---|---|---|
| 1 | Keep TimetableSection table permanently? | **Optional long-term**; required through cutover for GET/PUT + resolver. May deprecate after consumers move to TG APIs. |
| 2 | GET may read TG only and stop writing projection? | Allowed later; PUT facade still must update SoT. |

No fundamental SoT ambiguity remains.

---

## 14. Files inspected

- `SectionManagementService.SetTimetableSectionsAsync` / `GetTimetableSectionsAsync`
- `TimetableSection` entity; `ApplicationDbContext.TimetableSections`
- `AttendanceSessionResolver` TimetableSections join
- UI: `sectionService.setTimetableSections` / `listTimetableSections`
- AI-SCHED-TG.2 schema/integration/final decision docs

---

## 15. Final readiness

Source-of-truth and TimetableSection projection rules are unambiguous for implementation.

### STATUS: **PASS**

**Confirmation:** No production code, database, API, or UI was modified.
