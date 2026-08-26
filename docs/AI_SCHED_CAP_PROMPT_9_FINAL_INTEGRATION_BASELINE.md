# AI-SCHED-CAP Prompt 9 — Final Integration Discovery & Acceptance Baseline

**Workstream:** AI-SCHED-CAP  
**Prompt:** 9 — Final Integration Discovery & Acceptance Baseline  
**Date:** 2026-08-20  
**Type:** **ACCEPTANCE / DISCOVERY ONLY** — no production behavior changed  
**Scope:** AI-SCHED-TG.4A / TG.5 / TG.6 + AI-SCHED-CAP Prompts 1–8.3  

**Final recommendation: PASS**

---

## 1. Architecture baseline (frozen — verified)

The following remain in force and were spot-checked against the live codebase:

| # | Rule | Verification |
| --- | --- | --- |
| 1 | `TeachingGroupSection` = TG section-membership SoT | TG.4A freeze docs + projector comments |
| 2–3 | `TimetableSection` projection-only; sole writer `TimetableSectionProjector` | Only `new TimetableSection` entity construction in `TimetableSectionProjector.cs` (DTO mapping elsewhere is not a write) |
| 4 | Explicit `TimetableEntry.TeachingGroupId`; no SA→TG inference / auto-create | UI contracts `shouldInferTeachingGroupFromSubjectAllocation() === false`; no auto-create on designer load |
| 5–6 | Assign/Clear via dedicated APIs; projector + single SaveChanges in application service | `TeachingGroupApplicationService` + projector **no** `SaveChanges` |
| 7–8 | Legacy `TeachingGroupId = null` valid; `/sections` compatibility intact | TG.4A legacy bridge docs + CAP readiness legacy semantics |
| 9 | Membership resolution server-authoritative | `TeachingGroupMembershipResolver` (reads `StudentSections` only) |
| 10 | PlacementSize: Resolved (incl. 0) → ExpectedStudentCount → Subject.ExpectedCapacity → Unset | `PlacementSizeResolver` |
| 11–13 | Room vs TG capacity distinct; shared `IRoomCapacityEvaluator`; TG uses resolved count | `RoomCapacityEvaluator` + `TeachingGroupCapacityExceededRule` |
| 14 | Draft soft / detect-only | `ConflictSummary.BlocksEditing => false` |
| 15–18 | Publish Level-3 gate; readiness read-only; `PublishAsync` → `EvaluatePublishReadinessAsync` before mutation; `PublishNotReadyException` | `TimetableLifecycleService` + API `BadRequest(ex.Readiness)` |
| 19–20 | UI no decision engine; SoftWarnings ≠ PublishReadiness | Separate panels; guards assert no client engines |
| 21–23 | No Attendance schema / StudentSection mutation from Scheduling / no UI TimetableSection writes | Projector + membership services declare non-mutation; UI guards |
| 24–25 | No client TG compatibility filtering; no auto publish retry | Selector contract + publish flow guards |
| 26 | No invented browser E2E | See §6 |

---

## 2. Components verified

| Component | Status |
| --- | --- |
| `TimetableSectionProjector` | Sole entity writer; no `SaveChanges` |
| `TeachingGroupApplicationService` / membership services | Assign/clear + membership SoT boundaries |
| `ConflictEngine` | Single DI registration |
| `IPlacementSizeResolver` / `PlacementSizeResolver` | Single shared implementation |
| `IRoomCapacityEvaluator` / `RoomCapacityEvaluator` | Shared margin-aware evaluator |
| `TeachingGroupCapacityExceededRule` | Server TG capacity |
| SoftValidation (`TimetableSoftValidationService`) | Uses PlacementSize + RoomCapacity + presentation composer |
| `ISchedulingConflictPresentationComposer` | Registered; reused by soft + readiness messaging |
| `ITimetablePublishReadinessService` | Read-only orchestration via `IConflictAnalysisRunner` |
| `TimetableLifecycleService.PublishAsync` | Gate before mutation |
| `PublishNotReadyException` | Structured readiness payload |
| `PublishReadinessPanel` | Designer + PublishingPage |
| TG membership UX / timetable TG selector | TG.5 / TG.6 UI present; inference flags false |
| Architecture guards (CAP 1–8.3, TG) | Green in focused runs |

---

## 3. API endpoints verified

| Endpoint | Role |
| --- | --- |
| `GET /api/scheduling/timetables/{id}/publish-readiness` | Read-only preflight (`CanViewSchedulingTimetable`) |
| `POST /api/scheduling/timetables/{id}/publish` | Authoritative publish + readiness gate (`CanPublishScheduling`); 400 + readiness DTO on block |
| `…/entries/{id}/teaching-group` assign/clear | Explicit TG APIs |
| `…/entries/{id}/compatible-teaching-groups` | Server-side compatibility query |
| Soft-warnings GET/dismiss | Draft informational (unchanged) |

Client mirrors: `getTimetablePublishReadiness`, `publishTimetable`, `parsePublishFailure`, TG assign/clear helpers in `schedulingService.ts`.

---

## 4. Known deviations (non-blocking / documented)

| Item | Notes |
| --- | --- |
| Pre-gate lifecycle `DomainException` strings | Frozen/NotEligible/ScopeConflict still return **string** `400` when thrown **before** readiness evaluation (Prompt 7 intentional preservation). Archived may surface via readiness when gate is reached. |
| Prompt 5 historical wording | Contract doc still says Prompt 5 must not change `PublishAsync` — historically correct; Prompt 7 owns the gate. Not a code defect. |
| Prompt 8.1 discovery tests | Updated in 8.2/8.3 to reflect readiness client presence (approved progression, not weakened guards). |
| Naming: “ExpectedCapacity” vs `ExpectedStudentCount` | Same PlacementSize middle tier; code/API use `ExpectedStudentCount`. |
| `TeachingGroupsPage` `createTeachingGroup` | Explicit user action only — not inference/auto-create from SubjectAllocation. |
| Membership resolver reads `StudentSections` | Read-only resolution input — not mutation from Scheduling. |

No duplicate capacity engines, UI conflict engines, Publish gate bypasses, projector `SaveChanges`, `IgnoreQueryFilters` in Scheduling Application layer, or automatic publish retry were found.

---

## 5. Deferred items

| Item | Owner |
| --- | --- |
| Live browser E2E with real tenant data | Environment / Prompt follow-on |
| SoftWarnings finding → entry navigation parity | Optional UX polish (Publish blockers already navigate) |
| Unifying lifecycle DomainException responses into readiness DTO on publish | Optional; would change existing string error contract |
| Prompt 10 (if any) corrective / release packaging | **Not started** — this prompt stops here |

---

## 6. Browser E2E availability

**NOT EXECUTED — ENVIRONMENT/DATA UNAVAILABLE**

No credentials, live API+UI session, or acceptance dataset were used. Unit/architecture/build evidence only.

---

## 7. Recommended corrective work

**None required for PASS.**

Optional (non-blocking) follow-ups only:

1. SoftWarnings click-to-entry navigation (parity with PublishReadiness).
2. Product decision: whether Frozen/NotEligible publish failures should return readiness DTOs instead of strings (contract change — needs explicit approval).

Do **not** weaken architecture guards or alter frozen TG.4A–TG.6 / CAP 1–8.3 contracts for convenience.

---

## 8. Test evidence (focused runs)

| Suite | Result |
| --- | --- |
| `AiSchedCap` + `AiSchedTg` + `TeachingGroup` | **441 Passed** |
| Scheduling regression filter (Conflict / Phase2A–B / SoftValidation / Timetable / AiSchedCap) | **329 Passed** |
| UI Vitest (`src/pages/setup/scheduling`) | **PASS** (exit 0) |
| API build | **0 errors** |
| UI production build (`tsc -b && vite build`) | **PASS** |

Production code was **not** modified for this prompt.

---

## 9. Search results summary

| Search | Result |
| --- | --- |
| Duplicate PlacementSize / RoomCapacity / ConflictEngine | Single registrations |
| UI capacity/conflict decision engines | None in designer / publish panel |
| Direct TimetableSection entity writes outside projector | None |
| Hidden TG creation / SA inference | Forbidden by contracts; explicit create only on TG management page |
| Publish gate bypass | `PublishAsync` always evaluates readiness before mutation |
| Readiness logic duplicated in UI | Presentation + `isBlocking` filter only |
| Stale obsolete CAP guards | Updated only where Prompt 7/8 progression superseded discovery gaps |
| Projector SaveChanges | Absent |
| Auto publish retry | Absent |
| Attendance / StudentSection mutation from TG scheduling path | Declared non-writers; membership reads StudentSection only |
| `IgnoreQueryFilters` in Scheduling Application | None found |

---

## 10. Final recommendation

### **PASS**

Frozen TG.4A–TG.6 and CAP 1–8.3 architecture is intact. Focused regressions and builds are green. No production defects requiring change under this prompt.

**STOP** — do not automatically continue to Prompt 10.
