# AI-SCHED-TG.5 Prompt 3 — Teaching Group Management UI Discovery

**Workstream:** AI-SCHED-TG.5  
**Prompt:** 3 — UI Foundation (Discovery gate)  
**Date:** 2026-08-19  
**Type:** DISCOVERY (pre-implementation)

**STATUS: PASS — Gate cleared for UI implementation**

**Predecessors:** TG.4A FROZEN · TG.5 Prompt 1 · TG.5 Prompt 2 (CONDITIONAL PASS — membership mutation deferred)

---

## 1. Existing React architecture (confirmed)

| Area | Finding |
|---|---|
| Stack | React 19 + Vite + MUI + react-router-dom 7 + axios + react-hook-form + Vitest |
| Router | `src/routes/AppRoutes.tsx` under `MainLayout`; paths kebab-case `/setup/scheduling/...` |
| Catalog nav | Catalog (`/setup`) → Scheduling hub (`/setup/scheduling`) via `schedulingCatalogConfig.tsx` |
| Sidebar | Catalog only — no parallel scheduling sidebar |
| CRUD template | **SubjectAllocationPage** (classic MUI Table + Dialog + RHF) |
| Modern shell | TimetableHub uses `AcademicOperationalPageShell` — optional; TG foundation copies classic Subject Allocation style for consistency with Faculty Planning |
| Tables | MUI `Table` (no DataGrid) |
| Dialogs | MUI `Dialog`; newer confirms use `AcademicConfirmDialog` |
| Alerts | Inline MUI `Alert` success/error (no global toast) |
| Auth | `PermissionKeys` + `useAuth().hasPermission` + `ProtectedRoute anyPermission` |
| API | `src/api/axios.ts` + DTOs/functions in `src/services/schedulingService.ts` |
| Tests | Vitest unit tests (`*.test.ts`); no RTL for scheduling pages |

---

## 2. Preferred UI home

**Catalog → Scheduling → Teaching Groups**

- Route: `/setup/scheduling/teaching-groups`
- Catalog group: **Faculty Planning** (adjacent to Subject Allocation)
- Pattern: reuse Subject Allocation page structure + AcademicConfirmDialog for archive

---

## 3. Prompt 2 API contracts (consume as-is)

Base: `/scheduling/teaching-groups` (axios baseURL already includes `/api`)

| Capability | Method | Path | Available |
|---|---|---|---|
| List by SA | GET | `?subjectAllocationId=` | Yes |
| Get | GET | `{id}` | Yes |
| Create | POST | `/` | Yes |
| Update | PUT | `{id}` | Yes |
| Archive | POST | `{id}/archive` | Yes |
| Memberships | GET | `{id}/memberships` | Yes (read-only) |
| Sections list | GET | `{id}/sections` | Yes |
| Replace sections | PUT | `{id}/sections` | Yes |
| Add section | POST | `{id}/sections/{sectionId}` | Yes |
| Remove section | DELETE | `{id}/sections/{sectionId}` | Yes |

**Do not invent endpoints.** Membership mutation **not** in contract — UI must remain read-only.

**RBAC keys (backend):** `Scheduling.TeachingGroup.View` / `Scheduling.TeachingGroup.Manage` — must be added to UI `permissionKeys.ts`.

---

## 4. Absolute UI constraints

- No auto-create on page load / list / get
- No SubjectAllocation → single TeachingGroup inference
- Sections via TeachingGroupSection API only — never TimetableSection mutation
- ResolvedStudentCount display-only
- No timetable redesign; no Attendance/StudentSection changes
- Membership: display + “not yet available” notice

---

## 5. Gaps (report, do not invent)

| Gap | Action |
|---|---|
| Membership mutation API | Read-only UI + explicit message |
| Activate Draft→Active endpoint | Not in Prompt 2 Update/Create (status starts Draft). UI does not invent activate; archive only for lifecycle mutation |
| Timetable designer TG assign UI | Deferred (Prompt says optional minimal indicator only if trivial — **defer**) |

---

## 6. Gate decision

**Discovery confirms** existing UI architecture and Prompt 2 contracts are sufficient to implement Teaching Group Management UI Foundation.

**Proceed to implementation.**
