# AI20.PHASE2.5 — Model Lifecycle Reuse Analysis

**Milestone:** Pre-implementation review for AI Model Lifecycle Platform.

## Existing Components Reused

| Component | Location | Reuse |
|-----------|----------|-------|
| `RecognitionGoldenDataset` (PHASE2.3 stub) | Application/Recognition/Regression | Concept extended in lifecycle golden datasets |
| `IAttendanceAnalyticsService` (PHASE2.4) | ClassroomAttendance | Complementary; lifecycle adds model-level metrics |
| `InsightFaceOptions` / embedding metadata | Infrastructure | Version strings for compatibility checks |
| `IApplicationDbContext` + repository pattern | Infrastructure | `IModelLifecycleRepository` |
| Domain events pattern | Domain/Events | Model lifecycle events |
| `AttendanceRecognition` metrics | Domain | Operational metrics for quality engine |

## Not Reused (and Why)

| Component | Reason |
|-----------|--------|
| `IRecognitionOrchestrator` | Unchanged — lifecycle never invokes inference in this phase |
| `IEnrollmentOrchestrator` | Frozen enrollment boundary |
| `IClassroomRecognitionOrchestrator` | Frozen attendance boundary |
| Background workers | Frozen worker framework |
| PHASE2.3 `RecognitionRegressionRunner` stub | Superseded by lifecycle `IRecognitionRegressionRunner` in Common.Interfaces |

## New Components — Rationale

| Class | Why new |
|-------|---------|
| `IModelRegistry` | Central model metadata registration |
| `IModelVersionManager` | Version lifecycle without inference |
| `IActiveModelProvider` | Sole model supply seam for recognition consumers |
| `IModelCompatibilityService` | Pipeline/model compatibility validation |
| `IEmbeddingCompatibilityService` | Embedding/recognition version matrix |
| `IGoldenDatasetManager` | Versioned immutable golden datasets |
| `IRecognitionRegressionRunner` | Regression independent of deployment |
| `IRecognitionBenchmarkService` | Benchmark independent of regression |
| `IRecognitionQualityEngine` | Aggregated quality summaries |
| `IDriftDetectionService` | Observational drift only |
| `IModelRolloutManager` / `IModelRollbackManager` | Governed deployment control |
| `IRecognitionMetricsService` | Operational telemetry |
| `IContinuousLearningCoordinator` | Retraining candidate queue — no ML |
| `IModelLifecycleRepository` | All SQL isolated |

## Future AI Roadmap

| Phase | Capability |
|-------|------------|
| Wire `IActiveModelProvider` | Recognition engine reads active model at runtime |
| Real regression runner | Invoke orchestrator against golden dataset images |
| GPU benchmark harness | Hardware metrics collection |
| Automated rollout gates | Regression + benchmark approval before production |
| Continuous learning pipeline | Export retraining candidates to ML platform |
| Blue/green deployment | Extend `IModelRolloutPolicy` |

## Architecture Boundary

```
Recognition Engine → IActiveModelProvider → IModelRegistry
Quality metrics → IRecognitionQualityEngine → IRecognitionMetricsService
Deployment → IModelRolloutManager / IModelRollbackManager (no inference)
Drift → IDriftDetectionService (observational only)
Learning → IContinuousLearningCoordinator (queue only)
```

No enrollment, recognition, attendance, or worker modifications.
