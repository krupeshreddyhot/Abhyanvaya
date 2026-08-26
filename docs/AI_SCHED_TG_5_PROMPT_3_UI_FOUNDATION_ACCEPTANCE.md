# AI-SCHED-TG.5 Prompt 3 — Teaching Group Management UI Foundation Acceptance

**Workstream:** AI-SCHED-TG.5  
**Prompt:** 3 — UI Foundation  
**Date:** 2026-08-19  

**STATUS: CONDITIONAL PASS**

**Predecessors preserved:** TG.4A FROZEN · TG.5 Prompt 1 · TG.5 Prompt 2  

---

## 1. Scope implemented

- Discovery gate: `docs/AI_SCHED_TG_5_PROMPT_3_UI_DISCOVERY.md`
- Route: `/setup/scheduling/teaching-groups`
- Catalog card: Scheduling → Faculty Planning → **Teaching Groups**
- API client: `teachingGroupService.ts` (Prompt 2 contract only)
- Page: Subject Allocation context → list → create → detail/edit → sections → membership read → archive confirm
- RBAC: `Scheduling.TeachingGroup.View` / `Manage`
- Vitest + C# architecture guards
- **No** timetable redesign, Attendance/StudentSection changes, membership mutation, migrations, or auto-create

---

## 2. Existing UI patterns reused

| Pattern | Source |
|---|---|
| Catalog → Scheduling hub card | `schedulingCatalogConfig.tsx` |
| Classic scheduling page (back link, filters, MUI Table, Dialog) | Subject Allocation style |
| Confirm archive | `AcademicConfirmDialog` |
| Inline Alert success/error | Subject Allocation |
| `ProtectedRoute` + `hasPermission` | AppRoutes / AuthContext |
| axios service module | existing `src/services` pattern |

---

## 3. API contracts consumed

All via `/scheduling/teaching-groups` Prompt 2 surface:

list · get · create · update · archive · memberships GET · sections GET/PUT/POST/DELETE  

No invented endpoints. No `setTimetableSections` / legacy timetable section mutation from this page.

---

## 4. RBAC behavior

| Permission | UI |
|---|---|
| View | Route, list, detail, memberships, sections read |
| Manage | Create, edit, archive, add/remove/clear sections |
| Missing View | Page-level warning (route also gated) |

UI checks are convenience only; API policies remain authoritative.

---

## 5. TeachingGroupSection source-of-truth compliance

Section UI calls `getTeachingGroupSections` / `addTeachingGroupSection` / `removeTeachingGroupSection` / `replaceTeachingGroupSections` only.  
User-facing confirmation: “Sections updated successfully.”  
No TimetableSection mutation from the UI.

---

## 6. Membership behavior

Membership mutation was intentionally not implemented because the mutation contract has not yet been approved.

UI shows read-only membership rows and: **“Membership management is not yet available.”**

---

## 7. Timetable behavior

No timetable redesign was implemented.

Timetable designer / entry dialog left unchanged (TG assign UI deferred).

---

## 8. Attendance behavior

No Attendance behavior or schema was changed.

---

## 9. Test results

### Vitest (Teaching Group UI)

| Suite | Passed | Failed | Skipped |
|---|---|---|---|
| `teachingGroupUi.test.ts` + `AiSchedTg5Prompt3TeachingGroupUiGuard.test.ts` | **10** | **0** | **0** |

### .NET (Prompt 2 + Prompt 3 + TG.4A guards)

| Suite filter | Passed | Failed | Skipped |
|---|---|---|---|
| `AiSchedTg5Prompt2` / `AiSchedTg5Prompt3` / `AiSchedTg4APrompt8` / `AiSchedTg4APrompt10` | **42** | **0** | **0** |

---

## 10. Architecture Guard results

- UI Vitest guards: **PASS** (6 contract/path assertions)
- C# `AiSchedTg5Prompt3UiArchitectureGuardTests`: **PASS**
- TG.4A Prompt 8/10 + Prompt 2 guards: **PASS** (included in 42)
- Prompt 2 “no UI” gate superseded: TG UI confined to approved Prompt 3 paths; TimetableDesigner remains free of TeachingGroup references

---

## 11. API/UI build results

| Build | Result |
|---|---|
| `Abhyanvaya.API` | **Succeeded** |
| `abhyanvaya-ui` (`npm run build` / tsc + vite) | **Succeeded** |

---

## 12. Known limitations

1. Membership mutation deferred (by design).
2. No Draft→Active activate control (not in Prompt 2 Update contract).
3. Timetable designer does not yet show/assign Teaching Groups.
4. Subject Allocation labels use catalog subject names when available; otherwise id-based labels.
5. Vitest covers helpers + static architecture guards (no RTL page mounts — matches repo practice).

---

## 13. Recommendation for Prompt 4

Suggested next prompt options (pick one):

1. **Membership mutation contract + UI** (after Explicit/Hybrid write rules are approved), or  
2. **Minimal TimetableEntry Teaching Group assign/clear** in designer using existing assign API, or  
3. **Activate / lifecycle UX** if a dedicated activate endpoint is added.

Do not invent membership writes or timetable redesign without an approved contract.

---

## Final gate

**CONDITIONAL PASS**

Reasons (not FULL PASS):

1. Membership mutation intentionally incomplete (documented).
2. Timetable integration deferred (documented).
3. No RTL interactive page suite (repo convention; static guards cover contracts).

All mandatory architectural constraints for UI Foundation are met.
