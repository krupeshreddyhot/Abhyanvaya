# AI29.1C.5 — Replay

```mermaid
flowchart LR
  Old[Historical Scenario] --> Config[Stored Configuration]
  Config --> Run[Engine Run]
  Run --> New[New Scenario]
```

Never modifies the historical scenario. Never modifies live students.
