# AI20.PHASE2.3 — Recognition Engine Reuse Analysis

**Milestone:** Pre-implementation review for AI Recognition Engine & Vector Search Framework.

## Existing Components Reused

| Component | Location | Reuse |
|-----------|----------|-------|
| `IEmbeddingEngine` | Enrollment/InsightFace | Embedding extraction stage references engine metadata; aligned-face path |
| `IFaceDetectionService` | InsightFace | Classroom image → face embedding extraction |
| `IFaceMatcher` | Recognition/FaceMatcher | Existing classroom pipeline unchanged; logic mirrored in decision thresholds |
| `StudentFaceEmbedding` | Domain | Canonical embedding store populated by enrollment |
| `InsightFaceEngine` | Infrastructure | Shared ONNX inference host |
| `IRecognitionMediaService` | Recognition | Thumbnail persistence (existing classroom pipeline) |
| `IRecognitionExecutionContext` | Services | Correlation/trace context |
| Enrollment pipeline patterns | Phase 2.1.8 | Orchestrator → Executor → Stages model mirrored |
| `EnrollmentPipelineStatistics` pattern | Application | `RecognitionStatistics` with stage durations |
| Domain events pattern | `EnrollmentPipelineEvents` | `RecognitionPipelineEvents` |

## Not Reused (and Why)

| Component | Reason |
|-----------|--------|
| `IEnrollmentOrchestrator` | Recognition is a separate bounded context |
| `IEnrollmentPipelineExecutor` | Different artifacts (candidates, search results, decisions) |
| `IEnrollmentResultWriter` | Recognition persists `AttendanceRecognition`, not embeddings |
| `ClassroomRecognitionPipeline` internals | Frozen — new engine is parallel architecture |
| Direct EF in pipeline | Moved to `IRecognitionRepository` |

## New Components — Rationale

| Class | Why new |
|-------|---------|
| `IRecognitionOrchestrator` | Recognition workflow coordinator |
| `IRecognitionPipelineExecutor` | Stage sequencing without enrollment coupling |
| `IRecognitionCandidateProvider` | Candidate retrieval separated from search |
| `IVectorSearchEngine` | Top-K search without decision logic |
| `ISimilarityEngine` | Score normalization separated from search |
| `IRecognitionDecisionEngine` | Policy/threshold application only |
| `IRecognitionResultWriter` | Dedicated recognition persistence |
| `IRecognitionRepository` | All SQL for candidates + results |
| `IVectorDatabaseProvider` | pgvector now; FAISS/Qdrant/Milvus future |
| `IRecognitionCandidateStrategy` | Pluggable scope filters |
| `IRecognitionPolicy` | Configurable thresholds |
| `ISimilarityProvider` | Cosine/Euclidean/InnerProduct abstraction |
| `RecognitionDecisionContext` | Immutable decision input |
| `RecognitionPipelineState` | Typed runtime state |
| Regression stubs | Future AI regression architecture only |

## Future ANN Providers

| Provider | Implementation |
|----------|----------------|
| PostgreSQL pgvector (current abstraction) | `PostgreSqlVectorDatabaseProvider` |
| FAISS | Future `FaissVectorDatabaseProvider` |
| Qdrant | Future `QdrantVectorDatabaseProvider` |
| Milvus | Future `MilvusVectorDatabaseProvider` |
| Pinecone | Future `PineconeVectorDatabaseProvider` |
| Weaviate | Future `WeaviateVectorDatabaseProvider` |
| Azure AI Search | Future `AzureSearchVectorDatabaseProvider` |

## Architecture Boundary

```
IRecognitionOrchestrator → IRecognitionPipelineExecutor
    → CandidateProvider → VectorSearchEngine → SimilarityEngine
    → DecisionEngine → ResultWriter
```

No enrollment modifications. No AI logic in orchestrator. No SQL outside repository.
