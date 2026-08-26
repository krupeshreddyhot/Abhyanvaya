# AI-SCHED-CAP Prompt 5 — Publish Readiness Architecture & Level-3 Gate Contract

**Workstream:** AI-SCHED-CAP  
**Prompt:** 5 — Publish Readiness Architecture & Gate Contract  
**Date:** 2026-08-20  
**Type:** **CONTRACT / ARCHITECTURE DESIGN ONLY** — no production publish-gate behavior  
**Status:** **PASS** (contract locked for a future implementation prompt)

**Frozen:** AI-SCHED-TG.3 → TG.6; CAP Prompt 1–4 (PlacementSize, RoomCapacityEvaluator, presentation, Draft soft).  
**Baseline discovery:** CAP Prompt 1; capacity/conflict policy: CAP Prompt 2 §5–§8.

---

## 1. Executive decision summary

| Decision | Contract |
| --- | --- |
| Scope of this prompt | **Define** Publish Readiness + Level-3 gate **contract only** |
| Implementation of gate | **Deferred** — do not change `PublishAsync` in this prompt |
| Architectural home | New read-only application service (not SoftValidation; not Configuration Readiness; not Optimization Readiness) |
| Findings source | **Reuse** `ConflictEngine` (+ shared PlacementSize / RoomCapacity / TG capacity / presentation composer) — **no second conflict engine** |
| Draft mutations | Remain Level 1 soft / detect-only (unchanged) |
| Level-3 blockers | All **Critical** integrity findings **plus** rule codes `ROOM_CAPACITY` and `TEACHING_GROUP_CAPACITY_EXCEEDED` (even though those rules emit `Error` today) |
| Non-blocking | Warning / Information findings; Error findings that are **not** capacity rule codes listed above (unless later contract expands the blocker set) |
| UI | Consumes server readiness DTO; never recalculates PlacementSize / EffectiveRoomCapacity / TG capacity |

---

## 2. Current-state analysis

### 2.1 Timetable lifecycle / status

| Status | Value | Editability | Notes |
| --- | --- | --- | --- |
| Draft | 1 | Entry mutations via EnsureDraft | SoftValidation + ConflictEngine detect-only |
| Locked | 2 | Not Draft-editable | Typical pre-publish lock |
| Published | 3 | Not Draft-editable | Live schedule; attendance may resolve Published/Locked |
| Archived | 4 | Terminal | |

Orthogonal: `Timetable.IsFrozen` (freeze/unfreeze does **not** change `Status`).

**Today’s `PublishAsync` gates (lifecycle only — not conflict/capacity):**

1. Frozen → reject (“cannot be republished until unlocked”)
2. Status must be Locked **or** linked ScheduleVersion Approved
3. At most one non-frozen Published timetable per Tenant + AcademicYear + Department

**Not checked today:** ConflictEngine, SoftValidation, ROOM_CAPACITY, TEACHING_GROUP_CAPACITY_EXCEEDED, PlacementSize.

### 2.2 Existing readiness surfaces (do not overload)

| Service | Purpose | Publish Gate? |
| --- | --- | --- |
| `ISchedulingConfigurationReadinessService` | Module setup progress | **No** |
| `ISchedulingSetupValidator` | Advisory missing-config | **No** (never blocks) |
| `IOptimizationReadinessService` | Optimization preview | **No** |
| SoftValidation | Draft designer warnings | **No** |
| ConflictEngine | Detect-only analysis | **Input to** future Publish Readiness |

### 2.3 Capacity / conflict stack (authoritative — reuse)

```text
IPlacementSizeResolver
IRoomCapacityEvaluator
ITeachingGroupMembershipResolver (ResolvedStudentCount)
TeachingGroupCapacityExceededRule / SoftValidation TG check
ISchedulingConflictPresentationComposer
ConflictEngine / ConflictAnalyzer
```

PlacementSize precedence (CAP Prompt 2/3 — locked):

```text
ResolvedStudentCount (incl. 0)
  → ExpectedStudentCount (> 0)
  → Subject.ExpectedCapacity (> 0)
  → Unset
```

Room: `PlacementSize > EffectiveRoomCapacity` via `IRoomCapacityEvaluator`.  
TG: `ResolvedStudentCount > MaxTeachingCapacity` when Max is positive.

### 2.4 Severity today (engine)

| Finding | Typical severity | Draft edit | Publish today |
| --- | --- | --- | --- |
| Faculty/Room/Student double-booking (etc.) | Critical | Soft | Not gated |
| ROOM_CAPACITY | Error | Soft | Not gated |
| TEACHING_GROUP_CAPACITY_EXCEEDED | Error | Soft | Not gated |
| Preference / non-working day / lab recommended | Warning / soft codes | Soft | Not gated |

`ConflictSummary.BlocksEditing => false` remains.

### 2.5 Auth

Publish mutation: policy `CanPublishScheduling` / permission `Scheduling.Publish`.  
Future readiness evaluate endpoint: prefer same publish permission **or** timetable view + publish (implementation prompt chooses; must remain server-authoritative and tenant-scoped).

---

## 3. Where Publish Readiness belongs

**Decision:** Introduce a dedicated application abstraction:

```text
ITimetablePublishReadinessService
  EvaluatePublishReadinessAsync(int timetableId, CancellationToken) → TimetablePublishReadinessDto
```

| Belong | Reason |
| --- | --- |
| Application Scheduling layer | Same home as lifecycle / ConflictAnalyzer |
| Read-only | No SaveChanges; no status mutation |
| Separate from SoftValidation | SoftValidation is Draft UX dismissible warnings |
| Separate from Configuration Readiness | Setup modules ≠ conflict publish policy |
| Consumes ConflictEngine | Single conflict subsystem (CAP Prompt 2) |

**Conceptual API (future):** `GET api/scheduling/timetables/{id}/publish-readiness`  
(Exact route/verb finalized in implementation prompt.)

**Not in this prompt:** DI registration, controller, DTO classes in production, or `PublishAsync` wiring.

---

## 4. Publish Readiness response model (contract)

Conceptual DTO (names may match project conventions when implemented):

```text
TimetablePublishReadinessDto
{
  timetableId
  tenantId                          // optional in API payload; always enforced server-side
  currentLifecycleStatus            // Draft | Locked | Published | Archived
  isFrozen
  canPublish                        // true iff blockers empty AND lifecycle preconditions satisfied
  lifecycleEligible                 // Locked or approved ScheduleVersion; not Frozen; uniqueness OK
  blockers[]                        // Level-3 blocking findings (+ lifecycle blockers as findings optional)
  warnings[]
  informationalFindings[]
  summary
  {
    blockerCount
    warningCount
    informationalCount
    criticalIntegrityCount
    roomCapacityBlockerCount
    teachingGroupCapacityBlockerCount
    evaluatedAtUtc
  }
}
```

**Finding item (conceptual):**

```text
PublishReadinessFindingDto
{
  code                              // e.g. ROOM_CAPACITY, ROOM_DOUBLE_BOOKING, LIFECYCLE_NOT_LOCKED
  severity                          // Critical | Error | Warning | Information
  blocking                          // bool — authoritative server flag
  category                          // optional ConflictCategory / Lifecycle
  title / message / why / suggestedAction   // reuse presentation composer where applicable
  timetableEntryId?
  dayOfWeek? / timeSlotId?
  roomId?
  teachingGroupId?
  teachingGroupCode? / teachingGroupName? / teachingGroupStatus?
  placementSize? / placementSizeSource?
  roomCapacity? / capacityMarginPercent? / effectiveRoomCapacity?
  resolvedStudentCount? / maxTeachingCapacity?
}
```

Do **not** expose EF entities, SQL, or stack traces.

---

## 5. Blocking classification (Level 3)

### A. BLOCKING (`blocking = true`, contributes to `canPublish = false`)

1. **Critical scheduling integrity conflicts** from ConflictEngine  
   (any finding with `ConflictSeverity.Critical` — e.g. room/faculty/student double-booking, selected calendar criticals).

2. **`ROOM_CAPACITY`** — regardless of engine severity (`Error` today).

3. **`TEACHING_GROUP_CAPACITY_EXCEEDED`** — regardless of engine severity (`Error` today).

4. **Lifecycle preconditions** (already enforced by `PublishAsync`; readiness must surface them as blockers when evaluating publishability):
   - Frozen
   - Not Locked and ScheduleVersion not Approved
   - Another Published non-frozen timetable in same AY + Department scope

### B. WARNINGS (`blocking = false`)

- ConflictEngine `Warning` findings  
- SoftValidation-style advisories if optionally mirrored (PREFERRED_ROOM_MISSING, LAB_RECOMMENDED, NON_WORKING_DAY, FACULTY_UNAVAILABLE, …)  
- ConflictEngine `Error` findings **other than** the two capacity codes above — **remain non-blocking for publish unless a future contract expands the set**

### C. INFORMATIONAL

- ConflictEngine `Information`  
- Diagnostic notes (e.g. PlacementSize unset → room capacity not evaluable — **not** a capacity blocker)

**Rule:** Do **not** classify every SoftValidation Warning as a publish blocker.

---

## 6. Lifecycle awareness

| State | Readiness evaluate | canPublish |
| --- | --- | --- |
| Draft | Allowed (preview) | Typically **false** due to lifecycle (not Locked / not approved version) **even if** no conflict blockers |
| Locked | Allowed | true only if no Level-3 conflict/capacity blockers and uniqueness OK |
| Published | Allowed (re-evaluate) | Republish only if not Frozen and uniqueness rules allow; conflict blockers still apply |
| Archived | Allowed (report only) | **false** (archived is not publishable) |
| Frozen (any status) | Allowed | **false** until unlock-frozen |

**Republish:** Existing lifecycle allows republish when not Frozen and Locked/approved-version rules pass. Future gate adds conflict/capacity blockers on top — does not remove freeze/uniqueness rules.

**Evaluation scope:** Current timetable entries + masters for **this tenant + this timetableId** only (not historical versions unless a later contract says otherwise).

---

## 7. Teaching Group / legacy / projection behavior

| Scenario | Publish Readiness contract |
| --- | --- |
| `TeachingGroupId = null` | Valid legacy. No TG inference/create. No TG capacity blocker. PlacementSize falls through to Subject.ExpectedCapacity; ROOM_CAPACITY may still block |
| Assigned Active/Draft/Locked TG | Evaluate TG capacity + room PlacementSize using assigned TG only |
| Assigned Archived TG | **Do not silently clear/replace.** Still evaluate capacity if Resolved/Max available. Presentation may label Archived. New assign remains forbidden by TG.4/6 attach rules (separate from readiness) |
| Multiple TGs on SubjectAllocation | Use **assigned** TG only — never pick another TG for capacity |
| Orphaned / stale TimetableSection | Readiness does **not** rewrite TimetableSection. Optional future informational finding if projection audit exists; **out of Prompt 5 MVP blockers**. `TimetableSectionProjector` remains sole writer |
| Clone/version | Do not write TimetableSection; readiness not auto-run in this contract (optional later) |

---

## 8. Capacity representation

Publish readiness **must** use:

- `IPlacementSizeResolver` (not a private copy)
- `IRoomCapacityEvaluator` (margin-aware)
- Existing TG capacity semantics (`Resolved > Max` when Max > 0)
- Presentation fields via `ISchedulingConflictPresentationComposer` where useful

Same timetable snapshot ⇒ same PlacementSize / EffectiveRoomCapacity / TG capacity outcomes as SoftValidation + ConflictEngine.

---

## 9. Aggregation & deterministic ordering

Collect **all** applicable findings (do not stop at first).  
Do not collapse ROOM_CAPACITY + TEACHING_GROUP_CAPACITY into one `CAPACITY_ERROR`.

**Order (stable):**

1. `blocking` desc (blockers first)  
2. Severity rank (Critical > Error > Warning > Information)  
3. Rule `code` (ordinal)  
4. `timetableEntryId` (nulls last)  
5. `dayOfWeek`, `timeSlotId`  
6. `roomId`, `teachingGroupId`  
7. Deterministic secondary key (e.g. code + entry + slot hash)

Do **not** rely on database row order.

---

## 10. Security model

- Tenant isolation via current-user tenant on all loads (no casual `IgnoreQueryFilters`)
- No cross-tenant TeachingGroup / Room / Timetable lookup
- Authorization: server policies (`CanPublishScheduling` / timetable view as chosen in implementation)
- No client-side authorization or client recalculation of readiness metrics
- Read-only evaluate: **zero** database mutations

---

## 11. Future publish integration boundary (deferred implementation)

```text
Publish request
      │
      ▼
ITimetablePublishReadinessService.EvaluatePublishReadinessAsync(timetableId)
      │
      ├─ canPublish == false  →  reject (DomainException / 409/400) with blockers payload
      │
      └─ canPublish == true   →  existing TimetableLifecycleService.PublishAsync transition
```

**This prompt does not wire the gate.** Implementation belongs to a subsequent CAP prompt after contract acceptance.

Draft Create/Move/Copy/Paste/Duplicate/DnD remain soft.

---

## 12. Explicit deferred items

| Item | Status |
| --- | --- |
| Implement `ITimetablePublishReadinessService` | Deferred |
| API `…/publish-readiness` | Deferred |
| Wire gate into `PublishAsync` | Deferred |
| UI publish button readiness panel | Deferred |
| Schema / migrations | Not required for this contract |
| Hard Draft mutation rejection for double-booking | Deferred (optional later CAP) |
| Expand Error→blocker beyond capacity codes | Deferred unless reopened |
| Automatic room/TG selection | Forbidden |
| Attendance / StudentSection / TimetableSection architecture changes | Forbidden |

---

## 13. Architecture home diagram

```text
Timetable (tenant-scoped)
        │
        ▼
ITimetablePublishReadinessService (read-only)     ← NEW (future)
        │
        ├── ConflictAnalyzer / ConflictEngine      ← REUSE
        ├── IPlacementSizeResolver                 ← REUSE
        ├── IRoomCapacityEvaluator                 ← REUSE
        ├── ITeachingGroupMembershipResolver       ← REUSE
        ├── ISchedulingConflictPresentationComposer← REUSE
        └── Lifecycle precondition checks          ← ALIGN with PublishAsync
                │
                ▼
        TimetablePublishReadinessDto
                │
                ▼
        (future) PublishAsync gate
```

---

## 14. Completion checklist (Prompt 5)

- [x] Contract document created  
- [x] Blocking vs warning vs informational defined  
- [x] Lifecycle / republish / Frozen / Archived TG / legacy null TG documented  
- [x] Deterministic ordering defined  
- [x] Reuse of PlacementSize / RoomCapacity / ConflictEngine mandated  
- [x] No publish gate implementation in this prompt  
- [x] Architecture/contract tests added  
- [x] Draft soft behavior preserved  

**STATUS: PASS (contract only)**
