# AI-SCHED-TG.4A — Final Acceptance (Prompt 9)

**Workstream:** AI-SCHED-TG.4A  
**Prompt:** 9 — End-to-End Regression & Acceptance  
**Date:** 2026-08-18  
**Predecessor:** AI-SCHED-TG.4A Prompt 8 (PASS — architecture guards)

**STATUS: PASS**

---

## 1. Scenario matrix

| # | SCENARIO | Expected | Evidence | Result |
|---|---|---|---|---|
| 1 | Single section bridge | SoT + projection = 1 | `AiSchedTg4APrompt9EndToEndAcceptanceTests.S1_*` | **PASS** |
| 2 | Combined sections | No duplicate SoT/projection | `S2_*` | **PASS** |
| 3 | Zero sections | Clears SoT + projection | `S3_*` | **PASS** |
| 4 | Idempotent replace | No duplicates on re-PUT | `S4_*` | **PASS** |
| 5 | Shared TG → all entries | All bound entries projected | `S5_*` | **PASS** |
| 6 | Wrong tenant | Not found; no mutation | `S6_*` | **PASS** |
| 7 | Incompatible scope | DomainException; no mutation | `S7_*` | **PASS** |
| 8 | Missing TeachingGroup | Not found; no auto-create | `S8_*` | **PASS** |
| 9 | Null TeachingGroupId | Reject; no inference/create | `S9_*` | **PASS** |
| 10 | Attendance | Timetable read + Legacy fallback | `S10_*` + resolver suite | **PASS** |
| 11 | Clone | `TeachingGroupId` preserved; no direct TimetableSection write | `S11_*` | **PASS** |
| 12 | Schedule version | TG via `CloneEntry` + compatibility | `S12_*` | **PASS** |
| 13 | Faculty RBAC | Policies unchanged | `S13_*` | **PASS** |
| 14 | Admin/operator | Sections + TG assign authorized | `S14_*` | **PASS** |

### Clone / projection note (SCENARIO 11)

`TimetableService.CloneEntry` copies `TeachingGroupId`. Clone/version services do **not** copy `TimetableSection` rows (established since Prompt 1). SoT remains on the shared TeachingGroup; projection for cloned entries is refreshed via the approved `ReplaceSectionsAndProjectAsync` path when sections are set again. Accepted coherence for TG.4A (not silent backfill).

---

## 2. Required gates

| Gate | Scope | Result |
|---|---|---|
| Prompt 9 E2E scenarios | `AiSchedTg4APrompt9*` | **PASS** (15/15 after doc marker) |
| Architecture + TG.4A regression | Prompt 1–8 + projector + bridge + conversion + AttendanceSessionResolver + mutation invariants | **PASS** (180 related tests in combined run; 1 doc-marker flake fixed) |
| API build | `dotnet build Abhyanvaya.API` | **PASS** (0 errors) |
| UI TypeScript | `abhyanvaya-ui` `tsc --noEmit` | **PASS** (exit 0) |
| Live production browser E2E | — | **DATA UNAVAILABLE** (not required; no live tenant harness in this gate) |

---

## 3. Explicit non-claims

- No permanent timetable backfill migration.
- No UI redesign.
- No Attendance schema change.
- No automatic TeachingGroup creation / SA→TG inference.

---

## 4. Verdict

**STATUS = PASS**

All executable Prompt 9 scenarios and build gates passed. Live production browser E2E is reported as **DATA UNAVAILABLE** rather than invented PASS.

Prompt 10 (architectural freeze) may proceed.
