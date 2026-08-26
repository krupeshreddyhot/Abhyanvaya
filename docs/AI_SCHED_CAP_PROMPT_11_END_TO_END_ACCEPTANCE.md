# AI-SCHED-CAP Prompt 11 — End-to-End Scheduling Acceptance

**Workstream:** AI-SCHED-CAP  
**Prompt:** 11 — End-to-End Scheduling Acceptance  
**Date:** 2026-08-20  
**Type:** **ACCEPTANCE / VERIFICATION ONLY** — no architecture redesign  
**Baseline:** CAP Prompts 1–10 + TG.4A / TG.5 / TG.6 (frozen)  
**Final recommendation: PASS**

---

## 1. Objective

Prove the complete scheduling workflow across API, application services, UI, Teaching Groups, capacity validation, conflict detection, and publishing — without redesigning architecture.

---

## 2. Browser E2E

**BROWSER E2E = NOT EXECUTED — ENVIRONMENT/DATA UNAVAILABLE**

No authenticated live API+UI session or acceptance dataset was available. Evidence is unit / architecture / contract / build only. Results below are **not invented** browser outcomes.

---

## 3. Acceptance matrix

| Scenario | Expected | Actual | Result | Evidence / Test |
| --- | --- | --- | --- | --- |
| **Scenario 1 — Create Draft Timetable Entry** | Entry created; no TG inferred/auto-created; `TeachingGroupId` null when not assigned; scheduling create preserved | Create/Update/Upsert DTOs omit `TeachingGroupId`; UI `shouldInferTeachingGroupFromSubjectAllocation() === false`; dialog creates without TG | **PASS** | `TimetableEntryTeachingGroupMutationInvariantTests.Create_with_null_TeachingGroupId_succeeds`; `TeachingGroupApplicationArchitectureGuardTests.Update_and_create_entry_DTOs_omit_TeachingGroupId…`; `AiSchedTg6Prompt4SelectorContract.test.ts`; `AiSchedCapPrompt11…Scenario1_*` |
| **Scenario 2 — Assign Teaching Group** | Compatible TG assigned; `TeachingGroupSection` SoT unchanged; `TimetableSection` projected; designer shows assignment; no UI TimetableSection mutation | Assign stages `TeachingGroupId` → projector sync → one SaveChanges; sole writer projector; UI uses dedicated assign APIs | **PASS** | `AiSchedCapPrompt10TransactionalIntegrityTests.Assign_*`; `AiSchedTg6FinalGatePrompt21AssignmentProjectionTests.Assign_projects_*`; Cap 10/11 assign guards; TG.6 UI assignment actions |
| **Scenario 3 — Clear Teaching Group** | `TeachingGroupId` null; entry projection cleared/soft-deleted; SoT / other entries untouched | Clear projection then null id; one SaveChanges; SoT count unchanged | **PASS** | `AiSchedCapPrompt10…Clear_*`; `AiSchedTg6FinalGatePrompt21…Clear_soft_deletes_*`; Cap 10/11 clear order guards |
| **Scenario 4 — Membership Resolution** | Explicit / Hybrid / Section / Combined / StudentSubject server-authoritative; UI does not calculate resolved count | `TeachingGroupMembershipResolver` + membership services; UI presents API roster only (`shouldCalculateResolvedMembershipInUi() === false`) | **PASS** | `AiSchedTg5Prompt5MembershipResolverMutationTests`; `AiSchedTg5Prompt5AMembershipIntegrityTests.Combined_sections_*`; `AiSchedTg6Prompt3MembershipUxGuard.test.ts`; Cap 11 Scenario4 |
| **Scenario 5 — Teaching Group Capacity** | Draft mutation allowed; soft warning; `TEACHING_GROUP_CAPACITY_EXCEEDED` blocking on readiness; UI does not calculate blocker | Conflict rule + readiness `IsBlockingConflict`; `BlocksEditing => false`; UI presents server `isBlocking` | **PASS** | Cap 3/4 capacity + presentation matrices; Cap 6 `Evaluate_TEACHING_GROUP_CAPACITY_EXCEEDED_blocks`; Cap 7/10 publish reject zero mutation; Cap 8.2/8.3 UI no capacity engine; Cap 11 Scenario5 |
| **Scenario 6 — Room Capacity** | Shared PlacementSize + RoomCapacityEvaluator; soft warning; readiness blocks; UI presents only | Single DI registrations; soft/conflict/readiness agree on `ROOM_CAPACITY` | **PASS** | Cap 3A room alignment; Cap 3/4/6/7 room capacity tests; Cap 11 Scenario6 |
| **Scenario 7 — Timetable Conflict** | ConflictEngine detects; draft edit allowed; readiness uses locked severity/blocking rules | Single ConflictEngine; Critical blocks publish; Warning does not; non-capacity Error does not; draft `BlocksEditing => false` | **PASS** | `Phase2BConflictEngineTests`; Cap 5 contract; Cap 6 readiness severity matrix; Cap 11 Scenario7 |
| **Scenario 8 — Publish Readiness GET** | Deterministic ordering; `isBlocking`; zero mutation | Read-only service; ordered findings; GET maps to evaluate only | **PASS** | Cap 6 ordering/repeatability/no-persistence; Cap 10 `Readiness_service_and_GET_never_mutate`; Cap 11 Scenario8 |
| **Scenario 9 — Publish Blocked** | `PublishNotReadyException`; structured readiness; lifecycle unchanged; no partial mutation; no auto-retry | Gate before status mutation; API `400` + readiness DTO; SaveChanges never | **PASS** | Cap 7 publish gate suite; Cap 10 `Publish_blocked_*`; Cap 8.3 no auto retry; Cap 11 Scenario9 |
| **Scenario 10 — Resolve Blocker and Publish** | Publish succeeds; lifecycle Published; same readiness evaluation; one mutation transaction | Ready path: evaluate → status → one SaveChanges | **PASS** | Cap 7 `Clean_ready_timetable_publishes_and_saves`; Cap 10 `Publish_ready_*`; Cap 11 Scenario10 |
| **Scenario 11 — Archived Teaching Group** | Compatible GET does not clear assigned Archived TG; UI labels Archived; no auto-replace | Assigned Archived re-included; label in grid/dialog | **PASS** | `CompatibleTeachingGroupQueryServiceTests.Archived_TG_excluded_unless_currently_assigned`; Cap 4 archived presentation; TG.6 grid Archived label; Cap 11 Scenario11 |
| **Scenario 12 — Legacy Entry** | `TeachingGroupId` null valid; no inference; PlacementSize fallback; publish per contract | Null TG uses subject/expected capacity path; no TG capacity soft finding from null TG | **PASS** | Cap 3 `Legacy_null_TeachingGroupId_*`; Cap 4 null-TG soft warning absence; inference guards; Cap 11 Scenario12 |
| **Scenario 13 — Concurrency** | Stale updates rejected safely; mapped conflict response; UI presents conflict; no auto-retry | Membership unique → conflict; scheduling EF concurrency → `ForSchedulingModule`; UI 409 handling | **PASS*** | Cap 10 concurrency classifier + membership mapper; TG.6 409 UX; Cap 11 Scenario13. *Residual: Timetable/Entry lack RowVersion (documented Prompt 10) — when no DB concurrency exception is raised, last-write-wins remains platform residual |
| **Scenario 14 — Finding Navigation** | Designer: blocker → entry dialog; PublishingPage: `?entryId=` → Designer → dialog; navigation does not mutate | Shared `PublishReadinessPanel.onViewEntry`; Designer opens dialog; Publishing navigates with query | **PASS** | Cap 8.3 UX guards + panel tests; Cap 11 Scenario14 |
| **Scenario 15 — DnD / Copy / Paste** | Create/Update/Upsert payloads omit `TeachingGroupId`; TG assign remains dedicated | DTO omit + designer create/bulk omit TG | **PASS** | `AiSchedTg6Prompt4Prompt4GridIntegration.test.ts` drag/drop/paste omit; Cap/TG DTO omit guards; Cap 11 Scenario15 |

\*Scenario 13 **PASS** with documented concurrency-token residual (no schema change in CAP).

---

## 4. Architecture checks

| Check | Result | Evidence |
| --- | --- | --- |
| No `TimetableSection` writer outside projector | **PASS** | Cap 7/10/11 sole-writer guards |
| No `SaveChanges` in projector | **PASS** | Projector source + Cap 10/11 |
| No TG auto-create / no SA inference | **PASS** | Selector contract `false`; management create is explicit user action only |
| No client compatibility filtering | **PASS** | Compatible TG from API; Cap 4 / TG.6 contracts |
| No UI capacity calculation | **PASS** | Cap 8.2/8.3 / 11 panels |
| No UI publish gate | **PASS** | Server `PublishAsync` gate; UI presents readiness, does not decide |
| Readiness GET no mutation | **PASS** | Cap 6/10/11 |
| No Attendance / StudentSection mutation from Scheduling | **PASS** | Lifecycle/readiness/projector guards; membership resolver reads StudentSection only |
| No `IgnoreQueryFilters` in Scheduling Application | **PASS** | Cap 11 architecture scan (empty) |

---

## 5. Production behavior changed

**None.** Prompt 11 is acceptance-only. No API/UI production code modified.

---

## 6. Tests added

| Suite | Role |
| --- | --- |
| `AiSchedCapPrompt11EndToEndAcceptanceGuardTests` | Locks Scenarios 1–15 + architecture checks + documentation contract |

Existing CAP 1–10 and TG suites remain authoritative behavioral evidence (not weakened).

---

## 7. Verification evidence

| Suite | Result |
| --- | --- |
| Prompt 11 guards | **18 Passed** |
| CAP + TG + TeachingGroup | **481 Passed** |
| Scheduling filter | **369 Passed** |
| API build | **0 errors** |
| UI build | **PASS** (`tsc -b && vite build`) |
| Migration | **None** |
| Browser E2E | **NOT EXECUTED — ENVIRONMENT/DATA UNAVAILABLE** |

---

## 8. Known residuals (non-blocking)

1. Browser E2E not executed (environment unavailable).
2. Timetable/Entry optimistic concurrency tokens not present (Prompt 10 residual); mapping verified when exceptions occur.
3. SoftWarnings click-to-entry parity optional (Prompt 9 deferred); PublishReadiness navigation **is** covered.
4. Pre-gate Frozen/NotEligible publish failures remain string `400` DomainExceptions (Prompt 7 intentional).

---

## 9. Final recommendation

**PASS**

Capability end-to-end acceptance is satisfied on unit/architecture/contract/build evidence. Browser E2E remains unavailable and is explicitly marked as not executed.

**STOP** after Prompt 11.
