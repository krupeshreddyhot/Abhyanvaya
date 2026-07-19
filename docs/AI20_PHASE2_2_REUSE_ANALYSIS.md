# AI20.PHASE2.2 — Background Processing Reuse Analysis

**Milestone:** Pre-implementation review for distributed enrollment worker framework.

## Existing Components Reused

| Component | Location | Reuse |
|-----------|----------|-------|
| `IEnrollmentOrchestrator` | Phase 2.1.8 | Sole execution engine — workers delegate here |
| `IEnrollmentRetryPolicy` | Phase 2.1.8 | Retry timing via scheduler (not worker) |
| `IEnrollmentJobQueue` / wake signal | Phase 2.1.2 | Wake optimization for poll loop |
| `StudentEnrollmentItem` | Domain | Durable queue rows |
| `ClaimNextItemAsync` pattern | Item repository | Extended by work repository with lease guard |
| `IUnitOfWork` | Infrastructure | Transaction boundaries for lease/retry/dead-letter |
| `ITenantContextAccessor` | Infrastructure | Tenant scoping per worker scope |
| `StuckAttendanceSessionRecoveryService` | Background workers | Recovery sweep pattern |
| `ClassroomRecognitionBackgroundService` | Background workers | Scoped DI worker loop pattern |
| `EnrollmentBatchService.SignalWork` | Batch creation | Wake workers after batch commit |

## Not Reused (and Why)

| Component | Reason |
|-----------|--------|
| Orchestrator stages | Workers never call stages directly |
| `InMemoryEnrollmentWakeSignal.DequeueClaimedJobIdsAsync` stub | Replaced by scheduler + work queue |
| Direct `ClaimNextItemAsync` status→Downloading | Conflicts with orchestrator download stage; work claim reserves without status change |

## New Components — Rationale

| Class | Why new |
|-------|---------|
| `IEnrollmentBackgroundService` | Background host contract |
| `IEnrollmentWorkerHost` | Start/stop/scale workers (K8s/service ready) |
| `IEnrollmentWorker` | Stateless worker invoking orchestrator only |
| `IEnrollmentWorkScheduler` | Claim + retry schedule + eligibility |
| `IEnrollmentLeaseManager` | Distributed lease acquire/renew/release/expire |
| `IEnrollmentWorkRepository` | SQL for claim, retry, requeue |
| `IEnrollmentWorkQueue` | Queue abstraction (DB now; RabbitMQ/Azure later) |
| `IEnrollmentHeartbeatService` | HeartbeatUtc + pipeline state updates |
| `IEnrollmentRecoveryService` | Expired lease + stuck item recovery |
| `IEnrollmentSchedulingPolicy` | Priority/fair/retry ordering |
| `IDistributedLockProvider` | Cross-worker claim serialization |
| `IEnrollmentDeadLetterService` | Permanent failure persistence |
| `EnrollmentWorkLease` entity | Lease durability across servers |
| `EnrollmentDeadLetterEntry` entity | Manual review / future replay |

## Future Queue Providers

`IEnrollmentWorkQueue` enables swapping implementations without changing worker logic:

| Provider | Implementation |
|----------|----------------|
| Database (current) | `DatabaseEnrollmentWorkQueue` |
| RabbitMQ | Future `RabbitMqEnrollmentWorkQueue` |
| Azure Queue | Future `AzureQueueEnrollmentWorkQueue` |
| SQS | Future `SqsEnrollmentWorkQueue` |
| Kafka | Future `KafkaEnrollmentWorkQueue` |
| Hangfire | Future `HangfireEnrollmentWorkQueue` |

## Future Distributed Execution

| Concern | Interface | Current | Future |
|---------|-----------|---------|--------|
| Leasing | `IEnrollmentLeaseManager` | PostgreSQL table | Redis/Azure Blob |
| Claim lock | `IDistributedLockProvider` | pg_advisory_lock | Redis/PostgreSQL advisory |
| Heartbeat | `IEnrollmentHeartbeatService` | DB column updates | Redis TTL |
| Recovery | `IEnrollmentRecoveryService` | Periodic sweep | Leader-elected sweep |

## Architecture Boundary

```
BackgroundService → WorkerHost → Worker → IEnrollmentOrchestrator
                      ↓
              Scheduler + LeaseManager + WorkRepository
```

No AI, no pipeline logic, no orchestration duplication in background layer.
