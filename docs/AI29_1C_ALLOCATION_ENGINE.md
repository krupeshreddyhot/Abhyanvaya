# AI29.1C — Allocation Engine

## Principle

The engine consumes **only** `SectionAllocationContext` and produces an `AllocationScenario`.

It never writes live `StudentSection` rows.

```mermaid
flowchart LR
  Ctx[SectionAllocationContext] --> Engine[AllocationEngine]
  Engine --> Scenario[AllocationScenario]
  Scenario --> Sim[Simulation / Compare]
  Scenario --> Approve[Approval → Draft only]
  Engine -.->|forbidden| Repos[(Repositories / Capacity / Policy services)]
```

## Core types

- `AllocationEngine` / `IAllocationEngine.ExecuteAsync`
- `AllocationExecutionContext` / `AllocationExecutionResult`
- `AllocationSession` (persisted as `AllocationEngineSession`)
- `AllocationScenario` with explainable student recommendations
