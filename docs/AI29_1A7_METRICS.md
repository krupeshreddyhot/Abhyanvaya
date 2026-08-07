# AI29.1A.7 — Metrics Catalogue

## Operations

| Key | Span name |
|-----|-----------|
| `hierarchy.build` | `AcademicHierarchy.Build` |
| `tree.build` | `AcademicTree.Build` |
| `search.execute` | `AcademicSearch.Execute` |
| `breadcrumb.build` | `AcademicBreadcrumb.Build` |
| `structure.api` | `AcademicStructure.Api` |
| `program.statistics` | `ProgramStatistics.Load` |
| `snapshot.generate` | `AcademicHierarchy.Snapshot` |
| `architecture.guard` | `AcademicArchitecture.Guard` |

## Cache counters

- `cache.hierarchy.hit` / `miss`
- `cache.statistics.hit` / `miss`
- `cache.warm` / `refresh` / `invalidate`

## Domain events

- `ProgramCreated` / `ProgramUpdated` / `ProgramArchived`
- `CourseAssigned` / `CourseRemoved`

Counters: `published`, `succeeded`, `failed` + processing duration samples.
