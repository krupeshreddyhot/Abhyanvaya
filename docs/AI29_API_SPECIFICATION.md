# AI29 — API Specification

## Sections
- `GET /api/sections?academicYearId&courseId&groupId&semesterId`
- `GET /api/sections/{id}`
- `POST /api/sections` — CreateSectionRequest
- `PUT /api/sections/{id}` — UpdateSectionRequest
- `DELETE /api/sections/{id}` — soft delete
- `POST /api/sections/ensure-general`
- `POST /api/sections/auto-allocate`
- `GET /api/sections/statistics`
- `GET /api/sections/reports/{kind}` — students-by-section | faculty-by-section | section-capacity | section-transfers
- `GET /api/sections/dashboard/sections|faculty/{id}|students/{id}|combined-sessions`

## Student allocation
- `GET /api/student-sections?sectionId&studentId&currentOnly`
- `POST /api/student-sections`
- `POST /api/student-sections/transfer`

## Faculty allocation
- `GET /api/faculty-sections?sectionId&facultyId&currentOnly`
- `POST /api/faculty-sections`

## Timetable sections
- `GET /api/timetable/{timetableId}/sections`
- `PUT /api/timetable/{timetableId}/sections` — `{ timetableEntryId?, sectionIds: number[] }`

## Attendance (additive, backward compatible)
- `GET /api/attendance/students-for-marking?...&sectionId=&sectionIds=`
  - When omitted: identical to pre-AI29 (all students for Course/Group/Semester).

## Resolution (additive)
- `GET /api/attendance-resolution/current` — Timetable mode may include `sectionIds` / `sectionCodes`.
