# AI30 Phase 2B.7 — Optimization Sandbox

## Purpose

Safe environment for storing, replaying, comparing, and reviewing optimization simulations.

**Does not** optimize, edit timetables, or generate schedules.

## Domain

- `OptimizationScenario` / `OptimizationSnapshot`
- `ScenarioStatus`: Draft → Saved → Compared → Reviewed → Archived
- `SandboxService`, `IOptimizationScenarioRepository`

## Scenario contents (snapshot)

Current timetable reference (read-only), simulation, scores, conflict summary, metrics, recommendations.

## Immutability

After Save, snapshot payloads are immutable. Changes require Duplicate → new Draft → Save.

## Production isolation

```
Production Timetable → (read only) → Optimization Sandbox → Scenario → Replay → Compare
```
