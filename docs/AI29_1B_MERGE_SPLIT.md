# AI29.1B — Merge & Split

## Design refinements

- **Reversible**: sections are never physically deleted during merge/split.
- **Lineage**: `SectionLineage` + `ParentSectionId` + merge/split transactions with effective dates.
- **Allocation**: AI29.1C strategies are extension points only (`ISectionAllocationRecommendationService`).

## Merge sequence

```mermaid
sequenceDiagram
    participant UI
    participant API as Merge API
    participant Svc as SectionMergeService
    participant Life as Lifecycle SM
    participant Cap as Capacity Engine

    UI->>API: Validate / Preview
    API->>Svc: PreviewAsync
    Svc-->>UI: Preview (errors/warnings)
    UI->>API: Commit
    API->>Svc: CommitAsync
    Svc->>Svc: Move students/faculty (history rows)
    Svc->>Svc: Write SectionLineage
    Svc->>Life: Source → Merged
    Svc->>Cap: Occupancy advisory
    Svc-->>UI: Transaction DTO
```

## Split sequence

```mermaid
sequenceDiagram
    participant UI
    participant API as Split API
    participant Svc as SectionSplitService
    participant Alloc as Allocation (1C stub)
    participant Life as Lifecycle SM

    UI->>API: Preview (strategy)
    API->>Svc: PreviewAsync
    Svc->>Alloc: RecommendForSplitAsync (stub)
    Svc-->>UI: Proposed children
    UI->>API: Commit
    API->>Svc: Create child sections + lineage
    Svc->>Life: Source → Split
    Note over Svc: Students not auto-moved (AI29.1C)
```

## Combined sections (`SectionGroup`)

First-class aggregate with membership history (`SectionGroupMember`). One timetable entry / one attendance session may map to multiple sections via existing `TimetableSections` — no duplicate schedules.

## Permissions

`Section.Merge`, `Section.Split`
