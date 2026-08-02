# AI30 Phase 2B.6.1 — Optimization Strategy Framework

## Contracts

- `IOptimizationStrategy`
- `OptimizationContext`
- `OptimizationRequest`
- `OptimizationResult`
- `OptimizationScore`
- `OptimizationCandidate`
- `OptimizationSummary`
- `OptimizationExecution`

## Shipped strategy

`NoOpOptimizationStrategy` (`Kind=None`, `IsImplemented=false`) — readiness placeholder only.

## Future strategies (catalog / Phase 3)

Greedy, Genetic, Simulated Annealing, Tabu Search, AI Assisted, Manual Assisted

## Guarantees

- Repository + CQRS-style DTO reads
- DI registration via `AddOptimizationReadiness()`
- Never hard-codes algorithms into the platform core
- Never mutates live timetables
