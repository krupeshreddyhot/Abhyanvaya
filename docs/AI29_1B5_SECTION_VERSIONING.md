# AI29.1B.5 — Section Versioning

## Immutability

`SectionVersion` is append-only. Never update or delete a version row for business corrections — record a new version.

## Operations

Create · Update · Merge · Split · CapacityChange · LifecycleChange

## Version lifecycle

```mermaid
flowchart LR
    Op[Operational change] --> Snap[Snapshot Section state]
    Snap --> Vn[Append SectionVersion n]
    Vn --> Link[PreviousVersionId = n-1]
```

Hooks: create/update, lifecycle transition, capacity update, merge commit, split commit.
