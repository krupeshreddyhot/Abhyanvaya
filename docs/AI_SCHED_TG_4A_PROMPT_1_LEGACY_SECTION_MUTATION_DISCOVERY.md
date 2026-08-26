# AI-SCHED-TG.4A Prompt 1 — Legacy Section Mutation Discovery

**Workstream:** AI-SCHED-TG.4A — Legacy TimetableSection Bridge & TeachingGroup Projection  
**Prompt:** 1 — Architecture Discovery & `/sections` Mutation Trace  
**Date:** 2026-08-18  
**Type:** DISCOVERY ONLY  

**Production changes:** **None.** No database, entity, EF, migration, API, UI, Attendance, RBAC, TeachingGroup schema, or timetable behavior was modified.

**STATUS: PASS**

---

## Executive summary

| Finding | Status |
|---|---|
| Primary mutation endpoint | `PUT /api/timetable/{timetableId}/sections` |
| Application owner today | `SectionManagementService.SetTimetableSectionsAsync` (Academic boundary — **not** TimetableService) |
| Mutation style | Soft-delete existing rows for `(timetableId, TimetableEntryId)` then insert new `TimetableSection` rows |
| TeachingGroup involvement | **None** |
| TeachingGroupSection involvement | **None** |
| Sole production write of `TimetableSection` | `SetTimetableSectionsAsync` only |
| Attendance section resolve | Reads `TimetableSections` in Timetable mode; Legacy fallback intact |
| Clone / schedule-version | **Do not** copy or write `TimetableSection` |
| Future SoT | Must become `TeachingGroupSection` (projection → `TimetableSection`) |

---

## Dependency map

```text
┌─────────────────────────────────────────────────────────────────────────┐
│ UI (discovery only — not modified)                                      │
│ abhyanvaya-ui/src/services/sectionService.ts                            │
│   listTimetableSections / setTimetableSections                          │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ API                                                                     │
│ Abhyanvaya.API/Controllers/SectionsController.cs                        │
│   TimetableSectionsController                                           │
│   GET  api/timetable/{timetableId}/sections  → CanViewSchedulingTimetable│
│   PUT  api/timetable/{timetableId}/sections  → CanManageSchedulingTimetable│
└───────────────────────────────┬─────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ Application                                                             │
│ Abhyanvaya.Application/Academic/ISectionManagementService.cs            │
│ Abhyanvaya.Application/Academic/SectionManagementService.cs             │
│   GetTimetableSectionsAsync                                             │
│   SetTimetableSectionsAsync   ◄── ONLY TimetableSection writer          │
│   GetCombinedSessionsAsync    ◄── read                                  │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│ Persistence                                                             │
│ Domain: Abhyanvaya.Domain/Entities/Academic/TimetableSection.cs         │
│ DbSet:  ApplicationDbContext.TimetableSections → table "TimetableSections"│
│ Index:  (TenantId, TimetableId, TimetableEntryId, SectionId) UNIQUE     │
│         WHERE IsDeleted = FALSE                                         │
└─────────────────────────────────────────────────────────────────────────┘

READ consumers (non-exhaustive but complete for scheduling/attendance):
  AttendanceSessionResolver          → SectionIds for Timetable mode
  SectionReadinessService            → AnyAsync TimetableSections
  SectionHealthService               → AnyAsync TimetableSections
  MergePreviewService / SplitPreview → CountAsync TimetableSections
  SectionAllocationContextBuilder    → CountAsync TimetableSections
  GetCombinedSessionsAsync           → multi-section sessions
  GET /api/timetable/{id}/sections   → list for designer
```

**Not on the write path today:**

- `TimetableService` / `TeachingGroupApplicationService`
- `TeachingGroup` / `TeachingGroupSection`
- `SubjectAllocation` inference
- Attendance write APIs

---

## A. Current mutation flow

### 1. API / controller

| Item | Reference |
|---|---|
| Class | `TimetableSectionsController` |
| File | `Abhyanvaya.API/Controllers/SectionsController.cs` (lines ~183–206) |
| Route | `[Route("api/timetable/{timetableId:int}/sections")]` |
| Method | `[HttpPut] Set(...)` |
| Class auth | `[Authorize(Policy = CanViewSchedulingTimetable)]` |
| PUT auth | `[Authorize(Policy = CanManageSchedulingTimetable)]` (method override) |
| Errors | `KeyNotFoundException` → 404; `InvalidOperationException` → 400 |

### 2. Request DTO

| Item | Reference |
|---|---|
| Type | `SetTimetableSectionsRequest` |
| File | `Abhyanvaya.Application/DTOs/Academic/SectionDtos.cs` |
| Shape | `TimetableEntryId: int?`, `SectionIds: IReadOnlyList<int>` |
| Response | `IReadOnlyList<TimetableSectionDto>` (`Id`, `TimetableId`, `TimetableEntryId`, `SectionId`, `SectionCode`, `SectionName`) |

**Compatibility note:** Request does **not** include `TeachingGroupId`. Callers identify a timetable + entry + section id list only.

### 3. Timetable application service

**Not used.** Mutation lives in the Academic `ISectionManagementService` / `SectionManagementService`, not `ITimetableService` / `TimetableService`.

### 4. TimetableEntry loading

**Not performed.** `SetTimetableSectionsAsync` validates timetable existence only:

```csharp
var ttOk = await _db.SchedulingTimetables.AnyAsync(
    t => t.Id == timetableId && t.TenantId == _currentUser.TenantId, ...);
```

It does **not**:

- Load `TimetableEntry` by `request.TimetableEntryId`
- Verify entry belongs to the timetable
- Check Draft / Published / Locked lifecycle
- Read or require `TimetableEntry.TeachingGroupId`

### 5–6. TimetableSection deletion / insertion

**File:** `SectionManagementService.SetTimetableSectionsAsync`  
**Lines:** ~329–366

Algorithm:

1. Load existing `TimetableSections` for `(TenantId, TimetableId, TimetableEntryId)`.
2. Soft-delete each (`IsDeleted = true`, `UpdatedDate = UtcNow`).
3. Distinct positive `SectionIds`.
4. For each section id: tenant check on `Sections`; `AddAsync(new TimetableSection { ... })`.
5. Single `SaveChangesAsync`.
6. Return `GetTimetableSectionsAsync(timetableId)` (all sections for the **timetable**, not only the entry).

**Lifecycle semantics of removal:** soft-delete (not hard delete). Unique index filter `IsDeleted = FALSE` allows re-insert of same `(Tenant, Timetable, Entry, Section)`.

### 7–8. TeachingGroup / TeachingGroupSection lookup

**None.** Zero references in `SetTimetableSectionsAsync` / `GetTimetableSectionsAsync`.

### 9. Transaction boundaries

- Single `SaveChangesAsync` after soft-deletes + inserts.
- No explicit `BeginTransaction` / ambient transaction wrapper.
- Soft-delete + insert participate in one EF change tracker flush (atomic for this DbContext save).

### 10. Tenant validation

| Check | Present? |
|---|---|
| Timetable `TenantId == current` | Yes |
| Existing TimetableSection filter by tenant | Yes |
| Section `TenantId == current` | Yes |
| TimetableEntry tenant / ownership | **No** (entry not loaded) |
| TeachingGroup tenant | N/A (unused) |
| `.IgnoreQueryFilters()` | **Not used** on this path |
| Global query filters on `BaseEntity` | Yes (tenant + soft-delete via `ApplicationDbContext`) |

### 11. Authorization

| Policy | Permission wiring (`Program.cs`) |
|---|---|
| `CanViewSchedulingTimetable` | View (+ manage implied for view policy setup) |
| `CanManageSchedulingTimetable` | `PermissionKeys.SchedulingTimetableManage` |

Constants: `Abhyanvaya.API/Common/AuthorizationPolicies.cs`.

Faculty / read-only users without Manage cannot PUT.

### 12. Empty section list

`SectionIds = []` → soft-delete all existing rows for that entry; insert nothing. Combined-class support is “N sections”; zero is valid for this API today.

### 13. Gaps vs TG.4A target

| Gap | Risk for bridge |
|---|---|
| No TeachingGroup required | Silent SoT bypass |
| No TeachingGroupSection write | TimetableSection remains competing SoT |
| No Draft lifecycle guard | Sections can be set on frozen/published if auth allows |
| No entry existence check | Orphan TimetableSection rows possible |
| No academic scope check beyond tenant | Section from wrong Course/Group/Semester possible |
| Response returns **all** timetable sections | Callers must filter by `TimetableEntryId` |

---

## B. Current read flow

### Timetable sections API

| Step | Reference |
|---|---|
| GET | `TimetableSectionsController.Get` |
| Service | `GetTimetableSectionsAsync(timetableId)` |
| Query | `TimetableSections` where `TenantId` + `TimetableId` (AsNoTracking) |
| Map | `MapTimetableSectionsAsync` joins `Sections` for code/name |

### Combined sessions

| Step | Reference |
|---|---|
| API | `SectionsController.CombinedSessions` → `GetCombinedSessionsAsync` |
| Logic | Groups by `(TimetableId, TimetableEntryId)` where distinct SectionId count > 1 |

### Scheduling Timetable reads

`TimetableService` grid/projection DTOs **do not** include TimetableSection lists. Section association for designer/attendance is via the separate `/timetable/{id}/sections` API (and Attendance resolver).

### UI

`abhyanvaya-ui/src/services/sectionService.ts`:

- `listTimetableSections(timetableId)` → GET  
- `setTimetableSections(timetableId, body)` → PUT  

(Discovery only — UI not changed.)

---

## C. Current Attendance dependency

| Item | Reference |
|---|---|
| Resolver | `Abhyanvaya.Application/Scheduling/Conflicts/AttendanceSessionResolver.cs` |
| Mode | Timetable vs Legacy |
| Section source (Timetable mode) | JOIN `TimetableSections` → `Sections` filtered by tenant, `TimetableId`, and `(TimetableEntryId == null \|\| == entry.Id)` |
| Output | `SectionIds`, `SectionCodes` on `AttendanceSessionResolutionDto` |
| TeachingGroup | **Not referenced** |
| Legacy fallback | `Legacy(...)` when no current/upcoming entry — unchanged |

**Implication for bridge:** Projection must keep `TimetableSections` coherent after TeachingGroupSection mutations, or Attendance Timetable mode loses combined/single section ids. Prompt 1 does **not** change Attendance.

Additional Attendance UI/types reference TimetableSections contract (additive metadata) — out of scope to modify.

---

## D. Existing authorization requirements

Must preserve for later prompts:

1. GET sections: `CanViewSchedulingTimetable`
2. PUT sections: `CanManageSchedulingTimetable`
3. Do not weaken to Operations.View / Allocation.* / faculty-only policies

---

## E. Existing tenant isolation behavior

- All TimetableSection queries in the mutation/read path filter `_currentUser.TenantId`.
- Section existence checks include tenant.
- Timetable existence checks include tenant.
- Soft-deleted rows hidden by global filters on reads using the filtered `TimetableSections` set.
- Cross-tenant section id → `InvalidOperationException("Invalid section {id}.")` (does leak the requested section id number — existing behavior).

---

## F. Existing API compatibility requirements

| Contract element | Must keep |
|---|---|
| Route | `PUT/GET /api/timetable/{timetableId}/sections` |
| Body | `{ timetableEntryId?, sectionIds: number[] }` |
| Response | Array of `TimetableSectionDto` |
| Auth policies | View for GET; Manage for PUT |
| Soft-delete replacement semantics | Empty list clears entry’s sections |
| Multi-section combined classes | Multiple SectionIds per entry |
| Attendance Timetable mode | Continues to read TimetableSections |

Callers must **not** be required to send TeachingGroup internals (until a later UI prompt).

---

## G. Assumptions violated if TimetableSection becomes a projection

| Current assumption | Impact when SoT moves to TeachingGroupSection |
|---|---|
| TimetableSection is the authoritative section list for an entry | Writes must stop originating here; projection only |
| PUT /sections can succeed without TeachingGroupId | Bridge must require explicit TG **or** reject (no silent create) — Prompt 2 will define |
| No TeachingGroupSection maintenance | Must become primary mutation |
| Attendance trusts TimetableSection alone | Projection sync is mandatory after SoT change |
| Clone/version ignore TimetableSection | Still true; TG relationships may need separate coherence rules in later prompts |
| Academic readiness/health counts TimetableSections | Remains valid **if** projection stays in sync |
| Soft-delete + re-insert identity | Projection should reuse same lifecycle (soft-delete obsolete) |

---

## H. Direct TimetableSection writes outside `/sections`

**Repository-wide search result:**

| Location | Writes TimetableSection? |
|---|---|
| `SectionManagementService.SetTimetableSectionsAsync` | **Yes — only production writer** |
| Controllers | No direct DbSet mutation |
| `TimetableService` / clone / version | **No** |
| Attendance services | **No** |
| TeachingGroup services | **No** |
| Unit tests | Construct entities in-memory only |

**Conclusion:** Bridging `/sections` through TeachingGroup + projection is sufficient to eliminate application-level TimetableSection mutation ownership — no secondary write pipelines discovered.

---

## Clone / version behavior (detail)

| Path | TimetableSection | TeachingGroupId (entry) |
|---|---|---|
| `TimetableService.CloneEntry` | Not copied | Copied (TG.4 P2/P4) |
| `TimetableCloneService` | Not written | Preserved via CloneEntry + compatibility check |
| `ScheduleVersionService` | Not written | Preserved via CloneEntry + compatibility check |

**Gap for later prompts:** Cloning entries with TG does not recreate TimetableSection projection rows. Attendance may see empty SectionIds until `/sections` (or projection) runs again. Document for Prompt 5/6/9; do not invent silent repair in Prompt 1.

---

## TeachingGroup / TeachingGroupSection current state (context)

| Entity | Role today | Timetable wiring |
|---|---|---|
| `TeachingGroup` | Scheduling cohort under SubjectAllocation | Linked via `TimetableEntry.TeachingGroupId` (TG.4 P2–P4) |
| `TeachingGroupSection` | Intended SoT TG → Section | **Not** written by `/sections` |
| `TimetableSection` | De facto SoT for entry → Section | Written by `/sections` only |

Target (post 4A):

```text
TeachingGroupSection  = SOURCE OF TRUTH
TimetableSection      = PROJECTION (Attendance / legacy GET)
```

---

## Discovery confirmation tests

Added source-inspection tests (no production behavior change):

`Abhyanvaya.Application.UnitTests/Scheduling/AiSchedTg4APrompt1LegacySectionDiscoveryTests.cs`

Confirms:

1. Controller route + auth policies  
2. `SetTimetableSectionsAsync` soft-deletes + inserts TimetableSection  
3. No TeachingGroup / TeachingGroupSection in that method  
4. No TimetableEntry.TeachingGroupId usage on the path  
5. AttendanceSessionResolver reads TimetableSections; no TeachingGroup  
6. TimetableService / Clone / Version do not write TimetableSection  
7. Sole `new TimetableSection` / Add pattern is in SectionManagementService  

---

## Deferred to Prompt 2+

- Bridge contract when entry has TimetableSections but `TeachingGroupId == null`
- TeachingGroupSection application boundary
- Projection component
- Retrofit PUT through TG boundary
- Read compatibility / disposable conversion / guards / E2E / freeze

---

## Acceptance (Prompt 1)

| Criterion | Result |
|---|---|
| Full `/sections` mutation trace documented | Yes |
| All TimetableSection readers identified | Yes |
| Sole writer identified | Yes |
| No production code modified | Yes |
| Exact file/class/method references | Yes |
| Dependency map | Yes |

**STATUS = PASS**
