# AI29.1D.24B.2 — Final Validation (Prompt 7)

**Date/time:** 2026-08-11 ~09:40 IST  
**Phase:** Production-like browser validation data preparation & final acceptance  
**Production logic changes:** **NONE** (validation data + harness only)

## 1. Validation environment

| Item | Value |
|------|--------|
| UI | `http://localhost:5173` |
| API | `http://localhost:5210` / `https://localhost:7063` (restarted earlier for occupancy `sectionIds`) |
| College admin | University `001` (Osmania), College `1053`, Username `admin` |
| SuperAdmin (available) | `superadmin` (not required for A–H) |
| Browser harness | Playwright (`playwright-core` + system Chrome), headless |

## 2. Validation data used

Prepared via existing admin APIs (no new domain model):

| Data | Detail |
|------|--------|
| Academic Year | id=1 — Academic Year 2026-227 |
| Program | Commerce only (id=1) |
| Course | B.Com (id=1) |
| Groups | Computer Applications (id=2), Finance (id=1) |
| Semesters | III (id=3); **IV created** (id=9) for Test F |
| CA III Sections | `SCCA01`, `CA-A`, `CA-B` |
| Finance III Sections | `SC001`, `FIN-A`, `SC002` |
| CA IV Section | `CA-IV-A` (created) |
| Empty scope (Test H) | CA + **Semester I** — zero Sections |
| Sem IV population | 5 B.Com/CA students moved to Semester IV via existing Student update API (for workflow continue) |
| Faculty no-timetable | **Not available** — credentials not provided |

Inventory artifact: `scripts/ai29_1d_24b2_prompt7_inventory.json`

## Browser results A–I

| Test | Result | Evidence |
|------|--------|----------|
| A — Group scope (CA III) | **PASS** | Target Sections `SCCA01,CA-A,CA-B`; no Finance leak |
| B — All eligible | **PASS** | Next enabled |
| C — Explicit selection | **PASS** | 3 checkboxes; Selected 1→2→1 |
| D — Zero explicit | **PASS** | Next disabled + required message |
| E — Group change → Finance | **PASS** | CA cleared; Finance sections loaded; no stale CA selection |
| F — Semester III → IV | **PASS** | `CA-IV-A` shown; CA III sections gone; no stale selection |
| G — Program change | **NOT EXECUTED** | Validation tenant contains only one valid Program (Commerce). Do not invent a second Program. |
| H — Zero eligible Sections | **PASS** | Message exact; Next disabled; server context sections=0 |
| I — Faculty no timetable | **NOT EXECUTED** | DATA UNAVAILABLE — only admin/superadmin credentials supplied; no dedicated Faculty + Attendance.View + no-timetable persona |

### Additional browser / regression paths

| Item | Result |
|------|--------|
| Timetable-driven attendance | **NOT EXECUTED** — DATA UNAVAILABLE (faculty-with-timetable persona not provided) |
| Combined Section attendance | **NOT EXECUTED** — DATA UNAVAILABLE (not exercised in admin session) |
| Explicit selection contract | **PASS** (UI + Allocation Context authority) |
| API scope probe | CA3 / FIN3 / empty Sem I / CA4 contexts verified without printing tokens |

Screenshots (Prompt 7 folder): `p7-ca-iii.png`, `p7-before-group-change.png`, `p7-after-group-finance.png`, `p7-semester-iv.png`, `p7-zero-eligible.png`, `p7-final.png`, `browser-results.json`

## Automated regression counts

| Suite | Passed | Failed | Skipped |
|-------|--------|--------|---------|
| AI29.1A | 53 | 0 | 0 |
| AI29.1B | 41 | 0 | 0 |
| AI29.1C | 36 | 0 | 0 |
| AI29.1D | 318 | 0 | 0 |
| AI29.1D.10A | 20 | 0 | 0 |
| AI29.1D.24* | 102 | 0 | 0 |
| AI29.1D.24B.2 | 19 | 0 | 0 |
| AI22 Attendance | 33 | 0 | 0 |
| Scheduling / Phase2B (AI30) | 165 | 0 | 0 |
| AI31 Faculty/Dashboard | 71 | 0 | 0 |
| Architecture Guard / Prompt21 | 29 | 0 | 0 |

No skipped tests counted as passed.

## Builds

| Build | Result |
|-------|--------|
| UI (`npm run build`) | **PASS** |
| API (`dotnet build` alternate output) | **PASS** (when built from repo root) |

## Architecture Guard

**PASS** (29 / 0 failed / 0 skipped)

## API changes

**None in Prompt 7.**  
(Prior additive occupancy filter remains: `GET /api/sections/capacity/occupancy?sectionIds=`)

## Database changes

**None to schema.**  
Validation data only: sections `CA-A`/`CA-B`/`FIN-A`/`CA-IV-A` as needed; Semester IV; 5 students reassigned to Semester IV for population continuity.

## Files changed (Prompt 7)

| File | Purpose |
|------|---------|
| `scripts/ai29_1d_24b2_prompt7_data_prep.mjs` | Admin-API data preparation |
| `scripts/ai29_1d_24b2_prompt7_inventory.json` | Validation inventory |
| `scripts/ai29_1d_24b2_prompt7_browser.mjs` | Live browser harness |
| `docs/AI29_1D_24B_2_FINAL_VALIDATION.md` | This report |

No production Allocation / Attendance / Engine code modified.

## Known limitations

1. **Test G** cannot run — tenant has a single Program.
2. **Test I** / timetable / combined attendance cannot run — faculty personas/credentials not provided.
3. Sem IV population was enabled by reassigning 5 existing CA students to Semester IV (reversible via admin Student update).
4. After rapid academic-scope changes, the UI may briefly show “Unable to load eligible Sections”; Retry + wait settles to the authoritative Allocation Context (observed during harness hardening; not treated as a production redesign).

## Login credentials (reference)

| Role | Username | Password | Notes |
|------|----------|----------|-------|
| SuperAdmin | `superadmin` | `SuperAdmin@1` | Platform |
| College Admin | `admin` | `admin123` | University `001`, College `1053` |

## Final status (Prompt 7)

**CONDITIONAL PASS**

Mandatory live tests **A–F** and **H** passed with prepared validation data. **G** and **I** (and timetable/combined attendance) remain **NOT EXECUTED — DATA UNAVAILABLE**, so overall **PASS** is not declared per Prompt 7 rules.

---

# AI29.1D.24B.2 — Prompt 8: Faculty & Timetable Final Acceptance

**Date/time:** 2026-08-11 ~14:45 IST  
**Phase:** Live-browser Faculty no-timetable / timetable / combined acceptance gate

## Prompt 8 — required report fields

### 1. Faculty-A identity/purpose (no credentials)

| Item | Value |
|------|--------|
| Username | `teststaff1` |
| Purpose | FACULTY-A — valid tenant Faculty with Attendance permissions and **no** published/locked timetable |
| Staff link | Staff id 7 (teaching subjects for B.Com / Computer Applications / Semester III) |
| Application role | FACULTY (id 101) including `Section.View` + Attendance permissions |

### 2. Faculty-A timetable status

`AttendanceSessionResolver` → `mode=Legacy`, `hasTimetable=false`  
Message: Faculty has no published/locked timetable; use legacy attendance workflow.

### 3–14. Browser / acceptance tests

| Test | Result | Evidence |
|------|--------|----------|
| Test 1 — No timetable / legacy manual | **PASS** | Course→Group→Semester→Subject(English)→Period; Showing 50/235; Save/Update HTTP 200 |
| Test 2 — Optional Section manual | **PASS** | Section CA-A; Showing 20/20; Save HTTP 200; clear Section restored full-cohort path |
| Test 3 — Timetable-driven | **NOT EXECUTED — DATA UNAVAILABLE** | Faculty-B `knraj` login OK but resolver `hasTimetable=false` (no published/locked timetable) |
| Test 4 — Combined A+B | **NOT EXECUTED — DATA UNAVAILABLE** | `GET /api/section-groups` empty; no published combined timetable |
| Test 5 — No “Attendance unavailable” hard block | **PASS** | Manual path reachable; UI states timetable not required |
| Test 6 — Manual / Timetable path separation | **PASS** | Manual → Legacy cascade; Timetable would use AttendanceSessionResolver (source + network) |
| Test 7 — Section authorization (scoped selection) | **PASS** | CA-A within C/G/S; unauthorized Section injection covered by AI29.1D.15A (73 passed) |
| Test 8 — Student scope integrity | **PASS** | Exercised via AI29.1D.15A automated suite (73 passed, 0 failed); browser injection not performed |
| Test 9 — Combined save integrity | **NOT EXECUTED — DATA UNAVAILABLE** | No SectionGroup A+B data |
| Test 10 — Attendance regression | **PASS** | Prompt 11/11A/11B/12/13 + 15A + AI22 + AttendanceSessionResolver — all green (see counts) |
| Test 11 — Timetable authority (UI) | **PASS** | Network: `/api/attendance-resolution/current` (no direct timetable table reconstruction in UI) |
| Test 12 — No-timetable hard gate | **PASS** | Same as Test 1 — Mark→Save succeeded without timetable |

Screenshots: `p8-facultyA-attendance.png`, `p8-test1-manual.png`, `p8-test2-section.png`, `browser-results.json`

### 15. Automated regression counts

| Suite | Passed | Failed | Skipped |
|-------|--------|--------|---------|
| AI29 | 457 | 0 | 0 |
| AI29.1A | 53 | 0 | 0 |
| AI29.1A.5 | 14 | 0 | 0 |
| AI29.1A.6 | 18 | 0 | 0 |
| AI29.1A.7 | 12 | 0 | 0 |
| AI29.1B | 41 | 0 | 0 |
| AI29.1B.5 | 11 | 0 | 0 |
| AI29.1B.7 | 16 | 0 | 0 |
| AI29.1C | 36 | 0 | 0 |
| AI29.1C.5 | 28 | 0 | 0 |
| AI29.1C.5A | 16 | 0 | 0 |
| AI29.1D | 319 | 0 | 0 |
| AI29.1D.10A | 20 | 0 | 0 |
| AI29.1D.15A | 73 | 0 | 0 |
| AI29.1D.24 | 103 | 0 | 0 |
| AI29.1D.24A | 2 | 0 | 0 |
| AI29.1D.24B | 24 | 0 | 0 |
| AI29.1D.24B.2 | 20 | 0 | 0 |
| AI22 Attendance | 33 | 0 | 0 |
| AI30 Scheduling / Optimization | 165 | 0 | 0 |
| AI31 Faculty Workspace / Dashboard | 71 | 0 | 0 |
| Architecture Guard / Prompt21 | 29 | 0 | 0 |
| AttendanceSessionResolver | 22 | 0 | 0 |
| Prompt 8 JwtRolePermissionResolution | 1 | 0 | 0 |

Skipped never counted as passed.

### 16. UI build

**PASS** (`npm run build` in `abhyanvaya-ui`)

### 17. API build

**PASS** (`dotnet build Abhyanvaya.API`) — API restarted on `http://localhost:5210` after JwtService fix

### 18. Architecture Guard

**PASS** (29 passed / 0 failed / 0 skipped)

### 19. Database changes

**No schema changes.**  
Validation data only (existing admin APIs):

- Faculty user `teststaff1` password reset / staff link / FACULTY role permissions (`Section.View`)
- Staff teaching subject assignments for Sem III subjects
- 20 students assigned to Section `CA-A`, 20 to `CA-B` via `/api/student-sections`

Prompt 7 Sem IV student moves: **not restored** in Prompt 8 (documented; no destructive cleanup).

### 20. Production-code changes

| File | Change |
|------|--------|
| `Abhyanvaya.Infrastructure/Services/JwtService.cs` | Smallest additive fix: resolve application-role permissions via `IgnoreQueryFilters()` on `ApplicationRole` + direct `ApplicationRolePermissions` query so login ambient TenantId cannot drop assigned keys (e.g. `Section.View`) |
| `Abhyanvaya.Application.UnitTests/.../AI29_1D_24B2_Prompt8_JwtRolePermissionResolutionTests.cs` | Regression guard for the JwtService resolution pattern |

No AttendanceSessionResolver / Allocation Engine / SectionGroup / TimetableSections redesign.

### 21. Validation-data changes

Documented in Prompt 8 artifacts:

- `persona-prep.json`
- `section-membership-prep.json`
- Scripts: `ai29_1d_24b2_prompt8_*.mjs`

### 22. Known limitations

1. **Test 3 / Test 4 / Test 9** — NOT EXECUTED: no published/locked timetable for Faculty-B; no SectionGroup combined class.
2. Faculty-A password is reset by prep scripts and may be force-changed on first UI login (`MustChangePassword`).
3. Optional Section attendance requires `Section.View` in JWT; prior JwtService tenant-filter bug hid assigned role permissions at login (fixed).
4. Unscoped Sem III roster remains 235; section-scoped rosters reflect StudentSections membership (20+20 for CA-A/CA-B).

## Defect found & fixed (Prompt 8)

**Owning component:** `JwtService.ResolvePermissionKeysAsync`  
**Symptom:** Faculty with FACULTY application role including `Section.View` still received LegacyFacultySet-only JWT claims → `GET /api/sections` = 403 → empty Section selector options.  
**Root cause:** Navigating `UserApplicationRoles → ApplicationRole` applied ambient tenant query filters during login (TenantId often 0), yielding zero role rows and silent LegacyFacultySet fallback.  
**Fix:** Resolve role ids with `ApplicationRoles.IgnoreQueryFilters()` and load permission keys from `ApplicationRolePermissions` without ApplicationRole navigation under the filter.

## Prompt 8 final status

**CONDITIONAL PASS**

- Hard gate **Test 1 / Test 12 PASS** (no-timetable Faculty → Mark → Save).
- Tests 2, 5, 6, 7, 8, 10, 11 PASS; builds + Architecture Guard PASS.
- Tests **3, 4, 9** remain **NOT EXECUTED — DATA UNAVAILABLE** (timetable / combined SectionGroup), so full **PASS** is not declared per Prompt 8 rules.

---

# AI29.1D.24B.2 — Prompt 8A status

**PASS** — JWT permission hardening + Architecture Guard discrepancy resolved.

See:

- `docs/AI29_1D_24B_2_PROMPT_8A_JWT_DISCOVERY.md`
- `docs/AI29_1D_24B_2_PROMPT_8A_JWT_PERMISSION_HARDENING.md`

Authoritative Architecture Guard (Prompt 8A rerun): **Failed: 0, Passed: 29, Skipped: 0**.  
Prompt 8 raw artifact `28/1` was environmental (parallel snapshot / `--no-build` stale binary); guard rules were not weakened.

---

# AI29.1D.24B.2 Prompt 9 — Final Security, Validation Data Cleanup & Timetable Acceptance

**Date/time:** 2026-08-11 ~22:30 IST  
**Phase:** Final hardening + acceptance closure gate

## Production-code changes (audit wording)

A targeted authentication/authorization defect was fixed in `JwtService` during acceptance testing (Prompt 8 / hardened in Prompt 8A).  
**Prompt 9 applied no further JwtService production change.**

Correct architectural statement:

> Targeted authentication/authorization defect fix applied to JwtService.  
> No changes to Attendance, Timetable, Section, SectionGroup, Allocation Engine, Allocation Governance, or academic hierarchy business logic.

Do **not** state “Production code unchanged” for the overall AI29.1D.24B.2 acceptance history.

Prompt 9 additive artifacts only:

| Item | Purpose |
|------|---------|
| `AI29_1D_24B2_Prompt9_JwtCrossTenantSecurityTests.cs` | Named TEST A–F wrappers (delegate to Prompt 8A) |
| Discovery / inventory / probe scripts | Read-only validation evidence |
| Prompt 9 docs | Security, cleanup, timetable, final acceptance |

## 1. Security findings

- Prompt 8 defect confirmed: ambient TenantId filtering during login hid `ApplicationRole` → LegacyFacultySet → missing `Section.View`.
- `IgnoreQueryFilters()` on ApplicationRole at auth time is required and **must** remain paired with `role.TenantId == user.TenantId` + `UserApplicationRoles` ownership.
- Cross-tenant leak via IgnoreQueryFilters: **not observed** (automated Tests B/C).

## 2. JWT fix

Retained from Prompt 8/8A in `Abhyanvaya.Infrastructure/Services/JwtService.cs` (see Prompt 9.2 doc). No Prompt 9 production edit.

## 3. Cross-tenant tests

| Test | Result |
|------|--------|
| A Tenant A + Tenant A role | PASS |
| B Tenant A + unrelated Tenant B role | PASS |
| C IgnoreQueryFilters not a tenant bypass | PASS |
| D Missing/invalid role → no unauthorized perms | PASS |
| E Section.View continues | PASS |
| F Attendance.View / Attendance.Manage continue | PASS |

Suite: Prompt 9 wrappers **6/0/0** + Prompt 8A **12/0/0**.

## 4–7. Validation data inventory / cleanup / restore / retained

See `docs/AI29_1D_24B_2_PROMPT_9_VALIDATION_DATA_CLEANUP.md`.

- Inventory completed for Prompt 6/7/8 artifacts.
- **Cleanup executed:** none (safe retention preferred over destructive delete).
- **Restored:** none.
- **Sem IV students:** Original academic assignment could not be established; restoration not performed.
- **Intentionally retained:** Sem IV / `CA-IV-A` / `CA-A`/`CA-B`/`FIN-A` / StudentSections 20+20 / Faculty-A persona / FACULTY `Section.View` / staff-7 subjects.

## 8. Timetable availability

One Draft timetable (id=4). **No Published/Locked timetable.**  
SectionGroups: **0**. Combined A+B: **unavailable**.

## 9. Faculty persona used

| Persona | Username | Use |
|---------|----------|-----|
| Faculty-A | `teststaff1` | Manual / optional Section (Prompt 8 live evidence) |
| Faculty-B | `knraj` | Timetable probe — no published session |

## 10–11. Timetable / Combined browser results

| Path | Result |
|------|--------|
| Timetable-driven attendance | **NOT EXECUTED — DATA UNAVAILABLE** |
| Combined A+B attendance | **NOT EXECUTED — DATA UNAVAILABLE** |

No fake production data created to force PASS.

## 12–13. Manual / Section attendance

| Test | Result | Evidence |
|------|--------|----------|
| Manual no-timetable Mark→Save | **PASS** | Prompt 8 live browser (Faculty-A) |
| Optional Section Mark→Save | **PASS** | Prompt 8 live browser (CA-A, 20 students) |

## Final browser acceptance matrix

| Test | Result |
|------|--------|
| TEST 1 Faculty without timetable: C→G→S→Subject→Period→Mark→Save | **PASS** |
| TEST 2 Faculty without timetable: + Section → Mark→Save | **PASS** |
| TEST 3 Faculty with published timetable | **NOT EXECUTED — DATA UNAVAILABLE** |
| TEST 4 Combined A+B timetable attendance | **NOT EXECUTED — DATA UNAVAILABLE** |
| TEST 5 Manual fallback remains available | **PASS** |
| TEST 6 Manual/timetable architecture separation | **PASS** |
| TEST 7 Section authorization | **PASS** |
| TEST 8 Student save-scope integrity | **PASS / AUTOMATED SERVER-SIDE ONLY** (AI29.1D.15A 73/0/0) |
| TEST 9 Combined Section save integrity | **NOT EXECUTED — DATA UNAVAILABLE** |

## 14. Automated regression counts

Skipped never counted as passed.

| Suite | Passed | Failed | Skipped |
|-------|--------|--------|---------|
| AI29 (FQN ~AI29_) | 475 | 0 | 0 |
| AI29.1A (+.5/.6/.7) | 53 | 0 | 0 |
| AI29.1A.5 | 14 | 0 | 0 |
| AI29.1A.6 | 18 | 0 | 0 |
| AI29.1A.7 | 12 | 0 | 0 |
| AI29.1B | 41 | 0 | 0 |
| AI29.1B.5 | 11 | 0 | 0 |
| AI29.1B.7 | 16 | 0 | 0 |
| AI29.1C | 36 | 0 | 0 |
| AI29.1C.5 | 12 | 0 | 0 |
| AI29.1C.5A | 16 | 0 | 0 |
| AI29.1D (FQN ~AI29_1D_) | 337 | 0 | 0 |
| AI29.1D.10A | 20 | 0 | 0 |
| AI29.1D.15A | 73 | 0 | 0 |
| AI29.1D.24 (narrow filter) | 77 | 0 | 0 |
| AI29.1D.24A | 2 | 0 | 0 |
| AI29.1D.24B | 23 | 0 | 0 |
| AI29.1D.24B.2 | 38 | 0 | 0 |
| AI22 Attendance | 33 | 0 | 0 |
| AI30 Scheduling / Optimization | 165 | 0 | 0 |
| AI31 Faculty/Dashboard | 71 | 0 | 0 |
| AttendanceSessionResolver | 22 | 0 | 0 |
| JWT security (8/8A/9) | 19 | 0 | 0 |
| Prompt 16/16A | 18 | 0 | 0 |
| Architecture Guard 21/21A | 29 | 0 | 0 |
| Aggregate relevant filter run | 732 | 0 | 0 |

Prompt 18: no dedicated unit-test class; covered by adjacent permission/JWT suites (not counted as skipped-pass).

## 15. UI build

**PASS** (`npm run build` in `abhyanvaya-ui`)

## 16. API build

**PASS** (`dotnet build Abhyanvaya.API`)

## 17. Architecture Guard

**PASS** (29 passed / 0 failed / 0 skipped)

## 18. Database changes

**No schema changes.**  
No destructive cleanup. Sem IV student originals unrestored (unknown). Validation sections/memberships retained intentionally.

## 19. Production-code changes

| Scope | Prompt 9 |
|-------|----------|
| JwtService | No further edit (fix already present from Prompt 8/8A) |
| Attendance / Timetable / Section / SectionGroup / Allocation / hierarchy | **Unchanged** |
| Unit tests | Prompt 9 JWT wrapper tests added |

## 20. Remaining limitations

1. Timetable-driven attendance cannot be live-accepted until a **Published/Locked** timetable exists for a Faculty persona.
2. Combined A+B requires an authoritative SectionGroup + TimetableSections configuration.
3. Sem IV student original assignments were never inventoried; records left as-is.
4. Validation sections (`CA-A`/`CA-B`/`FIN-A`/`CA-IV-A`) remain in the tenant DB by design for this closure.

## 21. Final acceptance status

**CONDITIONAL PASS**

Gates that **must** pass for closure under Prompt 9 rules:

| Gate | Status |
|------|--------|
| No-timetable manual attendance | **PASS** |
| Optional Section manual attendance | **PASS** |
| JWT security / cross-tenant | **PASS** |
| Validation data reconciled (inventory + safe decisions) | **PASS** |
| Automated regression | **PASS** |
| UI build | **PASS** |
| API build | **PASS** |
| Architecture Guard | **PASS** |
| Timetable attendance (if data exists) | **N/A — DATA UNAVAILABLE** |
| Combined A+B (if data exists) | **N/A — DATA UNAVAILABLE** |

Because timetable/combined data does not exist, full **PASS** is not declared.  
Per FINAL STATUS RULE: **FINAL STATUS = CONDITIONAL PASS**.

### Chief Architect freeze

**AI29.1D.24B.2 is ready for closure** under CONDITIONAL PASS.  
Do not open another feature phase merely for UI wording or allocation expansion. Timetable/combined live acceptance is a **separate future scope** when legitimate published timetable + SectionGroup data exist.
