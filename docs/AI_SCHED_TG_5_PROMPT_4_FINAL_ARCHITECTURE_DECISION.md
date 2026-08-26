# AI-SCHED-TG.5 Prompt 4 — Final Architecture Decision

**Workstream:** AI-SCHED-TG.5  
**Prompt:** 4 — Teaching Group Membership Semantics & Mutation Contract  
**Date:** 2026-08-19  
**Type:** Architecture / contract only — **no mutation implementation, no schema change, no UI editor**

---

## Decision summary

| Topic | Decision |
|---|---|
| Hybrid model | **Model B:** `(Base ∪ ExplicitIncludes) − ExplicitExcludes` |
| Hybrid base | Sections if linked; else StudentSubject + academic scope |
| Entity sufficiency | Existing `TeachingGroupMembership` + `Inclusion` **sufficient** — no migration in this prompt |
| Uniqueness | Per-TG current row; ExclusionGroupKey mutual exclusion; **no** global SA uniqueness |
| ResolvedStudentCount | Derived via Membership Resolver; never persisted |
| Mutation ops | Add / Remove / Replace (+ GetResolved / GetOverlays) |
| API shape | Extend `.../memberships` + add `.../resolved-members` |
| Dynamic sources | No materialised membership CRUD in v1 |
| Attendance / StudentSection / Timetable | Hard isolation preserved |
| Schema / EF / UI mutation | **Unchanged** in Prompt 4 |

---

## Gate checklist

| Gate | Result |
|---|---|
| Architecture Discovery | **PASS** |
| Membership Semantics | **DEFINED** |
| Explicit Membership | **DEFINED** |
| Section-derived Membership | **DEFINED** |
| Hybrid Membership | **DEFINED** (Model B) |
| Capacity Rules | **DEFINED** (TG.2A preserved) |
| Eligibility Rules | **DEFINED** |
| Mutation Contract | **DEFINED** |
| API Contract | **DEFINED** |
| Authorization | **DEFINED** (View/Manage) |
| Concurrency | **DEFINED** (unique-index / 409; no new token in v1) |
| Audit | **DEFINED** (`IAuditService` + BaseEntity) |
| Attendance Boundary | **PASS** |
| Timetable Boundary | **PASS** |
| Architecture Guard Specification | **PASS** (documented + doc-existence tests) |

---

## Known implementation gaps (not semantic blockers)

1. **Resolver not implemented** — Prompt 2 `ResolvedStudentCount` counts Include rows only.  
2. **Mutation APIs not implemented** — by design until a later prompt.  
3. **Lock/Publish roster snapshot** — deferred (TG.2 default remains Yes).  
4. **AcademicYear eligibility binding** — follow existing student/enrollment conventions at implementation time.

These are **implementation follow-ups**, not unresolved Hybrid semantics.

---

## Architecture guards to enforce in implementation prompts

Forbidden:

- UI → EF  
- UI / Membership service → StudentSection mutation  
- UI / Membership service → TimetableSection / TimetableEntry mutation  
- Membership service → Attendance mutation  
- GET → membership mutation or TeachingGroup auto-create  
- SubjectAllocation → automatic TeachingGroup creation  
- Membership writes outside `ITeachingGroupMembershipApplicationService`

---

## Recommendation for next prompt

**AI-SCHED-TG.5 Prompt 5 — Membership Resolver & Mutation Implementation** (application + API only; UI editor after green API).

Do **not** start React membership editing until Prompt 5 APIs and guards pass.

---

## Final assessment

# CONDITIONAL PASS — READY FOR IMPLEMENTATION REVIEW

**Why not FULL PASS:** Mutation and resolver are intentionally **not implemented** in this prompt (per Chief Architect restriction). Semantics and contracts are defined and Hybrid is no longer UNDEFINED.

**Why not BLOCKED:** No schema gap for Hybrid Model B; entity/enums already support Include/Exclude; TG.4A boundaries preserved.

**Architect action:** Approve this ADR package, then authorize Prompt 5 implementation.
