# AI29.1A — API Specification

## Programs
- `GET /api/programs?includeInactive`
- `GET /api/programs/{id}`
- `POST /api/programs`
- `PUT /api/programs/{id}`
- `POST /api/programs/{id}/archive`
- `DELETE /api/programs/{id}`
- `POST /api/programs/assign-course` `{ courseId, programId? }`
- Dashboard prep: `GET /api/programs/statistics`, `/{id}/summary|student-count|faculty-count|course-count`

## Academic structure (read-only hierarchy)
- `GET /api/academic-structure?includeInactive&includeSections&includeSubjects`
- `GET /api/academic-structure/statistics`
- `GET /api/academic-structure/configuration`
- `PUT /api/academic-structure/configuration` `{ enablePrograms }`

## Course (additive)
- `GET/POST/PUT /api/course` may include optional `programId` without breaking existing clients.
