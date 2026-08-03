# AI30 Phase 2B.5 — Architecture Review

## Decision Support, Not Automation

Recommendations, impact graphs, and dependency graphs are advisory. Users remain in control. No automatic timetable edits. No Phase 3 optimizer.

## Separation of Responsibilities

| Service | Owns |
|---------|------|
| `ConflictEngine` | Detection |
| `ConflictResolutionAdvisor` | Recommendations |
| `ImpactAnalyzer` | Consequences |
| `ConflictDependencyAnalyzer` | Relationships |
| `ConflictRuleConfigurationService` | Threshold values only |
| `ConflictExplainabilityService` | Enterprise explain payloads |
| `ConflictAnalyticsService` | Historical analytics |

These services are registered independently and are not merged.

## Pluggable Guidance

Recommendation providers implement `IConflictRecommendationProvider` and are DI-registered, mirroring conflict rule plugins.

## Attendance Backward Compatibility

Hard requirement satisfied — see `AI30_PHASE2B5_ATTENDANCE_COMPATIBILITY.md`.

## Platform conventions

- Repository / DbContext access with tenant scoping
- Soft delete on new workspace/threshold entities
- Audit via `ConflictRuleConfigChangeHistory`
- Permissions reuse `Scheduling.Conflict.View` / `Manage`
- CQRS-style read models via DTO projection services

## Explicit non-scope (honored)

- No optimizer
- No auto-fix
- No timetable mutation from intelligence APIs
- No Phase 3 implementation
