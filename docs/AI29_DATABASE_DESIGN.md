# AI29 — Database Design

## Tables

### Sections
Operational section master. Scoped by College + AcademicYear + Course + Group + Semester.  
Unique: `(TenantId, AcademicYearId, CourseId, GroupId, SemesterId, SectionCode)` where not deleted.  
`MaximumStrength` is college-configurable (no hardcode).

### StudentSections
Append-only allocation history. `IsCurrent=true` for active row. Transfers close prior row (`EffectiveTo`) and insert new row.

### FacultySectionAssignments
Faculty (Staff) ↔ Section with Role `Primary|Secondary` and effective dates.

### TimetableSections
Maps `TimetableId` (+ optional `TimetableEntryId`) to many `SectionId` values for combined classes without duplicate timetable rows.

### AttendanceSessionSections
Optional join for sessions covering one or more sections (additive; AttendanceSession C/G/S/Subject fields unchanged).

### SectionAllocationPreferences
College strategy for auto-allocation (`Alphabetical`, `GenderBalance`, `Merit`, `Random`, `CapacityBased`).

## Migration policy

Idempotent SQL only (`Apply_AI29_SectionSchema.sql`). No destructive updates. No backfill of existing attendance rows. Optional `General` section via API `ensure-general` when required.
