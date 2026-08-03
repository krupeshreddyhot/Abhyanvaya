# AI30 Phase 2B.6 — Architecture Review

## Verdict

Optimization Readiness delivered as **architecture only**. No AI scheduling, genetic algorithms, simulated annealing, hill climbing, auto generation, auto conflict fixing, or automatic timetable updates.

## Isolation verified

| Capability | Owner | Mutates timetable? |
|------------|-------|--------------------|
| Conflict detection | `ConflictEngine` | No |
| Conflict advice | `ConflictResolutionAdvisor` | No |
| Impact analysis | `ImpactAnalyzer` | No |
| Dependency graph | `ConflictDependencyAnalyzer` | No |
| Optimization framework | `IOptimizationStrategy` / NoOp | No |
| Scoring | `OptimizationScoreCalculator` | No (independent) |
| Simulation | `OptimizationSimulationService` | No (preview only) |

## Architectural principles enforced

1. **Strategy pattern only** — algorithms loaded via `IOptimizationStrategy`
2. **Simulation before execution** — proposed changes + score/conflict/workload impact before any future apply
3. **Immutable timetable** — Accept marks status only; `CanApply=false`, `ModifiesTimetable=false`
4. **Scoring independent of optimization** — calculator does not know strategies
5. **Attendance compatibility** — Legacy + Timetable modes preserved

## Plugin registry

Registers Optimization / Scoring / Simulation / Metric / Recommendation / Conflict providers. Future Phase 3 strategy kinds are cataloged as unimplemented.

## Explicit non-scope (honored)

- No Phase 3 optimizer implementation
- No Apply button in UI
- No timetable writes from optimization APIs
