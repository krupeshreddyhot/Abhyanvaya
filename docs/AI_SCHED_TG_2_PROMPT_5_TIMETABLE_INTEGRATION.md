# AI-SCHED-TG.2 Prompt 5 — Timetable Integration Contract

**Workstream:** AI-SCHED-TG.2  
**Prompt:** 5 — Timetable integration (design only)  
**Date:** 2026-08-17  

**No code/database changes.**

---

## Target model

```text
SubjectAllocation
      ↓
TeachingGroup          ← authoritative operational cohort
      ↓
TimetableEntry         ← TeachingGroupId required (clean production)
      ↓
TimeSlot + Room + Faculty (Staff)
```

Existing modules **reused, not rebuilt:** SubjectAllocation, Governance, Approval, Publishing, Conflict detection, Optimization, Timetable Designer shell.

---

## TimetableEntry fields

| Field | Decision |
|---|---|
| `TeachingGroupId` | **Required** in production model |
| `SubjectAllocationId` | Remains; must equal `TeachingGroup.SubjectAllocationId` |
| Denormalized Course/Group/Semester/Subject/Staff | Remain; Staff/Room may still be editable with validation vs allocation |
| Infer cohort from room/name/section code | **Forbidden** |

### TimetableSection

- Remains for compatibility APIs and current AttendanceSessionResolver section enrichment.
- On TimetableEntry create/update: sync TeachingGroupSection → TimetableSection rows for that entry.
- Designer primary UX selects **Teaching Group**, not raw section multi-select (section multi-select may still create/update CombinedSections TG).

---

## Lifecycle operations

Uses existing `TimetableStatus`: Draft, Locked, Published, Archived  
Uses existing `ScheduleVersionStatus`: Draft, UnderReview, Approved, Published, Archived

| Operation | TeachingGroup rule |
|---|---|
| Create entry (Draft TT) | TG must be Draft or Active; same tenant/scope |
| Edit entry | Allowed in Draft; TG membership editable if TG not Locked |
| Validation | Soft + conflict: Room.Capacity vs TG.PlannedCapacity (prefer over Subject.ExpectedCapacity when TG present) |
| Approval / Publish | Existing governance; optionally require TG Active and non-empty members when AttendanceMandatory |
| Lock / Freeze | Existing; locks TG membership |
| Clone | Clone entries with TeachingGroupId; clone or share TG by policy (default: **reuse** Active TGs, do not duplicate) |
| Versioning | New ScheduleVersion copies entries; TG identity preserved |
| Archive | Entries retain TeachingGroupId |

---

## Room capacity

| Rule | |
|---|---|
| Constraint level | Scheduling validation / conflicts |
| Compare | `Room.Capacity` (effective with margin) vs `TeachingGroup.PlannedCapacity` (fallback Subject.ExpectedCapacity) |
| 30 students / room 40 | One TeachingGroup |
| 70 students / teaching capacity 40 | Two TeachingGroups — **never** two Sections |

---

## Creation / editing UX contract

1. Select SubjectAllocation (existing).
2. Select or create TeachingGroup for that allocation.
3. Place Day + TimeSlot + Room.
4. System validates allocation match, tenant, capacity, conflicts.
5. Optionally show derived SectionIds for SectionDerived/Combined.

---

## Confirmation

**No production changes.**
