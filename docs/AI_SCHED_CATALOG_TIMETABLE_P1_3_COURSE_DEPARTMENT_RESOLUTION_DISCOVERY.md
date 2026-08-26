# AI-SCHED-CATALOG/TIMETABLE — P1-3 Prompt 1  
# Course Department / Program Resolution — Architecture Discovery & Contract

**Workstream:** AI-SCHED-CATALOG/TIMETABLE  
**Prompt:** P1-3 Prompt 1 — Architecture Discovery & Resolution Contract  
**Date:** 2026-08-22  
**Type:** **READ-ONLY DISCOVERY** — no production behavior changed  
**Final status: PASS — discovery complete; Chief Architect decisions listed**

---

## 0. Scope and non-actions (this prompt)

| Action | Status |
| --- | --- |
| Production code changes | **NONE** |
| Schema / migrations | **NONE** |
| API behavior changes | **NONE** |
| UI behavior changes | **NONE** |
| Teaching Group / CAP changes | **NONE** |
| Undo / redesign of P1-2 (`Program.DepartmentId`) | **FORBIDDEN** |

This document is the resolution contract baseline for P1-3 Prompt 2+.

---

## 1. Current-state architecture

### 1.1 Approved target hierarchy (governance — frozen intent)

```text
College
  └── Department
       ├── Program (OPTIONAL via EnablePrograms)
       │     └── Course
       │           └── Group → Semester → Teaching Group
       └── Course (when Programs disabled)
             └── Group → Semester → Teaching Group
```

| Mode | Catalog path |
| --- | --- |
| `EnablePrograms = true` | Department → Program → Course |
| `EnablePrograms = false` | Department → Course |

P1-2 frozen: every **Program** has required `DepartmentId` (same Tenant + College); `CollegeId` retained; `EnablePrograms` authoritative for Program feature use.

### 1.2 Implemented catalog model (today)

```text
College
  ├── Program (P1-2: DepartmentId required; CollegeId retained)
  │     └── Course.ProgramId?   ← optional even when EnablePrograms=true
  └── Course                    ← NO Course.DepartmentId
        └── Group (CourseId)
              └── Semester (CourseId, GroupId?)
```

| Entity | Department link | Notes |
| --- | --- | --- |
| **Program** | `DepartmentId` (required) | P1-2 complete |
| **Course** | **None** | Only optional `ProgramId` |
| **Group / Semester** | Via Course only | Unaffected by Department ownership |
| **Department** | College-scoped catalog SSOT | Also used by Staff / Scheduling |

### 1.3 Implemented scheduling / operational model (today)

```text
SubjectAllocation
  DepartmentId + CourseId + GroupId + SemesterId + SubjectId + StaffId + AcademicYearId
  (unique includes DepartmentId)

TimetableEntry
  DepartmentId denormalized from SubjectAllocation (on create/update from allocation)
  TeachingGroupId? explicit (frozen TG)
```

| Fact | Evidence |
| --- | --- |
| SA chooses Department independently of Course | `SubjectAllocationService` validates Department exists; **no** Course↔Department consistency check |
| Same Course can appear under multiple Departments in SA | Unique key includes `DepartmentId` as a dimension |
| TimetableEntry.DepartmentId is operational denorm | `TimetableService` sets `entry.DepartmentId = allocation.DepartmentId` |
| TeachingGroup has no DepartmentId | Links via SubjectAllocation |

### 1.4 EnablePrograms behavior (today)

| Mode | Course.ProgramId | Course Department |
| --- | --- | --- |
| Enabled | Optional (`null` = “No Program” allowed by AI29.1D.24) | Not stored |
| Disabled | Forced null on assign path (`CourseProgramAssignmentRules.EvaluateDisabled`) | Not stored |

UI: `CoursesPage` shows Program selector only when enabled; create Program gated on Programs page.

### 1.5 Local data snapshot (evidence, not migration)

| Row | Observation |
| --- | --- |
| Courses | 1 active (`B.Com`, `ProgramId=1`) |
| Programs | 1 (`PG001`, `DepartmentId=1`) |
| SubjectAllocations | 1 (`CourseId=1`, `DepartmentId=1` = Program’s Department) |
| Multi-dept SA per Course | **0** locally |

Safe mapping path for this tenant: Course → Program → `Program.DepartmentId`. Environments with `ProgramId=null` Courses and multiple Departments remain ambiguous.

---

## 2. Target-state options A / B / C / D

### Option A — `Course.DepartmentId` authoritative in all modes

**Meaning:** Course always stores owning `DepartmentId`. When `ProgramId` is set, it **must** equal `Program.DepartmentId`.

| Pros | Cons |
| --- | --- |
| Matches target: Course always under a Department | Requires schema + migration |
| Works when Programs enabled but Course has no Program (unassigned) | Must define SA consistency vs Course ownership |
| Single catalog ownership field (no mode-switching SoT) | Risk if historical SA.DepartmentId ≠ Course.DepartmentId |
| Aligns with P1-2 Program ownership style | UI/API Course CRUD must accept Department |

### Option B — `Course.DepartmentId` authoritative only when Programs disabled; `Program.DepartmentId` authoritative when enabled

**Meaning:** Two different ownership fields depending on `EnablePrograms`.

| Pros | Cons |
| --- | --- |
| Avoids storing Department when always under Program | **Dual source of truth** by mode — violates “no second Department SoT” spirit |
| | Unassigned Courses (`ProgramId=null`) with Programs **on** have **no** Department owner |
| | Flag flips change which field is authoritative — dangerous for queries/caches |
| | Harder migrations and validation |

**Rejected as primary model.**

### Option C — Derive Department from Program when enabled; `Course.DepartmentId` only when disabled

**Meaning:** Programs on ⇒ never store / ignore Course.DepartmentId; resolve via Program. Programs off ⇒ store Course.DepartmentId.

| Pros | Cons |
| --- | --- |
| No redundant Department when Program assigned | Same failure as B for `ProgramId=null` while Programs on |
| | Mode-dependent presence of column semantics |
| | Toggle EnablePrograms requires backfill / clear of Course.DepartmentId |
| | Resolvers proliferate (catalog, SA, TT, UI) |

**Rejected as sole model** unless Chief Architect also makes ProgramId **mandatory** when EnablePrograms=true (product change beyond current AI29.1D.24).

### Option D — Repository-supported alternative: keep Department only on scheduling (SA/TT); no Course.DepartmentId

**Meaning:** Catalog Course remains Department-free; UI filters invent Department→Course association at allocation time only.

| Pros | Cons |
| --- | --- |
| Zero Course schema change | **Does not satisfy** approved Department → Course hierarchy when Programs disabled |
| Matches today’s SA behavior | Leaves catalog incomplete vs governance |
| | Continues dual/implicit ownership via operational data |

**Rejected for catalog ownership.** May remain relevant for **how** SA/TT continue to store DepartmentId as denormalized operational fields after catalog ownership is fixed.

---

## 3. Evidence summary (answers to required analysis)

| # | Question | Finding |
| --- | --- | --- |
| 1 | Current Course ownership | Tenant-scoped Course master; optional `ProgramId`; **no Department** |
| 2 | Program → Course | `Course.ProgramId` nullable FK; assign via `AssignCourseToProgramAsync` / Course Master |
| 3 | Department in Course workflows | Not on Course; only via SA/TT UI filters (independent select) |
| 4 | Course without Program? | **Yes** — allowed; forced when Programs disabled |
| 5 | EnablePrograms=false | ProgramId cleared/forced null; Course→Group→Semester unchanged |
| 6 | EnablePrograms=true | Program optional on Course; hierarchy tree roots Programs + unassigned Courses |
| 7 | Safe Department map for Courses? | **If `ProgramId` set:** deterministic via `Program.DepartmentId`. **If null:** only if exactly one Department per College (same rule as P1-2) or manual map — otherwise STOP |
| 8 | Course in multiple Departments? | **Catalog:** not modeled. **Scheduling:** possible today (SA unique includes DepartmentId) |
| 9 | Store vs derive Department on Course | Recommend **store** (`Course.DepartmentId`) + validate against Program when linked |
| 10 | Group → Semester | Remains Course-owned; no Department FK needed on Group/Semester for P1-3 |
| 11 | SubjectAllocation Department | Today independent; post-contract should **align to Course.DepartmentId** (denorm), not become a second catalog SoT |
| 12 | TimetableEntry Department | Remains denorm from SA; no new SoT |
| 13 | Inconsistency risk | High if Course gains DepartmentId while legacy SA rows use another DepartmentId |
| 14 | Tenant/College | Department must match Course tenant; Program.CollegeId / Department.CollegeId consistency (P1-2 pattern) |
| 15 | Migration | Additive `Course.DepartmentId`; deterministic backfill from Program; abort on unmapped |
| 16 | Data risks | Null-Program Courses in multi-dept colleges; multi-dept SA rows |
| 17 | API | Course create/update DTOs gain DepartmentId; Program assign must keep Department consistent |
| 18 | UI | CoursesPage Department selector; Program selector remains EnablePrograms-gated |
| 19 | TG/CAP | No TG/CAP schema/behavior change required for catalog ownership; regression must stay green |

---

## 4. Recommended architecture (for Chief Architect confirmation)

### Recommendation: **Option A** — `Course.DepartmentId` is the catalog ownership SSOT in all modes

**With Program consistency invariant (not Option B):**

```text
Course.DepartmentId  →  Department.Id     (required for owned Courses)
Course.ProgramId?    →  Program.Id        (optional layer; EnablePrograms governs UI/assign)

WHEN Course.ProgramId IS NOT NULL:
  Course.DepartmentId MUST EQUAL Program.DepartmentId
  (same Tenant; Program.CollegeId / Department.CollegeId already constrained by P1-2)

WHEN EnablePrograms = false:
  Course.ProgramId MUST be null
  Course.DepartmentId remains the sole Department path
```

### Why not B/C/D

- **B/C** create mode-dependent ownership and fail for “Programs on + No Program” Courses (current product behavior).
- **D** does not meet governance “Department → Course when Programs disabled.”
- **A** matches P1-2 pattern (explicit ownership FK), preserves EnablePrograms, and gives SA/TT a single catalog Department to validate against.

### Scheduling relationship (contract, not implemented here)

| Layer | Role after P1-3 |
| --- | --- |
| `Course.DepartmentId` | **Catalog ownership SoT** |
| `SubjectAllocation.DepartmentId` | **Operational denormalization** — must equal `Course.DepartmentId` (recommended invariant for Prompt 2+/SA prompts) |
| `TimetableEntry.DepartmentId` | Continues to copy from SA; no independent catalog meaning |

**Do not** invent a second catalog Department SoT on TeachingGroup, Student, or Attendance.

---

## 5. Explicit invariants (proposed)

1. `Program.DepartmentId` remains required (P1-2 frozen).
2. `Course.DepartmentId` becomes required for catalog ownership (Prompt 2+).
3. If `Course.ProgramId` set ⇒ `Course.DepartmentId == Program.DepartmentId`.
4. If `EnablePrograms = false` ⇒ `Course.ProgramId` is null.
5. Group/Semester remain Course children only.
6. Cross-tenant Course↔Department association is impossible (server-side).
7. No arbitrary/default Department backfill; unmapped Courses abort migration or use documented manual map.
8. TG/CAP frozen rules unchanged.
9. `EnablePrograms` remains the Program **feature** flag — it does not remove Department ownership from Course.

---

## 6. Explicit non-actions (P1-3 Prompt 1 and boundaries)

| Non-action | Reason |
| --- | --- |
| Do not implement `Course.DepartmentId` in this prompt | Discovery only |
| Do not modify SubjectAllocation / TimetableEntry / TeachingGroup | Frozen / later prompts |
| Do not modify Attendance / StudentSection | Frozen |
| Do not undo P1-2 | Frozen |
| Do not make ProgramId mandatory without CA decision | Conflicts with AI29.1D.24 “No Program” |
| Do not derive Course Department only from SA rows | Would reverse catalog/operational SoT |

---

## 7. Data migration impact (Prompt 2+ preview)

| Step | Rule |
| --- | --- |
| 1 | Add nullable `Course.DepartmentId` |
| 2 | Backfill from `Program.DepartmentId` where `Course.ProgramId` is set |
| 3 | For remaining nulls: apply **exactly-one Department per TenantId+CollegeId** only if College scope on Course can be established; else STOP for manual map |
| 4 | Enforce NOT NULL + Restrict FK when all rows mapped |
| 5 | Optional report: SA rows where `SA.DepartmentId <> Course.DepartmentId` (remediation before hardening SA validation) |

**Local DB:** Course `B.Com` → Program `PG001` → Department `1` — deterministic.

---

## 8. Tenant / College implications

- Course is tenant-scoped (`BaseEntity.TenantId`); Department is tenant + `CollegeId`.
- Course currently has **no CollegeId**; Department college consistency goes through Program when linked, or via Department’s College when only DepartmentId is set.
- Open: whether Course should gain `CollegeId` denorm (not required if Department FK + tenant filters suffice). Prefer **not** duplicating College unless existing architecture demands it.

---

## 9. API implications (Prompt 2+)

| Surface | Expected change |
| --- | --- |
| `CreateCourseRequest` / `UpdateCourseRequest` | Add `DepartmentId` |
| `CourseMasterRowDto` | Expose `DepartmentId` (+ optional names) |
| `CourseMasterWriteService` | Validate Department tenant/college; sync/check vs Program |
| `AssignCourseToProgramAsync` | When assigning Program, set/verify Course.DepartmentId = Program.DepartmentId |
| Program/Course list filters | Optional `departmentId` filter (later UX) |

Authorization: reuse existing Course / Program policies — no bypasses.

---

## 10. UI implications (Prompt 2+)

| Surface | Change |
| --- | --- |
| `CoursesPage` | Department selector (always, for ownership); Program selector still EnablePrograms-gated |
| Program assign flows | Changing Program may update/require matching Department |
| Subject Allocation / Timetable | **Out of scope for early P1-3 unless CA expands**; later enforce Course-filtered Department |

---

## 11. TG / CAP regression implications

| Area | Impact of recommended model |
| --- | --- |
| TeachingGroupSection / projector | None if SA/TT schemas untouched in Prompt 2 catalog phase |
| TimetableEntry.TeachingGroupId | Unchanged |
| Capacity / Conflict / Publish | Unchanged if no SA Department rewrite |
| Risk | Later enforcing SA.DepartmentId == Course.DepartmentId may reject legacy multi-dept allocations — handle in SA prompt with data report, not silent delete |

---

## 12. Proposed implementation sequence (Prompt 2+)

| Step | Prompt focus |
| --- | --- |
| **P1-3 Prompt 2** | Domain + EF: add `Course.DepartmentId`; migration with deterministic Program-based backfill; abort if unmapped; validators + CourseMasterWriteService + AssignCourse consistency; Course DTOs/API; CoursesPage Department UI; tests/guards |
| **P1-3 Prompt 3** (if needed) | SA/TT consistency validation (SA.DepartmentId must equal Course.DepartmentId); data remediation report; **no** TG/CAP redesign |
| Later backlog | Academic tree roots under Department; cascade helpers; fail-closed Dept→Program→Course filters |

**Do not** start Prompt 2 until Chief Architect confirms §4 and answers §13.

---

## 13. Open questions — Chief Architect decision required

| ID | Question | Recommendation |
| --- | --- | --- |
| **Q1** | Confirm Option A (`Course.DepartmentId` always authoritative + Program consistency)? | **Yes — adopt A** |
| **Q2** | When `EnablePrograms=true`, may Course remain with `ProgramId=null` (“No Program”) while still requiring `DepartmentId`? | **Yes — preserve AI29.1D.24**; Department required, Program optional |
| **Q3** | After Course ownership exists, must `SubjectAllocation.DepartmentId` always equal `Course.DepartmentId`? | **Yes** (denorm); implement in SA-focused follow-on, not silently in Prompt 2 if data conflicts exist |
| **Q4** | Should uniqueness on SA drop `DepartmentId` once it is pure denorm? | **Defer** — changing uniqueness is a separate migration risk; keep unique key until CAP/SA impact assessed |
| **Q5** | Courses with null ProgramId and multiple Departments in College — mapping strategy? | **Manual worksheet / STOP** — no invented defaults (same as P1-2) |

---

## 14. Architecture violations observed (current system vs target)

| Violation | Severity |
| --- | --- |
| Course not under Department in catalog FK tree | **Major** — P1-3 purpose |
| SA Department independent of Course ownership | **Major** for scheduling consistency after Course ownership |
| Academic tree roots Program/Course, not Department | Medium — UX/hierarchy presentation |
| AI29.1A docs still say College → Program (pre-P1-2 wording) | Docs drift — update when implementing |

No TG/CAP freeze violations introduced by this discovery prompt.

---

## 15. Risks

1. Multi-department SubjectAllocations for one Course become illegal under recommended SA invariant.
2. Null-Program Courses in multi-dept tenants block NOT NULL migration.
3. Opportunistic SA uniqueness change could break CAP/TG tests — defer.
4. Implementing Option B/C would break “No Program” Courses or create dual SoT.

---

## 16. Deferred work

- Implement Course.DepartmentId (Prompt 2+)
- SA/TT Department consistency enforcement
- Academic hierarchy tree under Department
- Student/Semester remapping (separate backlog)
- Doc refresh of AI29.1A hierarchy diagrams to Department → Program

---

## 17. Files created (this prompt)

| File | Purpose |
| --- | --- |
| `docs/AI_SCHED_CATALOG_TIMETABLE_P1_3_COURSE_DEPARTMENT_RESOLUTION_DISCOVERY.md` | This contract |
| `Abhyanvaya.Application.UnitTests/Academic/AiSchedCatalogTimetableP13Prompt1ResolutionContractGuardTests.cs` | Read-only contract guards |

**Production files changed: NONE**

---

## 18. STOP

Discovery complete. **Do not implement P1-3 Prompt 2** until Chief Architect confirms Q1–Q5.
