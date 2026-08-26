# AI29.1C — Strategies

## Pipeline (enabled via configuration)

```mermaid
flowchart TD
  V[Validation] --> C[Capacity]
  C --> P[Policy]
  P --> G[Gender]
  G --> L[Language]
  L --> S[Scholarship]
  S --> E[Elective]
  E --> T[Transport]
  T --> H[Hostel]
  H --> M[Merit]
  M --> SC[Scoring]
  SC --> Scenario[Scenario]
```

Disabled strategies appear in the allocation trace as skipped.

## Grouping modes

StudentNumber, StudentNumberRange, LastThreeDigits, Alphabetical, Merit, Gender, Language, Scholarship, MinorSubject, Hostel, Transport, ElectiveCombination.
