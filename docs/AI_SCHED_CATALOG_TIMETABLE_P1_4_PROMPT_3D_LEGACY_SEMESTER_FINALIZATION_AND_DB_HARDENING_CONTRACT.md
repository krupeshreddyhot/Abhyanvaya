# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3D  
# Legacy Semester Finalization & Database Hardening — Contract

**Date:** 2026-08-22  
**Type:** Formal future-migration contract (NOT executed)  
**Authority:** ADL + frozen TG.5/TG.6 + CAP + P1-3/P1-4 decisions

---

## 1. Objective (future)

Finalize remaining legacy NULL-group Semesters and harden:

```sql
Semester.GroupId NOT NULL
UNIQUE (TenantId, GroupId, Number)
```

**This contract is not an authorization to execute.** Each phase requires a separate approved prompt.

---

## 2. Invariants that must hold before schema hardening

1. No `Semester.GroupId IS NULL` in in-scope tenant datasets (or historical archive strategy approved).
2. Every former legacy row has an explicit Architect disposition.
3. No Attendance / SA / TimetableEntry / Student / Subject / Section / TeachingGroup FKs require NULL-group semantics.
4. AcademicTree and UI cascade no longer treat NULL GroupId as course-wide wildcard (or historical-read-only path is isolated).
5. Semester create/update/import already require GroupId (P1-4 2A — done).
6. Student write-path rejects null-group Semester (P1-4 3B-A — done).
7. `TenantId + GroupId + Number` has zero active duplicates.
8. `Semester.CourseId == Group.CourseId` for all group-owned rows.
9. Tenant isolation verified fail-closed.
10. Rollback strategy operationalized (backup + journal).

---

## 3. Proposed migration sequence

### PHASE A — Resolve remaining legacy Semester dispositions
- Apply Architect-approved disposition per inventory row (HISTORICAL_RETAIN archive, SAFE_SINGLE_GROUP_MAPPING, DUPLICATE_REVIEW merge plan, etc.).
- Fail closed on ambiguity.
- **Do not** mutate Teaching Groups here.

### PHASE B — Teaching Group residual remediation (separate approved TG prompt)
- Only after explicit TG architecture approval.
- Remap `TeachingGroup.SemesterId` using deterministic GroupId + Number.
- Do not infer TG; do not write TimetableSection; projector remains sole TimetableSection writer.

### PHASE C — Deprecate NULL-group wildcard dependencies
- AcademicTree, filterSemestersForScope, SA/Subjects/Attendance UI, ElectiveGroups, schedulingFormUtils.
- Replace with Group-scoped Semester lists.
- Keep historical-read-only surfaces only if Architect requires transition UX.

### PHASE D — Downstream reference verification
- Re-run integrity audit + finalization audit.
- Attendance/SA/TT/Student/Subject/Section/TG on NULL-group must be zero (or archived).

### PHASE E — Resolve duplicate `TenantId + GroupId + Number`
- Manual survivor selection; remap FKs; archive losers.
- No automatic merge.

### PHASE F — Final integrity audit
- Critical=0, Errors=0; NotNull preconditions all green.

### PHASE G — Apply `Semester.GroupId NOT NULL`
- EF migration + DB constraint.
- Only after F.

### PHASE H — Apply `UNIQUE(TenantId, GroupId, Number)`
- EF migration + DB unique index.
- Only after G (or combined with G if Architect approves atomic schema step).

### PHASE I — Post-migration verification
- Insert/update rejection tests; regression TG/CAP/P1-3/P1-4; API/UI builds.

---

## 4. Rollback contract

| Element | Requirement |
| --- | --- |
| Pre-migration backup | Full DB backup / point-in-time restore point |
| Snapshot | Export Semester + FK counts by SemesterId before Phase A |
| Journal | Row-level source→target mapping for every SemesterId change |
| Transaction | Each phase executes in explicit DB transaction(s); fail → rollback phase |
| Checkpoints | After each phase: integrity audit must meet exit criteria |
| Rollback trigger | Any Critical/Error integrity finding; unexpected row counts; TG projector divergence |
| Schema rollback | Keep reverse EF migrations for G/H; do not drop columns |
| Data rollback | Restore from journal/backup; never guess reverse mappings |

---

## 5. Explicit non-goals (now and until approved)

- No Semester mutation in Prompt 3D.
- No Teaching Group mutation in Prompt 3D.
- No NOT NULL / UNIQUE applied in Prompt 3D.
- No CAP / ConflictEngine / Publish / Projector changes.

---

## 6. Recommended next prompt (Chief Architect)

**P1-4 Prompt 3E** (proposed): Architect-approved disposition execution for HISTORICAL_RETAIN / DUPLICATE_REVIEW rows **excluding** TG-blocked Sem 3 — OR a dedicated **TG residual Semester remap** prompt (Phase B) if TG remediation is prioritized.

**STOP** — do not start automatically.
