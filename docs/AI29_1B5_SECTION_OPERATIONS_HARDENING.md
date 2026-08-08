# AI29.1B.5 — Enterprise Section Operations Hardening

Hardens AI29.1B with immutable versioning, capacity history, timeline projections, read-only merge/split preview engines, hierarchical policies, recommendations, health, and architecture guards.

## Non-goals

- No Attendance / Scheduling / Subject Master / Dashboard / Allocation Engine changes

## Components

| Area | Service |
|------|---------|
| Versioning | `ISectionVersioningService` |
| Capacity history | `ISectionCapacityHistoryService` |
| Timeline | `ISectionTimelineService` (projection) |
| Merge preview | `IMergePreviewService` (read-only) |
| Split preview | `ISplitPreviewService` (read-only) |
| Policies | `ISectionPolicyService` |
| Recommendations | `ISectionCapacityRecommendationService` |
| Health | `ISectionHealthService` |
| Guard | `AcademicArchitectureGuard.ValidateSectionBoundaries()` |

## Apply

```bash
dotnet ef database update --project Abhyanvaya.Infrastructure --startup-project Abhyanvaya.API
```

Migration: `20260808120000_AI29_1B_5_SectionOperationsHardening`
