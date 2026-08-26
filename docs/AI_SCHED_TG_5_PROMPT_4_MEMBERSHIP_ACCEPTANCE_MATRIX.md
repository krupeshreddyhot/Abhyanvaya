# AI-SCHED-TG.5 Prompt 4 — Membership Acceptance Matrix

**Date:** 2026-08-19  
**Type:** Contract-level test specification (implementation later)  

Legend: **Must pass** when membership mutation + resolver are implemented.

---

## A. Explicit

| # | Scenario | Expected |
|---|---|---|
| E1 | Add eligible student | Include row current; Resolved +1 |
| E2 | Remove student | Include ended; Resolved −1 |
| E3 | Duplicate add | Idempotent 200; still one current row |
| E4 | Wrong tenant student | 400/404; no row |
| E5 | Wrong academic scope | 400 |
| E6 | Ineligible (subject/course mismatch) | 400 |
| E7 | Add exceeding MaxTeachingCapacity | 400 |
| E8 | Exclude row on Explicit source | 400 rejected |
| E9 | Replace set | Exact Include set; Resolved matches |

## B. Section-derived / Combined

| # | Scenario | Expected |
|---|---|---|
| S1 | Add section → Resolved grows | Live from StudentSection |
| S2 | Remove section → derived leave | Live |
| S3 | Student joins section | Appears in resolved |
| S4 | Student leaves section | Leaves resolved |
| S5 | Duplicate across two combined sections | Distinct count once |
| S6 | POST memberships on Section source | 400 not supported |
| S7 | StudentSection unchanged after TG ops | Assert no StudentSection writes |

## C. Hybrid (Model B)

| # | Scenario | Expected |
|---|---|---|
| H1 | Base-only student | In resolved; provenance Derived |
| H2 | Explicit addition outside base | In resolved; ExplicitInclude |
| H3 | Explicit Exclude of base student | Not in resolved |
| H4 | Section change updates Base | Resolved follows Base∪Inc−Exc |
| H5 | Remove Include that was only explicit | Leaves resolved |
| H6 | Capacity after hybrid add | Enforce Max |

## D. StudentSubject source

| # | Scenario | Expected |
|---|---|---|
| SS1 | Enrolled subject students resolve | Distinct eligible set |
| SS2 | Full scope required | StudentId+SubjectId alone insufficient |
| SS3 | Membership POST | 400 not supported (v1) |

## E. Multiplicity / exclusion

| # | Scenario | Expected |
|---|---|---|
| M1 | One SA → many TGs | Allowed |
| M2 | Same student Lecture + Lab (no shared exclusion key) | Allowed |
| M3 | Same student two CapacitySplit with same ExclusionGroupKey | Rejected |
| M4 | Global SA uniqueness | Must **not** be enforced |

## F. Concurrency

| # | Scenario | Expected |
|---|---|---|
| C1 | Parallel Replace conflicting | 409 or serialized success without duplicate current rows |
| C2 | Unique current index held | Always ≤1 current per TG+Student |

## G. Security

| # | Scenario | Expected |
|---|---|---|
| SEC1 | No View | 401/403 on GET |
| SEC2 | No Manage | 401/403 on mutations |
| SEC3 | Cross-tenant TG id | 404 |
| SEC4 | IgnoreQueryFilters | Forbidden in service |

## H. Boundaries

| # | Scenario | Expected |
|---|---|---|
| B1 | No Attendance mutation | Architecture guard |
| B2 | No StudentSection mutation | Architecture guard |
| B3 | No TimetableSection mutation | Architecture guard |
| B4 | GET does not mutate membership | Guard |
| B5 | GET does not create TeachingGroup | Guard |
| B6 | SubjectAllocation does not auto-create TG | Guard |

## I. Capacity / room

| # | Scenario | Expected |
|---|---|---|
| CAP1 | Resolved > Max | Membership error |
| CAP2 | Resolved > Room | Membership OK; schedule/publish per TG.2A |
| CAP3 | Expected/Max unchanged by membership | Assert |

## J. UI contract (future)

| # | Scenario | Expected |
|---|---|---|
| U1 | Dynamic source — no add/remove controls | UX |
| U2 | Explicit/Hybrid — add/remove allowed with Manage | UX |
| U3 | Membership mutation absent until implemented | Prompt 3 remains read-only until then |
