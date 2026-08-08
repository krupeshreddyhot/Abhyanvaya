# AI29.1B.5 — Architecture Review

## Boundaries

```mermaid
flowchart LR
    Sec[Section] -.->|forbidden| Att[Attendance]
    Sec -->|allowed via TimetableSection| Sched[Scheduling]
    SG[SectionGroup] -->|allowed via AttendanceSessionSection| Att
```

## Preview / commit separation

```mermaid
sequenceDiagram
    participant UI
    participant Preview as Merge/Split Preview
    participant Commit as Merge/Split Commit
    UI->>Preview: Preview (read-only)
    Preview-->>UI: Warnings / readiness
    UI->>Commit: Commit (writes + versions)
```

## Timeline flow

```mermaid
flowchart TD
    V[SectionVersions] --> T[ISectionTimelineService]
    L[SectionLifecycleTransitions] --> T
    T --> R[Read-only timeline]
```

## Verification

- Preview services have no write / lifecycle commit dependencies (guard-enforced)
- Versioning isolated from capacity formula engine (capacity still sole calculator)
- Policies independent of AI29.1C allocation
- Tenant isolation on all new tables
- Observability reuses AI29.1A.7 telemetry
