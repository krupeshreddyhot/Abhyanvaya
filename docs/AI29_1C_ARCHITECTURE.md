# AI29.1C — Architecture

```mermaid
flowchart TB
  subgraph platform [AI29.1B.7 Platform]
    Builder[Context Builder]
    Ctx[SectionAllocationContext]
  end
  subgraph engine [AI29.1C Engine]
    Exec[ExecutionService]
    Eng[AllocationEngine]
    Pipe[Pipeline Strategies]
    Cons[ConstraintEngine]
    Score[ScoreCalculator]
  end
  subgraph persist [Persistence]
    Session[Sessions]
    Scenario[Scenarios]
    Draft[Drafts]
    Sandbox[Sandbox]
  end
  Builder --> Ctx
  Exec --> Builder
  Exec --> Eng
  Eng --> Pipe
  Eng --> Cons
  Eng --> Score
  Exec --> Session
  Exec --> Scenario
  Draft -.->|no live writes| Students[(StudentSections)]
```

## Guard

`AcademicArchitectureGuard.ValidateAllocationBoundaries()` rejects engine dependencies on DbContext, capacity, readiness, health, policy services, and repositories.
