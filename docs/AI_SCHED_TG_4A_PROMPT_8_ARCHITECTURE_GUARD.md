# AI-SCHED-TG.4A Prompt 8 — Architecture Guard & Source-of-Truth Enforcement

**Workstream:** AI-SCHED-TG.4A  
**Prompt:** 8 — Architecture Guard  
**Date:** 2026-08-18  
**Predecessor:** AI-SCHED-TG.4A Prompt 7 (PASS — disposable explicit conversion)

**STATUS: PASS**

---

## 1. Enforced architecture

```text
Application command
    ↓
TeachingGroup / TeachingGroupSection application boundary
    ↓
TeachingGroupSection   ← source of truth
    ↓
TimetableSectionProjector
    ↓
TimetableSection       ← projection only
```

| Role | Owner |
|---|---|
| SoT | `TeachingGroupSection` via `ITeachingGroupSectionApplicationService` |
| Projection writer | `TimetableSectionProjector` **only** |
| Legacy PUT bridge | `SectionManagementService.SetTimetableSectionsAsync` → `ReplaceSectionsAndProjectAsync` |
| Explicit TG assign | `ITeachingGroupApplicationService` |
| Disposable conversion | `ILegacyTimetableTeachingGroupConversionService` (scoped, never hosted) |

---

## 2. FORBIDDEN patterns (guards)

| Forbidden | Guard |
|---|---|
| `new TimetableSection` outside projector | App-layer scan (entity only; DTO excluded) |
| Controller → TimetableSection DbSet Add/Remove | API Controllers scan |
| `TimetableService` / clone / version → TimetableSection | Source asserts |
| Auto TG create on GET / Attendance | GET + resolver asserts |
| SubjectAllocation → TG inference | TG boundary files forbid SA DbSet / FindFirst / CreateIfMissing |
| `.IgnoreQueryFilters` on normal TG ops | TG/projector/conversion services |
| Attendance / StudentSection mutation from Scheduling | Scheduling folder scan |
| UI invents TG / SA inference on sections client | `sectionService.ts` |
| Conversion as hosted/startup job | DI + `Program.cs` |

---

## 3. Test suite

**Primary:** `AiSchedTg4APrompt8ArchitectureGuardTests`

**Also retained / complementary:**

- `TeachingGroupSectionArchitectureGuardTests`
- `TeachingGroupApplicationArchitectureGuardTests`
- `LegacyTimetableTeachingGroupConversionArchitectureGuardTests`
- Prompt 1/2/5/6 bridge & read guards

---

## 4. Production code

Prompt 8 is **guard + documentation only**. No product behavior change.

---

**STATUS: PASS**
