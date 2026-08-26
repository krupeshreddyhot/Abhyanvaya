# AI-SCHED-TG.3 Prompt 1A — Domain Correction & Final Gate

**Workstream:** AI-SCHED-TG.3  
**Prompt:** 1A — Domain Correction & Final Gate  
**Date:** 2026-08-17  
**Predecessor:** AI-SCHED-TG.3 Prompt 1 (conditionally accepted)

**STATUS: PASS**

---

## 1. Prompt 1 review findings

Prompt 1 delivered an approved TeachingGroup domain shape (entities, enums, pure rules). Architectural review flagged three corrections only:

| # | Finding | Severity |
|---|---|---|
| 1 | `TeachingGroup.SectionGroupId` created a second potential SoT for Sections alongside `TeachingGroupSection` | Source-of-truth ambiguity |
| 2 | Capacity validation treated `MaxTeachingCapacity = 0` like “unset” (`> 0` checks) | Incorrect null/zero semantics |
| 3 | Empty `EnsureMembershipDoesNotClaimStudentSectionMutation()` was not a real domain invariant | Non-enforceable placeholder |

No redesign of the TeachingGroup model was required. EF, migrations, APIs, UI, TimetableEntry, and Attendance remained out of scope.

---

## 2. SectionGroupId investigation

### SectionGroup entity

`Abhyanvaya.Domain/Entities/Academic/SectionGroup.cs` is an **AI29 academic** combined-section aggregate (`CollegeId` + academic hierarchy + `GroupCode` / `GroupName` / status). It owns combined-section membership history for academic operations and maps historically through `TimetableSection` for timetable/attendance combined cases.

### Scheduling usages of SectionGroup

- No references under `Abhyanvaya.Domain/Entities/Scheduling` except a comment on `TeachingGroup` noting SectionGroup is a separate construct.
- No Scheduling application service under `Abhyanvaya.Application/Scheduling` references `SectionGroup`.
- SectionGroup remains registered via AI29 `ISectionGroupService` (academic), not as a TeachingGroup dependency.

### Independent business meaning?

| Concept | Owner | Purpose |
|---|---|---|
| SectionGroup | Academic (AI29) | Named combined-section aggregate for section membership / historical TT bridge |
| TeachingGroup | Scheduling (AI-SCHED-TG) | Operational teaching cohort under SubjectAllocation |
| TeachingGroupSection | Scheduling | Authoritative TG → Section links |

SectionGroup and TeachingGroup are **not** equivalent. CombinedSections for TeachingGroup is fully expressible as multiple `TeachingGroupSection` rows. No documented TeachingGroup capability requires a FK to SectionGroup that cannot be represented by TeachingGroupSection.

---

## 3. Final SectionGroupId decision

**REMOVED** `TeachingGroup.SectionGroupId`.

Canonical (sole authoritative) relationship:

```
TeachingGroup
    ↓
TeachingGroupSection
    ↓
Section
```

For CombinedSections:

```
TeachingGroup
   ├── TeachingGroupSection → Section A
   ├── TeachingGroupSection → Section B
   └── TeachingGroupSection → Section C
```

SectionGroupId is **not** retained. No informational/reference FK was invented. Academic SectionGroup continues to exist independently for AI29; TeachingGroup does not link to it.

---

## 4. Capacity null/zero semantics

| Field | `null` | `0` | Positive | Negative |
|---|---|---|---|---|
| `ExpectedStudentCount` | Not configured (no planning value) | Explicit planning value (allowed, including Draft) | Explicit planning value | **Rejected** |
| `MaxTeachingCapacity` | Not configured | **Rejected** (not “unset”) | Configured ceiling | **Rejected** |

When **both** are configured: `ExpectedStudentCount > MaxTeachingCapacity` → **Rejected**.  
Equal values are valid. Either side alone configured is valid.

**Do not** silently normalize `0` → `null`.

`Room.Capacity` remains a physical room constraint (unchanged). No `PlannedCapacity` field.

`ResolvedStudentCount` remains **derived only** via `TeachingGroup.ComputeResolvedStudentCount` — never a persisted property.

---

## 5. Updated capacity validation

`TeachingGroup.SetCapacity`:

1. Reject `ExpectedStudentCount < 0`.
2. Reject `MaxTeachingCapacity` when set and `<= 0` (covers `0` and negatives).
3. When both non-null, reject if `Expected > Max`.
4. Assign values as supplied (including `Expected = 0`).

`EnsureResolvedWithinMaxCapacity` still enforces only when Max is configured (non-null positive after SetCapacity).

---

## 6. Removal of empty architecture assertion

Removed `TeachingGroupRules.EnsureMembershipDoesNotClaimStudentSectionMutation()`.

Architectural rule retained in documentation and deferred enforcement:

> TeachingGroup membership operations **MUST NOT** mutate `StudentSection`.

Enforcement belongs to application/service boundaries and Architecture Guard tests in later prompts. No DbContext access was added to domain entities/rules.

---

## 7. Tests added/changed

`TeachingGroupDomainTests` expanded to cover mandatory Prompt 1A cases (section links, SectionGroup removal proof, capacity matrix, derived count, exclusion, lifecycle, tenant).

| Suite | Failed | Passed | Skipped | Total |
|---|---:|---:|---:|---:|
| `TeachingGroupDomainTests` | 0 | 33 | 0 | 33 |
| Scheduling filter (excl. Phase2B6/2B7/3/35) | 0 | 162 | 0 | 162 |
| ArchitectureGuard + SubjectAllocation + SchedulingFoundation + TimetableEntryMapping | 0 | 34 | 0 | 34 |

No tests weakened, deleted, or converted to skips.

---

## 8. Regression results

All listed runs: **0 failed, 0 skipped**. Domain corrections did not break existing Scheduling or Architecture Guard suites exercised above.

---

## 9. Files changed

| File | Change |
|---|---|
| `Abhyanvaya.Domain/Entities/Scheduling/TeachingGroup.cs` | Removed `SectionGroupId`; corrected `SetCapacity` null/zero semantics; clarified Section SoT comment |
| `Abhyanvaya.Domain/Entities/Scheduling/TeachingGroupRules.cs` | Removed empty StudentSection assertion method |
| `Abhyanvaya.Application.UnitTests/Scheduling/TeachingGroupDomainTests.cs` | Expanded mandatory domain tests |
| `docs/AI_SCHED_TG_3_PROMPT_1A_DOMAIN_CORRECTION.md` | This report |

Unchanged (preserved): TeachingGroupSection, TeachingGroupMembership, all TG enums, ExclusionGroupKey behavior, lifecycle transitions, tenant helpers, ResolvedStudentCount derivation.

---

## 10. Explicit boundary confirmation

| Boundary | Status |
|---|---|
| No EF Core mapping changes | Confirmed |
| No migration created/applied | Confirmed |
| No database changes | Confirmed |
| No API changes | Confirmed |
| No UI changes | Confirmed |
| No TimetableEntry / TimetableSection changes | Confirmed |
| No Attendance changes | Confirmed |
| No SectionGroup entity modification | Confirmed |
| No StudentSection modification | Confirmed |

---

## Final architectural gate checklist

| # | Criterion | Met |
|---|---|---|
| 1 | SectionGroupId definitively resolved | Yes — **removed** |
| 2 | TeachingGroupSection sole authoritative TG → Section relationship | Yes |
| 3 | MaxTeachingCapacity = 0 rejected | Yes |
| 4 | ExpectedStudentCount = 0 explicit meaning documented | Yes — explicit planning value |
| 5 | Expected > Max rejected when both configured | Yes |
| 6 | ResolvedStudentCount remains derived | Yes |
| 7 | Empty StudentSection assertion method removed | Yes |
| 8 | TeachingGroup domain tests pass | Yes (33/0/0) |
| 9 | Relevant scheduling regressions pass | Yes |
| 10 | No EF/DB/API/UI/timetable/attendance changes | Yes |

---

## Chief Architect handoff

**STATUS = PASS**

AI-SCHED-TG.3 Prompt 1A is approved for EF Core configuration and migration work.

Do **not** auto-start Prompt 2 from this document; proceed only under an explicit Prompt 2 instruction.
