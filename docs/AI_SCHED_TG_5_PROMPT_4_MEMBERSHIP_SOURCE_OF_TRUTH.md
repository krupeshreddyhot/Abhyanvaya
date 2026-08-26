# AI-SCHED-TG.5 Prompt 4 — Membership Source of Truth

**Date:** 2026-08-19  
**Status:** APPROVED for subsequent implementation prompts  

---

## Source-of-truth matrix

| Information | Source of Truth | Not a source of truth |
|---|---|---|
| Teaching Group identity | `TeachingGroup` | SubjectAllocation uniqueness, Room, TimetableEntry inference |
| Teaching Group ↔ Section links | `TeachingGroupSection` | `TimetableSection` |
| Explicit Include / Exclude overlays | `TeachingGroupMembership` | StudentSection, TimetableSection, Attendance |
| Academic section membership | `StudentSection` | TeachingGroupMembership |
| Subject enrollment (electives) | `StudentSubject` | TeachingGroupMembership |
| **Derived / resolved membership** | **Membership Resolver** (application) reading approved sources per `MembershipSource` | Persisted `ResolvedStudentCount`, TimetableSection, UI caches |
| Timetable section projection | `TimetableSection` (via `TimetableSectionProjector`) | TeachingGroupSection reads must not reverse this |
| Timetable TG assignment | `TimetableEntry.TeachingGroupId` | — |
| Planning intent capacity | `TeachingGroup.ExpectedStudentCount` | Resolved headcount |
| Teaching ceiling | `TeachingGroup.MaxTeachingCapacity` | Room.Capacity |
| Physical room size | `Room.Capacity` | TG membership |

---

## Critical prohibitions

1. **`TimetableSection` must never become the source of Teaching Group membership.**  
2. **Managing Teaching Group membership must not silently insert/update/delete `StudentSection`.**  
3. **Membership mutation must not mutate `TimetableEntry`, `TimetableSection`, or Attendance.**  
4. **`ResolvedStudentCount` is never persisted as an authoritative column.**  
5. **GET operations must not mutate membership or create Teaching Groups.**

---

## Ownership diagram

```text
SubjectAllocation
       │
       ├── TeachingGroup A/B/C
              ├── TeachingGroupMembership  (explicit Include/Exclude SoT)
              └── TeachingGroupSection     (section-link SoT)
                        │
                        ▼
               TimetableSectionProjector
                        │
                        ▼
                 TimetableSection          (projection only)

StudentSection  ──► feeds derived resolution (read-only for TG)
StudentSubject  ──► feeds derived resolution (read-only for TG)
```
