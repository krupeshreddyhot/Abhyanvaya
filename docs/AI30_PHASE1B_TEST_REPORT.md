# AI30.9B — Test Report

| Field | Value |
|-------|-------|
| **Suite** | `Abhyanvaya.Application.UnitTests/Scheduling/Phase1B` |
| **Scope** | Faculty preferences, room features, subject delivery, holiday types, permissions |
| **Excluded** | Attendance, AI recognition, enrollment |

## Coverage intent

Unit tests for validators, services (business rules), permission keys. Integration-style service tests use mocks — no Attendance/AI fixtures.

## Run

```powershell
dotnet test Abhyanvaya.Application.UnitTests --filter FullyQualifiedName~Phase1B
dotnet test Abhyanvaya.Application.UnitTests --filter FullyQualifiedName~Scheduling
```

**Local run:** Scheduling filter suite passed (includes Phase1B unit tests for preferences, room features, delivery validation, holiday catalog, permission keys).
