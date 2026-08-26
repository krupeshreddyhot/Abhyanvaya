# AI-SCHED-TG.4A Prompt 6 — Legacy Read Compatibility

**Workstream:** AI-SCHED-TG.4A  
**Prompt:** 6 — Legacy read compatibility  
**Date:** 2026-08-18  
**Predecessor:** AI-SCHED-TG.4A Prompt 5 (PASS — PUT `/sections` bridge)

**STATUS: PASS**

---

## 1. Read architecture

| Layer | Role |
|---|---|
| Canonical SoT | `TeachingGroupSection` |
| Projection | `TimetableSection` |
| Mutation ownership | TeachingGroupSection application boundary + projector (Prompt 3–5) |

Existing timetable / Attendance read APIs **continue reading `TimetableSection`** for backward compatibility and performance. Mutation ownership remains with TeachingGroupSection.

---

## 2. Read paths covered

| Reader | Behavior |
|---|---|
| `GET /api/timetable/{id}/sections` | `GetTimetableSectionsAsync` — `AsNoTracking` projection |
| `GetCombinedSessionsAsync` | Projection only; multi-section filter |
| `AttendanceSessionResolver` | Timetable mode joins `TimetableSections`; Legacy fallback unchanged |
| `SectionReadinessService` / `SectionHealthService` | Continue counting / probing TimetableSection |

---

## 3. Explicit non-behaviors on GET / resolve

| Forbidden | Status |
|---|---|
| Auto-create TeachingGroup | Not present |
| Modify TeachingGroupSection on read | Not present |
| Modify TimetableSection on GET | Not present |
| SubjectAllocation → TG inference | Not present |
| Silent repair of projection drift | Not present — return projection as-is |

---

## 4. Validation scenarios (tests)

| # | Scenario | Expected |
|---|---|---|
| 1 | TG + one section | GET returns that projection row |
| 2 | TG + multiple sections | GET returns all projection rows |
| 3 | TG + zero sections | GET returns empty |
| 4 | Legacy entry without TG | GET returns existing TimetableSection rows; no TG created |
| 5 | Inconsistent SoT vs projection | GET returns projection as-is; no DB repair |

Also: Attendance Timetable mode reads projection; Legacy fallback when no staff; readiness/health still use TimetableSection.

---

## 5. Production deltas

- Documenting comments on GET / Attendance resolve paths only (behavior unchanged).
- Superseded Prompt 1/2 architecture guards updated to post–Prompt 5 writer ownership (`TimetableSectionProjector`).

No API contract change. No UI. No Attendance schema/resolver redesign.

---

## 6. Tests

`LegacyTimetableSectionsReadCompatibilityTests` + updated Prompt 1/2 guards.

**STATUS = PASS**
