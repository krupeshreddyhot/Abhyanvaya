# AI29.1D.24B.2 — Prompt 9 Implementation Prompts

**Phase title:** Final Security, Validation Data Cleanup & Timetable Acceptance  
**Author role:** Chief Architect / ADL governance  
**Audience:** Implementation agent (Cursor) under Chief Architect direction  
**Phase status entering Prompt 9:** `AI29.1D.24B.2` = **CONDITIONAL PASS**

---

## Architectural status (do not redesign)

Already proven and frozen:

| Area | Status |
|------|--------|
| Target Section scope / explicit selection / zero gates | PASS (Prompts 1–7) |
| No-timetable Faculty manual attendance hard gate | **PASS (live browser, Prompt 8)** |
| Optional Section manual attendance | PASS (Prompt 8) |
| Manual vs Timetable path separation | PASS (Prompt 8) |
| JWT assigned-role permission resolution + tenant isolation | **PASS (Prompt 8A)** |
| Architecture Guard (authoritative) | 29 / 0 / 0 (Prompt 8A) |
| AI29 / AI29.1D / AI22 / AI30 / AI31 regressions | Green at Prompt 8A |
| UI build / API build | PASS |

Still blocking full phase **PASS**:

| Gap | Prompt 8 result |
|-----|-----------------|
| Faculty WITH published/locked timetable → AttendanceSessionResolver path | **NOT EXECUTED — DATA UNAVAILABLE** |
| Combined Section A+B (SectionGroup + TimetableSections) | **NOT EXECUTED — DATA UNAVAILABLE** |
| Combined Section save integrity (live) | **NOT EXECUTED — DATA UNAVAILABLE** |
| Validation-data hygiene (Prompt 7 Sem-IV moves; Prompt 8 membership/password churn) | Documented, not cleaned |

---

## ADL / architectural invariants (non-negotiable)

### Dual attendance paths (must remain distinct at context resolution)

```
TIMETABLE MODE
Attendance UI
  → AttendanceSessionResolver   (sole timetable/session authority)
  → Existing Attendance APIs
  → Server authorization
  → Persistence

MANUAL MODE
Academic Scope
  → Course → Group → Semester → Optional Section → Subject → Period
  → Existing Attendance APIs
  → Server authorization
  → Persistence
```

### Forbidden production changes

Do **NOT** introduce or modify:

- AttendanceSessionResolver architecture (except documented defect of smallest additive fix)
- A second attendance resolver / second timetable resolver
- SectionGroup model redesign / TimetableSections redesign
- Subject Master relationship redesign
- Allocation Engine / Allocation Context / scoring
- Attendance eligibility engine redesign
- Faculty-section domain model redesign
- Attendance persistence architecture redesign
- React-side authorization / role-name checks
- Alternate JWT generation / second permission service / tenant bypass
- Hard-coded permissions in UI

### Security freeze (Prompt 8A)

Keep the JwtService contract:

1. Role IDs from `UserApplicationRoles` for the authenticated user  
2. `ApplicationRole` join may use `IgnoreQueryFilters()` **only** during auth  
3. Must retain `role.TenantId == user.TenantId`  
4. Permissions loaded only for those role IDs  
5. No cross-tenant role acquisition  

Do not “simplify” by removing `IgnoreQueryFilters()` without an equivalent tenant-safe auth-time solution proven by Prompt 8A tests.

### Data / environment safety

- Dedicated validation environment only  
- No production destructive cleanup  
- Prefer existing admin/scheduling APIs for timetable & SectionGroup preparation  
- Do not invent fake resolvers or fake combined-section entities  
- Do not delete legitimate operational data  
- Inventory before restore; restore only what Prompt 7/8 validation explicitly changed  

### Evidence rules

- Do **NOT** mark browser tests PASS from unit/integration tests alone  
- Do **NOT** mark browser PASS if not executed  
- Use exactly: `PASS` | `FAIL` | `NOT EXECUTED — DATA UNAVAILABLE`  
- Never count skipped automated tests as passed  
- Raw test-runner output is authoritative for Architecture Guard counts  

---

## Recommended execution order (do not run blindly as one step)

| Sub-prompt | Name | Mode |
|------------|------|------|
| **9.1** | Security freeze & validation-data inventory | Discovery + inventory (no cleanup yet) |
| **9.2** | Timetable & SectionGroup data preparation | Validation data via existing APIs only |
| **9.3** | Live timetable + combined acceptance | Browser acceptance Tests 3/4/9 + hard-gate recheck |
| **9.4** | Validation-data cleanup / restore | Controlled restore after evidence captured |
| **9.5** | Final regression, builds, phase closure | Automated suites + FINAL STATUS |

Artifacts root:

`D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI29.1D.24B.2\Prompt 9`

Do **not** overwrite Prompt 1–8 / 8A artifacts.

---

# Prompt 9.1 — Security Freeze & Validation-Data Inventory

## Objective

Prove the security posture from Prompt 8A is still intact, and produce an authoritative inventory of **all** validation-data mutations from Prompts 7–8 before any cleanup or timetable prep.

## Tasks

1. Re-read:
   - `docs/AI29_1D_24B_2_PROMPT_8A_JWT_PERMISSION_HARDENING.md`
   - `docs/AI29_1D_24B_2_FINAL_VALIDATION.md` (Prompt 7 + 8 + 8A sections)
   - Prompt 7 inventory / Prompt 8 `persona-prep.json` / `section-membership-prep.json`
2. Confirm `JwtService` still enforces:
   - `UserApplicationRoles` ownership  
   - `IgnoreQueryFilters()` only on ApplicationRole auth join  
   - `role.TenantId == user.TenantId`  
3. Re-run (must stay green):
   - `AI29_1D_24B2_Prompt8A_JwtPermissionIsolationTests`
   - Architecture Guard (`Prompt21|ArchitectureGuard`) — record **raw** Passed/Failed/Skipped
4. Inventory validation mutations (IDs only; no secrets):

| Category | What to capture |
|----------|-----------------|
| Prompt 7 | Semesters created (e.g. Sem IV), sections created (`CA-IV-A`, etc.), students moved to Sem IV (original vs current semester ids) |
| Prompt 8 | Faculty-A user/staff link, subject assignments added, StudentSections CA-A/CA-B assignments, RBAC role permission deltas |
| Passwords | Note MustChangePassword / reset churn for `teststaff1` without printing passwords |
| Timetable | Confirm Faculty-B (`knraj`) still `hasTimetable=false`; section-groups still empty (or not) |

5. Produce:

`docs/AI29_1D_24B_2_PROMPT_9_VALIDATION_DATA_INVENTORY.md`

and copy to Prompt 9 artifacts.

## Exit criteria

- Inventory complete and reviewable  
- JWT isolation tests PASS  
- Architecture Guard Failed = 0  
- **No cleanup performed yet**  
- **No timetable invented yet**

## Production code

**None**, unless a regression defect is proven (then smallest additive fix + tests).

---

# Prompt 9.2 — Timetable & SectionGroup Validation Data Preparation

## Objective

Create **legitimate** validation data so Prompt 8 Tests 3 / 4 / 9 can be executed — using existing scheduling / SectionGroup / TimetableSections administrative mechanisms only.

## Preconditions

- Prompt 9.1 inventory complete  
- Validation environment only  

## Required personas (reuse)

| Persona | Username (validation) | Requirement |
|---------|----------------------|-------------|
| FACULTY-A | `teststaff1` | No published/locked timetable; Attendance + Section.View as needed |
| FACULTY-B | `knraj` | Attendance permissions + **published/locked** timetable for a current/usable date |
| Admin | `admin` | Prepare timetable / SectionGroup via existing UI/API |

## Tasks

1. **Discover** existing scheduling entities for Faculty-B (staff link, subjects, rooms, periods, academic year).  
2. If a published/locked timetable already exists for Faculty-B: document and reuse.  
3. If not: create/publish using **existing** admin scheduling workflow only:
   - Prefer Draft → Review → Publish/Lock path already in product  
   - Timetable must resolve through **AttendanceSessionResolver** (`GET /api/attendance-resolution/current`) with `hasTimetable=true`  
4. **Combined class:**  
   - Prefer existing SectionGroup for CA-A + CA-B  
   - If none: create SectionGroup via existing `section-groups` APIs only  
   - Attach via TimetableSections (authoritative membership) — **no new combined Section entity**  
5. Verify API (no tokens in docs):

| Check | Expected |
|-------|----------|
| Faculty-B resolve | `hasTimetable=true`, mode Timetable (or equivalent product mode) |
| Participating sections | Includes CA-A + CA-B (or documented pair) |
| Faculty-A resolve | Still `hasTimetable=false` / Legacy manual path |

6. Document preparation in:

`docs/AI29_1D_24B_2_PROMPT_9_TIMETABLE_DATA_PREP.md`

Include entity IDs, dates used, SectionGroup id, Timetable id/status — **no credentials/tokens**.

## Hard rules

- Do **NOT** fake AttendanceSessionResolver  
- Do **NOT** bypass publish/lock rules  
- Do **NOT** invent React-side SectionGroup logic  
- If publish/lock cannot be completed safely: stop and report `NOT EXECUTED — DATA UNAVAILABLE` for Tests 3/4/9; do not force PASS  

## Exit criteria

- Faculty-B resolver proves timetable OR documented blocked with reason  
- SectionGroup A+B exists OR documented blocked  
- Faculty-A no-timetable invariant preserved  

---

# Prompt 9.3 — Live Timetable + Combined Acceptance (Browser)

## Objective

Obtain the remaining mandatory live-browser evidence for timetable-driven and combined-section attendance, without regressing the no-timetable hard gate.

## Preconditions

- Prompt 9.2 data prep complete (or explicitly blocked)  
- UI + API running current builds  

## Browser tests (acceptance)

### TEST 3 — Timetable-driven attendance (FACULTY-B)

Login as Faculty-B. Confirm published/locked timetable. Navigate Attendance via timetable-driven path.

Verify:

1. AttendanceSessionResolver is authoritative  
2. Course / Group / Semester / Subject / Period resolved correctly  
3. Room resolved where applicable  
4. Section / participating sections resolved correctly  
5. Date/session context correct  
6. Roster correct  
7. Mark + Save succeed  
8. UI does not reconstruct timetable eligibility independently  

Result: `PASS` | `FAIL` | `NOT EXECUTED — DATA UNAVAILABLE`

### TEST 4 — Combined Section A+B

Using legitimate combined operational class:

Verify:

1. Existing SectionGroup used  
2. TimetableSections authoritative  
3. Resolver returns participating Section IDs  
4. UI displays combined operational class appropriately  
5. Underlying A and B remain identifiable  
6. Combined roster correct  
7. Student Section info available where applicable  
8. Mark + Save succeed  
9. No new combined Section entity  

### TEST 9 — Combined save integrity

Where safely testable:

- Student ∈ A OR B accepted  
- Student outside A+B rejected atomically (no partial save)  
- Prefer existing Prompt 15A server tests if browser injection is unsafe; still record browser evidence for happy path  

### TEST 12 / TEST 1 regression (FACULTY-A) — hard gate

Re-confirm no-timetable path:

Attendance → Course → Group → Semester → Subject → Period → Students → Mark → Save  

If this fails: **FINAL STATUS = FAIL** (do not bypass).

### TEST 5 / TEST 6 / TEST 11 (smoke)

- No “Attendance unavailable because no timetable” for Faculty-A  
- Manual vs Timetable separation intact  
- Network shows `/attendance-resolution/current` (no direct timetable table reconstruction)

## Evidence

Screenshots + `browser-results.json` under Prompt 9 artifacts.  
No passwords/tokens in reports.

## Exit criteria

- Tests 3 & 4 PASS **or** honestly NOT EXECUTED  
- Hard gate Test 1/12 still PASS  
- No unauthorized production changes  

---

# Prompt 9.4 — Validation Data Cleanup & Restore

## Objective

After acceptance evidence is captured, restore validation-only mutations where safe, without destroying legitimate college data.

## Preconditions

- Prompt 9.3 evidence captured (or formally blocked)  
- Prompt 9.1 inventory is the restore checklist  

## Cleanup policy

| Item | Action |
|------|--------|
| Legitimate pre-existing sections/students/timetables | **Keep** |
| Prompt 7 Sem IV student moves | Restore original semester assignments **if** inventory lists exact before/after |
| Prompt 8 CA-A/CA-B StudentSections created for validation | End-date/remove **only** if inventory proves they were created solely for Prompt 8 and not used by legitimate ops |
| Prompt 9 published validation timetable / SectionGroup | Prefer leave if useful for future QA; if temporary, archive/unpublish via existing APIs only — document decision |
| Faculty-A `teststaff1` | Keep account; reset password via admin API to known validation secret if needed; do not delete |
| RBAC FACULTY role `Section.View` | Keep (correct product grant for Section-scoped attendance) unless inventory proves it was incorrectly added to a shared role and harms production semantics — prefer keep |

## Hard rules

- No destructive SQL deletes of unknown rows  
- No “cleanup” that breaks Faculty-A hard gate or Faculty-B timetable evidence without documenting why  
- If restore is ambiguous: **do not restore**; document as known limitation  

## Deliverable

`docs/AI29_1D_24B_2_PROMPT_9_CLEANUP_REPORT.md`

For each inventory item: `RESTORED` | `RETAINED` | `NOT RESTORED — AMBIGUOUS` | `N/A`

---

# Prompt 9.5 — Final Regression, Builds & Phase Closure

## Objective

Close AI29.1D.24B.2 with an authoritative final validation report.

## Automated regression (minimum)

Run and record Passed / Failed / Skipped:

- AI29, AI29.1A, AI29.1A.5–7  
- AI29.1B, AI29.1B.5, AI29.1B.7  
- AI29.1C, AI29.1C.5, AI29.1C.5A  
- AI29.1D, AI29.1D.10A, AI29.1D.15A  
- AI29.1D.24, AI29.1D.24A, AI29.1D.24B, AI29.1D.24B.2  
- AI22 Attendance  
- AI30 Scheduling / Optimization  
- AI31 Faculty Workspace / Dashboard  
- AttendanceSessionResolverTests  
- Prompt 8A Jwt isolation tests  
- Architecture Guard (Prompt 21 / 21A) — **raw runner output**

## Builds

- UI build PASS  
- API build PASS  
- Restart API; confirm running binary includes current JwtService  

## Final report

Update:

`docs/AI29_1D_24B_2_FINAL_VALIDATION.md`

Add **Prompt 9** section containing:

1. Security freeze confirmation (8A still green)  
2. Validation-data inventory summary  
3. Timetable prep summary  
4. Test 3 / 4 / 9 / 1 / 12 results  
5. Cleanup/restore outcomes  
6. Automated regression counts  
7. UI/API builds  
8. Architecture Guard raw counts  
9. Production-code changes (expect none, or smallest defect fixes only)  
10. Database/validation-data changes  
11. Known limitations  
12. **AI29.1D.24B.2 FINAL STATUS**

Copy all Prompt 9 docs/tests/scripts/screenshots to:

`D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI29.1D.24B.2\Prompt 9`

---

## FINAL STATUS rules for Prompt 9 / phase closure

### Prompt 9 = PASS only when

1. JWT/security freeze still PASS (8A tests + ArchGuard 0 failures)  
2. No-timetable hard gate still PASS (live)  
3. Test 3 PASS  
4. Test 4 PASS (where combined data exists; if created in 9.2, must execute)  
5. Test 9 PASS where executable (browser and/or 15A with clear mapping)  
6. Cleanup report complete (even if many items RETAINED)  
7. Full regression PASS  
8. UI + API builds PASS  
9. No architectural bypass introduced  

### Prompt 9 = CONDITIONAL PASS when

- Required timetable/SectionGroup data still unavailable after honest 9.2 attempt  
- OR cleanup items remain NOT RESTORED — AMBIGUOUS  
- AND hard gate + security freeze remain PASS  

### Prompt 9 = FAIL when

- No-timetable hard gate fails  
- Cross-tenant permission leakage / JwtService regression  
- Architecture Guard Failed > 0 (authoritative run)  
- Timetable path implemented by bypassing AttendanceSessionResolver  

### Phase AI29.1D.24B.2 FINAL STATUS

| Condition | Status |
|-----------|--------|
| Prompt 9 PASS and all prior mandatory gates PASS | **PASS** |
| Timetable/combined still unavailable; hard gate + security PASS | **CONDITIONAL PASS** |
| Hard gate or security FAIL | **FAIL** |

---

## Cursor execution guidance (Chief Architect)

Unlike feature prompts, do **not** execute 9.1–9.5 as one blind step.

1. Run **9.1** → stop for inventory review  
2. Run **9.2** → stop if publish/lock/SectionGroup blocked  
3. Run **9.3** only when 9.2 produced real resolver evidence  
4. Run **9.4** only after screenshots/results exist  
5. Run **9.5** last for closure  

### Distinctions that must remain visible in the final report

```
No timetable
  → Manual attendance AVAILABLE

Published timetable
  → AttendanceSessionResolver
  → Timetable-derived attendance
```

Timetable absence must **never** determine whether Faculty may use Attendance.

---

## Suggested credentials note (validation env only)

Use the validation credentials already established in Prompt 8 documentation.  
**Never** write passwords or JWT tokens into Prompt 9 reports.  
Reference usernames/roles only.

| Role | Username |
|------|----------|
| Admin | `admin` |
| Faculty-A (no TT) | `teststaff1` |
| Faculty-B (TT) | `knraj` |
| SuperAdmin | `superadmin` |

---

## Immediate implementer checklist (copy into agent kickoff)

```
[ ] 9.1 Inventory + Jwt/ArchGuard freeze
[ ] 9.2 Publish/lock Faculty-B timetable + SectionGroup A+B via existing APIs
[ ] 9.3 Live Tests 3,4,9 + hard-gate recheck
[ ] 9.4 Cleanup/restore per inventory
[ ] 9.5 Regressions + builds + FINAL_VALIDATION Prompt 9 section + artifact copy
```
