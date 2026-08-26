# AI29.1D.24 Prompt 4B.4 — ProgramId API Contract

## Endpoints (unchanged)

| Method | Path | Auth |
|--------|------|------|
| `GET` | `/api/course` | `CanManageCourses` |
| `POST` | `/api/course` | `CanManageCourses` (+ `CanAssignCourseToProgram` when assigning a positive Program) |
| `PUT` | `/api/course` | `CanManageCourses` (+ `CanAssignCourseToProgram` when `programId` is present, including null) |

**No** second Program-assignment endpoint was added. Authoritative command remains `AssignCourseToProgramAsync` (also exposed as `POST /api/programs/assign-course` for non–Course-Master clients). Course Master UI uses only `POST/PUT /api/course`.

**Database:** no schema change.

---

## Request contract — `programId`

Presence is detected via DTO setters (`ProgramIdSpecified`) so JSON `null` is distinct from an omitted property.

### `PUT /api/course` (Update)

| JSON | Meaning |
|------|---------|
| `"programId": 15` | Assign Program **15** (`Course.ProgramId = 15`) |
| `"programId": null` | **Explicitly remove** Program relationship |
| *(property omitted)* | **Do not modify** existing `Course.ProgramId` (backward compatible for legacy clients that only send `id` / `code` / `name`) |

### `POST /api/course` (Create)

| JSON | Meaning when `EnablePrograms = true` |
|------|--------------------------------------|
| `"programId": 15` | Create Course then assign Program **15** |
| `"programId": null` | Create unassigned |
| *(property omitted)* | Create unassigned |

When `EnablePrograms = false`: Program assignment is **not** invoked; `programId` in the body is ignored for relationship writes (legacy Code/Name Course Master).

---

## Request examples

**Assign**

```json
{ "id": 1, "code": "BCOM", "name": "B.Com", "programId": 15 }
```

**Explicit unlink**

```json
{ "id": 1, "code": "BCOM", "name": "B.Com", "programId": null }
```

**Legacy / omit Program (keeps existing link on update)**

```json
{ "id": 1, "code": "BCOM2", "name": "Bachelor of Commerce" }
```

**Create with Program**

```json
{ "code": "BCOM", "name": "B.Com", "programId": 15 }
```

---

## Response contract

Success (`200 OK`) body shape from `CourseMasterRowDto`:

| Field | Type | Notes |
|-------|------|-------|
| `id` | number | Course id |
| `code` | string | Normalized uppercase |
| `name` | string | Trimmed |
| `programId` | number \| null | Authoritative `Course.ProgramId` after the operation |

Example:

```json
{ "id": 1, "code": "BCOM", "name": "B.Com", "programId": 15 }
```

Error mapping (controller):

| Condition | Status |
|-----------|--------|
| Validation / assign rules | `400` |
| Course not found | `404` |
| Assign auth failed | `403` |
| Unexpected | Global exception handler (ProblemDetails) |

---

## Backward compatibility

- Existing clients that **omit** `programId` on Update continue to update Code/Name only; Program link is preserved.
- Clients that never used Programs (`EnablePrograms = false`) keep legacy Course CRUD with no Assign call.
- Course Master UI (Programs enabled) always sends explicit `programId` (value or `null`) so omit-vs-null is unambiguous for that client.

---

## Implementation map

| Layer | Responsibility |
|-------|----------------|
| `CreateCourseRequest` / `UpdateCourseRequest` | Presence-aware `ProgramId` + `ProgramIdSpecified` |
| `CourseMasterWriteService` | Update calls Assign only when `ProgramIdSpecified && EnablePrograms` |
| `CourseController` | HTTP mapping; no separate assign route |

---

## Tests

`AI29_1D_24_Prompt4B4_ProgramIdApiContractTests.cs` — JSON deserialize + write-service proofs for assign / null / omitted / create / Programs disabled / response shape / no second endpoint on CourseController.
