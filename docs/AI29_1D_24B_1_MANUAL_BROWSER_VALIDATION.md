# AI29.1D.24B.1 Prompt 4 — Manual Browser UX Validation

**Date/time:** 2026-08-10 14:10 IST  
**Phase:** AI29.1D.24B.1 (UX hardening — validation only)  
**Executor:** Cursor agent (Senior Frontend Engineer under Chief Architect)

## Environment

| Item | Value |
|------|--------|
| Application | Abhyanvaya (Allocation Workspace / Operations) |
| Expected UI | Vite/React (`abhyanvaya-ui`, typically `http://localhost:5173`) |
| Expected API | ASP.NET Core (`http://localhost:5210` / `https://localhost:7063`) |
| Browser | N/A — not launched |
| User roles | Intended: administrator with allocation permissions; also `Allocation.Operations.View` |
| Screenshots | None |

## Execution status (authoritative)

**Manual browser validation unavailable in execution environment.**

Evidence at validation time:

- No browser automation / interactive login tools were available to this agent.
- Local listeners for ports `5173`, `4173`, `5210`, `7063`, and `27632` were **down**.
- Probes to common UI/API URLs timed out or returned unreachable.

Therefore:

- **No live browser login was performed.**
- **No test case below may be marked PASS.**
- Automated unit/UI tests from Prompts 2–3 are **not** used as a substitute for this manual checklist.

**Overall result: NOT EXECUTED (BLOCKED)**

---

## Prompt-spec alignment note (Tests 9 & 10)

Prompt 4 checklist text still lists:

| Prompt 4 wording | Superseded by Prompt 3 (authoritative UX) |
|------------------|-------------------------------------------|
| Primary action: **Review & Rebuild Allocation** | **Review Academic Scope** |
| Action: **Regenerate Allocation** | **Replay Allocation** |

When a human re-runs this checklist against the current build, judge Tests 9–10 against **Prompt 3** semantics (`docs/AI29_1D_24B_1_REBUILD_REPLAY_SEMANTICS.md`), not the obsolete Prompt 4 label strings.  
Do **not** “fix” the UI by restoring “Regenerate” / “Review & Rebuild” labels — that would regress Prompt 3.

---

## Test matrix

Legend: **NE** = Not Executed (environment blocked)

### TEST 1 — Normal allocation workflow

| Field | Value |
|-------|--------|
| Path | Setup → Sections → Allocation Workspace |
| Expected | Academic context understandable; no AI29 / AI29.1C / AI29.1C.5A / Allocation Engine / raw JSON / GUID / checksum / StudentSection in normal workflow |
| Actual | Not observed — app not reachable; no browser session |
| Result | **NE** |

### TEST 2 — Allocation Rules

| Field | Value |
|-------|--------|
| Path | Allocation Rules step |
| Expected | Primary / Additional Allocation Rules; Required (not Mandatory in display); admin-friendly explanations; no engine payload JSON / groupingMode / constraintPriorities labels in normal view |
| Actual | Not observed |
| Result | **NE** |

### TEST 3 — Preview

| Field | Value |
|-------|--------|
| Action | Run Preview |
| Expected | Student / Proposed Section / Rule Applied / Rule Status / Allocation Score understandable; no StudentSection; no scenario GUID |
| Actual | Not observed |
| Result | **NE** |

### TEST 4 — Test Allocation

| Field | Value |
|-------|--------|
| Action | Test Allocation / Simulation |
| Expected | Administrator-friendly success (concept: “Test allocation completed” + whether student records changed); no GUID / engine / raw API / StudentSection |
| Actual | Not observed |
| Result | **NE** |

### TEST 5 — Allocation creation

| Field | Value |
|-------|--------|
| Action | Generate Allocation |
| Expected | Concept: “Allocation created successfully.”; no internal scenario id in normal message |
| Actual | Not observed |
| Result | **NE** |

### TEST 6 — Review

| Field | Value |
|-------|--------|
| Path | Review Allocation |
| Expected | Business terminology; Review Allocation / Approve Allocation visible; governance jargon hidden; approval state understandable |
| Actual | Not observed |
| Result | **NE** |

### TEST 7 — Technical Details (Operations.View)

| Field | Value |
|-------|--------|
| Role | User with `Allocation.Operations.View` |
| Expected | Technical Details present, collapsed by default; scenario reference / context version / checksum / governance diagnostics inspectable; collapse restores clean normal workflow |
| Actual | Not observed |
| Result | **NE** |

### TEST 8 — Non-technical administrator

| Field | Value |
|-------|--------|
| Role | Administrator **without** `Allocation.Operations.View` |
| Expected | Technical Details not visible; no raw technical payload elsewhere |
| Actual | Not observed |
| Result | **NE** |

### TEST 9 — Stale context

| Field | Value |
|-------|--------|
| Setup | Controlled academic change that makes allocation context stale |
| Expected (Prompt 4 text) | Heading “Allocation needs to be rebuilt.”; no “Flag: stale context” / GUID / checksum / context version in normal path; primary action **Review & Rebuild Allocation**; click navigates Academic Scope; does not claim rebuilt / approve / write allocations |
| Expected (Prompt 3 authority) | Same heading/behavior; primary action **Review Academic Scope** |
| Actual | Not observed |
| Result | **NE** |

### TEST 10 — Replay / regenerate action

| Field | Value |
|-------|--------|
| Action | Replay existing allocation scenario |
| Expected (Prompt 4 text) | Label **Regenerate Allocation**; existing server contract; no second engine; no technical wording |
| Expected (Prompt 3 authority) | Label **Replay Allocation**; client `replayAllocationScenario` → `POST /allocation/scenarios/{id}/replay` |
| Actual | Not observed |
| Result | **NE** |

### TEST 11 — Approval

| Field | Value |
|-------|--------|
| Cases | Governance allows / blocks approval |
| Expected | Approve enabled only when server `governance.canApprove` permits; confirm dialog with admin wording; no StudentSection; when blocked: disabled + business explanation; UI invents no extra rules |
| Actual | Not observed |
| Result | **NE** |

### TEST 12 — Responsive UX

| Field | Value |
|-------|--------|
| Viewports | Desktop and tablet widths |
| Expected | Usable buttons; Technical Details collapsed; workflow understandable; no overflow from technical identifiers; AI31 tokens intact |
| Actual | Not observed |
| Result | **NE** |

### TEST 13 — Regression (no workflow alteration)

| Field | Value |
|-------|--------|
| Areas | Attendance; manual attendance without timetable; timetable-driven attendance; Combined Section attendance; Scheduling; Faculty Workspace; Academic hierarchy |
| Expected | Unchanged behavior; this prompt applies **no** code changes |
| Actual | No production/business-logic edits performed for Prompt 4 |
| Result | **NE** (browser regression not run); **code delta for Prompt 4: none** |

---

## Defects discovered

None from live browser (session not possible).

No in-scope UI correction applied.

---

## Human re-run instructions

1. Start API (`Abhyanvaya.API`) and UI (`abhyanvaya-ui`).
2. Login as allocation administrator; repeat with `Allocation.Operations.View`.
3. Execute Tests 1–13 above; capture screenshots for Failures.
4. For Tests 9–10, use Prompt 3 labels (**Review Academic Scope**, **Replay Allocation**).
5. Update this document’s Actual / Result columns; attach screenshots under the Prompt 4 artifact folder.
6. Do not mark PASS based solely on Vitest / xUnit results.

---

## Related artifacts

- `docs/AI29_1D_24B_1_ADMINISTRATOR_UX_LANGUAGE.md`
- `docs/AI29_1D_24B_1_REBUILD_REPLAY_SEMANTICS.md`
- Prompt 2 / 2A / 3 automated UX tests (supporting only — not a manual substitute)
