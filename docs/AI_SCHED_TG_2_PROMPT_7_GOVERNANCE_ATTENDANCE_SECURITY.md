# AI-SCHED-TG.2 Prompt 7 — Governance, Lifecycle, Attendance & Security Contract

**Workstream:** AI-SCHED-TG.2  
**Prompt:** 7 — Governance / attendance / security (design only)  
**Date:** 2026-08-17  

**No implementation. Do not weaken RBAC.**

---

## Lifecycle (aligned to existing Timetable / ScheduleVersion)

TeachingGroup uses its own compact status, coordinated with timetable:

| TeachingGroupStatus | Meaning |
|---|---|
| Draft | Definition editable |
| Active | Eligible for draft/review timetable placement |
| Locked | Membership frozen (referenced by Published/Locked timetable) |
| Archived | Soft-retired |

Mapping to prompt narrative Draft→Ready→Scheduled→Published→Locked→Archived:

| Narrative | Actual |
|---|---|
| Draft / Ready | Draft → Active |
| Scheduled | Active + referenced by Draft/UnderReview timetable |
| Published | Referenced by Published timetable → TG Locked |
| Locked | Locked (timetable Locked/Frozen) |
| Archived | Archived |

---

## Operations by state

| Operation | Draft | Active | Locked | Archived |
|---|---|---|---|---|
| Edit name/capacity/notes | Yes | Yes* | No | No |
| Change membership | Yes | Yes* | **No** | No |
| Attach to Draft timetable | Yes | Yes | Yes (read-only cohort) | No |
| Attach to Published timetable | No (must Active then publish flow) | Via publish | Already | No |
| Archive | Yes | Yes | After TT unlock/archive policy | — |

\*If any referencing Timetable is Published/Locked → treat as Locked.

### Specific answers

| Question | Answer |
|---|---|
| Membership after timetable creation (Draft)? | Yes |
| After approval (ScheduleVersion Approved)? | Yes until Published |
| After publishing? | **No** without Unlock + new version |
| Membership change invalidate version? | If Published: require new ScheduleVersion / re-approval per existing governance |
| TG change require TT regeneration? | No automatic wipe; conflict re-validation recommended |
| SubjectAllocation change? | Restrict breaking changes; if allocation scope changes, TG must be updated or archived |
| Section membership change (StudentSection)? | SectionDerived/Combined **auto-reflect** until Locked; after Lock use frozen snapshot if taken |

---

## Audit & governance events

Record via existing change-history patterns / domain events:

- TeachingGroupCreated / Updated / Archived
- TeachingGroupMembershipChanged
- TeachingGroupLocked (on publish)
- TimetableEntryTeachingGroupAssigned

Do not bypass TimetableApprovalRequest / Publish permissions.

---

## Security / RBAC

| Action | Permission |
|---|---|
| View TG | `Scheduling.TeachingGroup.View` (or Timetable.View interim) |
| Manage TG | `Scheduling.TeachingGroup.Manage` |
| Publish/Approve TT | existing Scheduling.Publish / Approve |
| Attendance resolve | `CanManageAttendance` (unchanged) |

No new bypass of tenant filters.

---

## Attendance contract

### Timetable mode (extended)

```text
Resolve entry (existing)
→ read TeachingGroupId
→ MembershipResolver.GetStudents(teachingGroup)
→ return StudentIds (+ SectionIds from TeachingGroupSection for UI)
→ Course/Group/Semester/Subject/Period unchanged from entry
```

### Legacy mode

Unchanged when no published timetable / no staff / no entry.

### When TeachingGroup unused

Pre-cutover: TimetableSection path remains.  
Post-cutover: entries always have TeachingGroupId.

### Guarantees

- No attendance schema break
- No forced timetable
- Faculty override retained
- Empty TG → resolver returns empty student list + warning; does not invent Section

---

## Confirmation

**No production changes.**
