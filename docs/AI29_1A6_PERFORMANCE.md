# AI29.1A.6 — Performance

## Targets

| Metric | Target |
|--------|--------|
| Hierarchy tree from cache | &lt; 50 ms |
| Cached statistics retrieval | &lt; 30 ms |
| Cold hierarchy build (typical college) | &lt; 500 ms |
| Attendance / Scheduling latency | No increase |

## Cache separation

```mermaid
flowchart TB
  subgraph Hierarchy["academic-hierarchy:{tenant}:*"]
    P[programs]
    C[courses]
    G[groups]
    S[semesters]
  end
  subgraph Stats["academic-statistics:{tenant}:*"]
    PS[program-stats]
    HS[hierarchy-stats]
  end
  HC[IAcademicHierarchyCache] --> Hierarchy
  SC[IAcademicStatisticsCache] --> Stats
  ICS[ICacheService] --> HC
  ICS --> SC
```

Hierarchy changes rarely; statistics change frequently. **Never share cache keys.**

## Cold vs warm

```mermaid
sequenceDiagram
  participant API
  participant Tree as AcademicTreeService
  participant Cat as Catalog
  participant HCache as HierarchyCache
  participant SCache as StatisticsCache

  API->>Tree: BuildTree
  Tree->>Cat: masters
  Cat->>HCache: GetCourses
  alt miss
    Cat->>Cat: DB OrderBy DisplayOrder,Name
    Cat->>HCache: Set
  end
  Tree-->>API: ReadModel
  API->>SCache: GetStatistics
  alt miss
    API->>API: compute + SetStatistics
  end
```

## Snapshots

`AcademicHierarchy:EnableDailySnapshots` defaults to **false**. No daily job runs unless enabled.
