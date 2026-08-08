# AI29.1B.7 — Context Builder

## Interface

`ISectionAllocationContextBuilder`

- `BuildAsync` / `RefreshAsync`
- `SnapshotAsync`
- `ValidateAsync`
- `BuildAnalysisContextAsync`
- `GetLastCompositionReportAsync`

## Composition sequence

```mermaid
sequenceDiagram
  participant API as Allocation APIs
  participant Cache as AllocationContextCache
  participant B as ContextBuilder
  participant H as Hierarchy/DB
  participant C as Capacity Engine
  participant R as Readiness/Health/Policies
  participant S as Students/Faculty/Subjects/TT

  API->>Cache: Get(scope)
  alt miss or refresh
    Cache-->>API: miss
    API->>B: Build/Refresh
    B->>H: Hierarchy + Sections
    B->>C: Occupancy
    B->>R: Per-section readiness/health/policies
    B->>S: Students, faculty, subjects, rooms
    B-->>API: SectionAllocationContext + CompositionReport
    API->>Cache: Set
  else hit
    Cache-->>API: cached context
  end
```

## Isolation rule

Allocation Engine must **never** call hierarchy/capacity/student/section services directly. Only the Builder composes those sources into an immutable context.

## Composition report

Each build records service step timings, outcomes, warnings, and total duration for debugging and performance tuning.
