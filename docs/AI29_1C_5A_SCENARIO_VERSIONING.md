# AI29.1C.5A — Scenario Versioning

Immutable `AllocationScenarioVersion` rows are created via `IAllocationScenarioVersionService` for meaningful governance operations:

Create, Save, Review, Compare, Approve, Reject, Archive (+ Simulate/Replay when lifecycle changes).

Each version stores enough state to reconstruct the scenario:

- ScenarioId, VersionNumber, ContextVersion, ContextChecksum  
- Strategy/Constraint configuration versions  
- Operation, Reason, Lifecycle Status, Score  
- ScenarioJson / ConfigJson / TraceJson  
- Canonical ScenarioChecksum  

Versions are never updated after insert.
