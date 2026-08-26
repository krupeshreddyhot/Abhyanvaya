# AI-SCHED-TG.4A Prompt 2 — Legacy Section Bridge Contract

**Workstream:** AI-SCHED-TG.4A — Legacy TimetableSection Bridge & TeachingGroup Projection  
**Prompt:** 2 — Legacy Bridge Contract Design  
**Date:** 2026-08-18  
**Type:** DESIGN CONTRACT ONLY — **no implementation**  
**Predecessor:** AI-SCHED-TG.4A Prompt 1 (PASS — discovery)  
**Related:** AI-SCHED-TG.2A Prompt 3 (bridge design), AI-SCHED-TG.4 Prompts 2–4 (`TeachingGroupId` + mutation invariants)

**STATUS: PASS**

---

## 1. Purpose

Lock the semantics for retrofitting:

```text
PUT /api/timetable/{timetableId}/sections
```

so that:

| Role | Entity |
|---|---|
| **Source of truth** | `TeachingGroupSection` |
| **Projection / association** | `TimetableSection` |
| **Entry ownership** | `TimetableEntry.TeachingGroupId` (explicit; already enforced by TG.4) |

Do **not** reverse SoT. Do **not** implement in this prompt.

---

## 2. Target flow (approved)

```text
Legacy PUT /sections  (unchanged external contract)
        │
        ▼
Timetable / section application façade
  (preserve ISectionManagementService entrypoint OR thin adapter)
        │
        ▼
TeachingGroup application boundary
        │
        ├── Validate tenant, auth, lifecycle, TG, sections
        ├── Replace TeachingGroupSection set (SoT)
        │
        ▼
TimetableSection projector (approved writer only)
        │
        ▼
TimetableSection rows for TimetableEntry
        │
        ▼
Attendance / GET /sections (read projection — unchanged consumers)
```

External callers continue to send `{ timetableEntryId, sectionIds }` only.  
They are **not** required to understand TeachingGroup internals.

---

## 3. Command / input semantics

### 3.1 External command (backward compatible)

| Field | Type | Meaning |
|---|---|---|
| Route `timetableId` | `int` | Timetable header |
| `TimetableEntryId` | `int?` | Target entry (required for bridge; null/missing → reject) |
| `SectionIds` | `int[]` | Desired academic Section set for the entry’s TeachingGroup |

**Unchanged DTO:** `SetTimetableSectionsRequest` / `TimetableSectionDto`  
**Unchanged route:** `PUT/GET /api/timetable/{timetableId}/sections`

### 3.2 Internal command (application)

Conceptual (exact names follow codebase conventions in Prompt 3–5):

```text
ReplaceTeachingGroupSectionsForTimetableEntry(
    tenantId,           // ambient
    timetableId,
    timetableEntryId,
    desiredSectionIds   // distinct, > 0
)
```

Resolved internally:

1. Load `Timetable` + `TimetableEntry` (tenant-scoped).  
2. Require `entry.TeachingGroupId` (see §12).  
3. Load `TeachingGroup` (tenant-scoped; no filter bypass).  
4. Validate compatibility (TG.4 rules).  
5. Replace SoT section links on that TeachingGroup.  
6. Project SoT → `TimetableSection` for **this entry**.  
7. Commit once.

### 3.3 What the command does **not** accept

- Implicit TeachingGroup selection  
- SubjectAllocation-only resolution  
- Auto-create TeachingGroup  
- Section inference from Room / capacity / enrollment  

---

## 4. Source-of-truth semantics

**Canonical:** `TeachingGroupSection` rows for `TeachingGroupId = entry.TeachingGroupId`.

| Rule | Behavior |
|---|---|
| Authority | Only TeachingGroup application boundary mutates TeachingGroupSection |
| Cardinality | 0, 1, or many sections per TeachingGroup |
| Combined classes | Multiple sections explicitly allowed (`CombinedSections` type; also other types that allow multi) |
| Elective / subset | Zero sections may be valid per `TeachingGroupRules.ValidateSectionLinks` |
| Not equal to | Academic `Section` identity, `SectionGroup`, or `StudentSection` |

`TimetableEntry.TeachingGroupId` answers: *which TeachingGroup owns this placement*.  
`TeachingGroupSection` answers: *which academic Sections participate in that cohort*.

These are separate concepts and must not be collapsed.

---

## 5. Projection semantics

**Projection:** `TimetableSection` for `(TenantId, TimetableId, TimetableEntryId)`.

| Rule | Behavior |
|---|---|
| Direction | TeachingGroupSection → TimetableSection **only** (no reverse sync) |
| Matching rows | Kept |
| Missing rows | Created |
| Obsolete rows | Soft-deleted (`IsDeleted = true`) — matches today’s removal lifecycle |
| Duplicates | Forbidden; unique index `(TenantId, TimetableId, TimetableEntryId, SectionId)` WHERE not deleted |
| Idempotent | Same SoT + same entry → same projection after N runs |
| Scope | Project **only** the TimetableEntry being mutated (not other entries sharing the TG) |
| Must not | Create/infer TG; change `TeachingGroupId`; touch StudentSection / Attendance / SubjectAllocation |

**Shared TeachingGroup caveat:** Multiple TimetableEntries may reference the same TeachingGroup. Replacing TeachingGroupSection is **global to the TeachingGroup**. Projection for the current entry is updated in the same transaction. Other entries that share the TG may temporarily have stale TimetableSection rows until they are re-projected (Prompt 4/5 should either re-project all entries for that TG in-tenant or document the stale-read window). **Contract decision:** Prompt 5 implementation **must re-project all TimetableEntries in the same tenant that reference the same TeachingGroupId** within the same transaction, so Attendance and GET remain coherent.

---

## 6. Transaction boundary

| Step | Same transaction? |
|---|---|
| Load + validate | Yes (read) |
| TeachingGroupSection replace | Yes |
| TimetableSection projection | Yes |
| `SaveChanges` | **Single** commit |

Failure after validation → no partial SoT/projection write.  
No separate “eventual” projection job for the legacy PUT path.

Reuse ambient `IUnitOfWork` / DbContext save (existing pattern). Explicit `BeginTransaction` only if the codebase already requires it for multi-DbContext cases (not expected here).

---

## 7. Idempotency

| Scenario | Result |
|---|---|
| PUT same `SectionIds` twice | TeachingGroupSection set unchanged in effect; projection unchanged; HTTP 200 + current DTO list |
| PUT subset then restore | SoT and projection match final set |
| Soft-deleted then re-add same SectionId | New non-deleted TimetableSection row (existing unique filter allows) |

Idempotency is **state-based**, not request-id based.

---

## 8. Duplicate handling

| Layer | Rule |
|---|---|
| Request | `SectionIds.Distinct()` before apply |
| TeachingGroupSection | Reject duplicate active `(TeachingGroupId, SectionId)` — DB unique + domain |
| TimetableSection | Never create duplicate active projection rows |
| Domain type rules | `TeachingGroupRules.ValidateSectionLinks(type, sectionIds)` before persist |

---

## 9. Removal semantics

| Intent | Mechanism |
|---|---|
| Remove one section from multi | Desired set omits that id → SoT link removed/soft-deleted; projection soft-deleted |
| Clear all sections | `SectionIds = []` → SoT empty (if type allows); all projection rows for affected entries soft-deleted |
| Type forbids empty/multi | Domain validation **rejects** before mutation |

Removal never hard-deletes TimetableSection (preserve today’s soft-delete semantics and unique filtered index).

---

## 10. Empty section list semantics

```text
SectionIds = []
```

Means: **explicit clear** of TeachingGroup ↔ Section links for the entry’s TeachingGroup (subject to type rules), then project empty TimetableSection set for all entries bound to that TG.

| TeachingGroupType | Empty allowed? |
|---|---|
| `SectionDerived` | **No** (requires exactly one) — reject |
| `CombinedSections` | **No** (requires ≥ 2) — reject |
| `Elective` | **Yes** (must not require section links) |
| `StudentSubset` / `Laboratory` / `CapacitySplit` / `Custom` | **Yes** (optional sections) |

This is stricter than today’s API (which always allowed empty). Document as intentional SoT correctness: legacy empty clear succeeds only when the TeachingGroup type permits zero sections.

---

## 11. Concurrency behavior

| Concern | Contract |
|---|---|
| Timetable lifecycle | Reuse `TimetableService.EnsureDraft` — frozen/published/locked timetables reject section mutation |
| TeachingGroup status | `EnsureCanMutate` — Locked/Archived/deleted TG reject section-set changes |
| EF concurrency | Participate in existing `SaveChanges` / concurrency helper; no second token model |
| Stale overwrite | Last writer wins at SaveChanges unless rowversion exists; document limitation if none (do not invent) |

Order:

```text
Authorization → Tenant → Lifecycle (timetable Draft) → Load entry/TG
  → Build desired SoT → Domain type + academic scope validation
  → Persist TeachingGroupSection → Project TimetableSection → Save
```

---

## 12. Legacy data: TimetableSections present, TeachingGroupId null

**Environment:** Pre-production; timetable test data disposable.  
**No permanent production backfill** (Prompt 7 handles optional explicit conversion).

### 12.1 PUT `/sections` when `TeachingGroupId == null`

| Option | Decision |
|---|---|
| Auto-create TeachingGroup | **Forbidden** |
| Infer TG from SubjectAllocation | **Forbidden** |
| Infer TG from existing TimetableSections | **Forbidden** |
| Continue writing TimetableSection only | **Forbidden** after bridge (would keep competing SoT) |
| **Approved behavior** | **Reject** with actionable application error |

**Error (semantic):**

> This timetable entry has no Teaching Group assigned. Assign a Teaching Group first, then set sections.

HTTP mapping: `400 BadRequest` via existing `DomainException` / `InvalidOperationException` pattern (same as today’s controller).

**Prerequisite path (already exists — TG.4 Prompt 3):**

```text
PUT .../entries/{entryId}/teaching-group  { teachingGroupId }
THEN
PUT .../timetable/{id}/sections  { timetableEntryId, sectionIds }
```

### 12.2 GET `/sections` when `TeachingGroupId == null`

Continue returning existing `TimetableSection` rows (read compatibility).  
**Do not** repair, create TG, or mutate on GET.

### 12.3 Attendance when `TeachingGroupId == null`

Unchanged: resolver reads `TimetableSections`; Legacy mode unchanged.

### 12.4 Disposable conversion

If tests need entries with both TG and sections, use **Prompt 7** explicit admin/dev conversion (not automatic). Until then, reject PUT without TG.

---

## 13. Tenant validation

Mandatory for every involved record:

```text
Timetable.TenantId
  == TimetableEntry.TenantId
  == TeachingGroup.TenantId
  == each Section.TenantId
  == each TeachingGroupSection.TenantId
  == each TimetableSection.TenantId
  == ambient ICurrentUserService.TenantId
```

| Rule | |
|---|---|
| Query filters | Honored; **no** `.IgnoreQueryFilters()` on this bridge |
| Cross-tenant SectionId | Reject as invalid/not found (prefer safe message; avoid leaking other-tenant data) |
| Cross-tenant TG | Already impossible via entry.TeachingGroupId + filters; still validate explicitly |

---

## 14. Academic-scope validation

Validate each desired Section against TeachingGroup / entry scope using existing Section fields (`AcademicYearId`, `CourseId`, `GroupId`, `SemesterId`) and TeachingGroup denormalized scope:

| Check | |
|---|---|
| Section exists + same tenant | Required |
| Section.AcademicYearId == TeachingGroup.AcademicYearId | Required |
| Section.CourseId == TeachingGroup.CourseId | Required |
| Section.GroupId == TeachingGroup.GroupId | Required |
| Section.SemesterId == TeachingGroup.SemesterId | Required |
| Section.CollegeId | Present on Section but **not** on TeachingGroup — do not invent TG.CollegeId; College is out of TG scope contract |
| Entry ↔ TeachingGroup compatibility | `TeachingGroupRules.EnsureCompatibleWithTimetableEntry` |

Do **not** use SubjectId on Section (Section is academic cohort, not subject-scoped).

Additionally enforce `TeachingGroupRules.ValidateSectionLinks(type, sectionIds)`.

---

## 15. Error contract

| Condition | Result | Persist? |
|---|---|---|
| Timetable not found / wrong tenant | 404 | No |
| Entry not found / not in timetable / wrong tenant | 404 | No |
| `TeachingGroupId` null | 400 actionable (see §12.1) | No |
| TeachingGroup missing | 404 safe | No |
| TG not mutable / archived | 400 | No |
| Timetable not Draft / frozen | 400 (existing lifecycle messages) | No |
| Section invalid / wrong tenant / wrong scope | 400 | No |
| Type rule violation (count) | 400 | No |
| Unauthorized | 401/403 (policy) | No |

Must not expose: stack traces, SQL, DbContext, other-tenant ids, JWT claims, internal class names.

Reuse controller catches: `DomainException` / `InvalidOperationException` → 400; `KeyNotFoundException` → 404.

---

## 16. Authorization contract

| Operation | Policy |
|---|---|
| GET sections | `CanViewSchedulingTimetable` |
| PUT sections | `CanManageSchedulingTimetable` |

Same as Prompt 1 discovery.  
Do not grant via Allocation.* or Operations.View.  
Do not weaken faculty restrictions.

---

## 17. Lifecycle contract

| State | PUT /sections |
|---|---|
| Draft | Allowed (if auth + validations pass) |
| Locked / Published / Archived / IsFrozen | Reject via `EnsureDraft` |

TeachingGroup Locked: section-set mutation rejected (`EnsureCanMutate`), even if timetable is Draft.

---

## 18. Read contract (preview for Prompt 6)

| Reader | After bridge |
|---|---|
| GET `/sections` | May continue reading **TimetableSection** projection |
| AttendanceSessionResolver | Continues reading TimetableSection |
| Readiness / health counts | Continue counting TimetableSection |

Reads must **not** mutate SoT or projection.  
Inconsistent projection: return current projection rows as-is (safe); do not auto-heal on GET.

---

## 19. Explicit non-goals (this workstream)

- UI redesign / TeachingGroup picker  
- Attendance schema or resolver redesign  
- Permanent production backfill migration  
- SubjectAllocation → TeachingGroup inference  
- Automatic TG creation on GET/startup  
- Making TimetableSection the SoT  
- StudentSection / membership changes from `/sections`  

---

## 20. Implementation sequencing (locked)

| Prompt | Deliverable |
|---|---|
| **3** | TeachingGroupSection application boundary (replace/add/remove/list) |
| **4** | TimetableSection projector (idempotent sync) |
| **5** | Retrofit `SetTimetableSectionsAsync` through TG boundary + projector |
| **6** | Read compatibility tests |
| **7** | Disposable explicit conversion (if needed) |
| **8–10** | Guards, E2E, freeze |

Do not merge Prompts 3–6 into one change set.

---

## 21. Acceptance criteria (Prompt 2)

| Criterion | Met? |
|---|---|
| SoT = TeachingGroupSection; projection = TimetableSection | Yes |
| External API contract preserved | Yes |
| No auto TG create / no SA inference | Yes |
| Null TeachingGroupId behavior defined (reject + assign-first) | Yes |
| Empty list / multi-section / idempotency / tenant / errors / auth defined | Yes |
| No permanent backfill designed | Yes |
| No production code changed | Yes |

**STATUS = PASS**
