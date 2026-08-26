# AI-SCHED-TG.4A Prompt 7 — Disposable Pre-Production Timetable Conversion

**Workstream:** AI-SCHED-TG.4A  
**Prompt:** 7 — Explicit TimetableEntry → TeachingGroup conversion  
**Date:** 2026-08-18  
**Predecessor:** AI-SCHED-TG.4A Prompt 6 (PASS — read compatibility)

**STATUS: PASS**

---

## 1. Intent

Provide a **controlled, explicitly invoked** conversion utility for disposable pre-production timetable test data so the migration architecture can be proven.

```text
Legacy data
     │
     │ explicit conversion only
     ▼
TeachingGroup   (must already exist — never auto-created)
     │
     ▼
TeachingGroupSection   (via approved application boundary)
     │
     ▼
TimetableSection       (projection)
```

**Not** a permanent production backfill, hosted job, or startup reconciler.

---

## 2. API (explicit operator invocation)

| Method | Route | Auth |
|---|---|---|
| GET | `/api/scheduling/legacy-teaching-group-conversion/entries-without-teaching-group` | `CanManageSchedulingTimetable` |
| POST | `/api/scheduling/legacy-teaching-group-conversion` | `CanManageSchedulingTimetable` |

POST body:

```json
{
  "dryRun": true,
  "items": [
    {
      "timetableEntryId": 12,
      "teachingGroupId": 5,
      "sectionIds": [1, 2]
    }
  ]
}
```

Report outcomes per item: **Converted** | **Skipped** | **Rejected** + `reason`.

---

## 3. Conversion rules

| Rule | Enforcement |
|---|---|
| Explicit TeachingGroupId required | Reject if missing; never infer from SubjectAllocation |
| Explicit SectionIds only | Passed to `ReplaceSectionsAndProjectAsync` |
| Tenant + academic scope | `EnsureCompatibleWithTimetableEntry` + section scope checks |
| Draft only | `TimetableService.EnsureDraft` — rejects Published/Locked/Frozen/Archived |
| No implicit TG create | Service never constructs `TeachingGroup` |
| No Attendance / StudentSection | Not referenced |
| Transactional | Assign TeachingGroupId, then SoT+projection via boundary; undo TG on section failure |
| Idempotent | Same TG + SoT + projection → **Skipped** |
| Dry-run | Validate + report; no persistence |

---

## 4. Explicit non-invocation

Must **not** run from:

- Application startup / `Program.cs`
- GET `/sections` / combined sessions
- Attendance resolve
- SubjectAllocation lookup
- `IHostedService`

Architecture guards assert this.

---

## 5. Files

| File | Role |
|---|---|
| `ILegacyTimetableTeachingGroupConversionService` | Contract |
| `LegacyTimetableTeachingGroupConversionService` | Implementation |
| `LegacyTimetableTeachingGroupConversionDtos` | Request/report |
| `LegacyTimetableTeachingGroupConversionController` | Explicit admin API |
| DI `AddScoped` only | No hosted service |

---

## 6. Tests

- `LegacyTimetableTeachingGroupConversionTests`
- `LegacyTimetableTeachingGroupConversionArchitectureGuardTests`

**STATUS = PASS**
