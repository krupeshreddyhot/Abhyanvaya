# AI29.1A — Program Management

## Entity

`Program`: ProgramCode, ProgramName, Description, DisplayOrder, IsActive, Status (Active|Archived), CollegeId, tenant + audit + soft delete.

## Rules

- Course has at most one `ProgramId` (nullable FK).
- Cannot delete Program while Courses are linked.
- Archived Programs cannot receive new Courses.
- When `EnablePrograms=false`, Course.ProgramId is forced null on create/assign.

## UI

Catalog → **Programs**: list, create, edit, view, archive, delete; EnablePrograms switch.

## Permissions

`Program.View|Create|Edit|Delete|Manage`
