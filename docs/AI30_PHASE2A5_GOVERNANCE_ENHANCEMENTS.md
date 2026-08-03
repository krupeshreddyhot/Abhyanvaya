# AI30 Phase 2A.5 — Enterprise Governance Enhancements

## Scope

Four governance capabilities on top of Phase 2A (no Timetable Designer / aggregate / versioning / approval engine redesign):

1. **Version Comparison** — Draft vs previous / Published vs Draft entry diffs  
2. **Approval Comments & Decision History** — structured comments + old/new status timeline  
3. **Freeze / Unlock** — post-publish freeze with Academic Admin unlock  
4. **Archive Reasons & Lifecycle Metadata** — reason lookup, comments, dashboard stats  

Out of scope: Phase 2B conflict detection, optimizer, AI scheduling, email/notifications.

## Capabilities

### Version comparison

- Service: `VersionComparisonService`
- API: `POST /api/scheduling/versions/compare`, `POST .../compare/export`
- Categories: Added / Removed / Modified; Faculty, Room, Subject, Period, TimeSlot
- UI: Governance → Schedule versions → Compare versions
- Permissions: `Scheduling.VersionCompare.View`, `Scheduling.VersionCompare.Export`

### Approval comments & decision history

- Entities: `TimetableApprovalComment`, `TimetableDecisionHistory`
- Extended history with `OldStatus` / `NewStatus`
- Comment required on Reject / Return
- UI: Approval queue timeline + decision history panel
- Permissions: `Scheduling.ApprovalComments.View`, `Scheduling.ApprovalComments.Manage`

### Freeze / Unlock

- Fields on `Timetable`: `IsFrozen`, `FrozenDate`, `FrozenBy`, `FreezeReason`, `UnlockDate`, `UnlockedBy`, `UnlockReason`
- Workflow: Published → Frozen → Unlocked → Published
- Designer opens read-only when frozen
- Permissions: `Scheduling.Freeze`, `Scheduling.Unlock`

### Archive reasons

- Lookup table `SchedulingArchiveReason` (seeded Superseded, Semester Complete, Correction, Emergency, Academic Council, Other)
- Metadata on Timetable / ScheduleVersion archive
- Dashboard: reason distribution + latest archives
- Permissions: `Scheduling.Archive.View`, `Scheduling.Archive.Manage` (existing `Scheduling.Archive` retained)

## Migration

`20260801185321_AI30_Phase2A5_GovernanceEnhancements`
