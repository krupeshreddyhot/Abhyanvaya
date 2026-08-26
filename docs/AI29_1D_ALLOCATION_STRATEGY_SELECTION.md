# AI29.1D — Allocation Strategy Selection UI

Strategy step of the Enterprise Allocation Workspace configures **existing** AI29.1C `AllocationPipelineConfig` fields only. React never computes scores or placements.

## Contracts

| UI concern | Engine field |
|------------|--------------|
| Primary strategy | `groupingMode` |
| Pipeline toggles | `enabledStrategies` |
| Constraint levels | `constraintPriorities` (`Mandatory` / `Preferred` / `Informational`) |

Posted on `POST /allocation/run` and `POST /allocation/simulate`.

## Catalog endpoints

- `GET /allocation/grouping-modes`
- `GET /allocation/pipeline-strategies`
- `GET /allocation/constraint-priorities` (defaults)

## Exposed strategies

Student Number, Last Three Digits (`LastThreeDigits` grouping), Alphabetical Order, Gender Balance, Merit, Scholarship Category, Minor Subject, Language, Transport Route, Hostel, Elective Combination, and Weighted / Combined (preset enabling multiple pipeline strategies; Scoring remains on the server).

## UI surfaces

- Human-readable explanation per selected criterion
- Engine payload preview (`groupingMode`, `enabledStrategies`, `constraintPriorities`)
