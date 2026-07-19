# AI21.PHASE4 – Operational Runbook

## Database Failure

**Symptoms:** Health check fails for Database component; `/health/ready` returns 503.

**Recovery Steps:**
1. Verify PostgreSQL connectivity and credentials
2. Check connection pool exhaustion
3. Review database health check duration in operations dashboard
4. Restore from backup if data corruption detected
5. Restart API process after database recovery

**Escalation:** Database administrator → Platform operations lead

---

## Cloudflare R2 Failure

**Symptoms:** Storage health check fails; artifact uploads failing.

**Recovery Steps:**
1. Verify R2 endpoint and credentials in `ArtifactStorage:R2`
2. Check R2 bucket permissions and quota
3. Review artifact upload worker logs (no PII)
4. Confirm network connectivity to R2 endpoint
5. Retry failed uploads after storage recovery

**Escalation:** Infrastructure team → Cloudflare support

---

## Recognition Failure

**Symptoms:** Recognition health degraded; classroom sessions stalling.

**Recovery Steps:**
1. Verify ONNX models present in API content root
2. Check recognition health provider status
3. Inspect active alerts for recognition latency
4. Confirm active model version in governance registry
5. Restart recognition background worker if offline

**Escalation:** AI platform team → Model governance lead

---

## Enrollment Failure

**Symptoms:** Enrollment jobs failing; queue depth increasing.

**Recovery Steps:**
1. Check enrollment health provider
2. Review face enrollment batch job states in database
3. Use `IFaceEnrollmentRecoveryService.ResumeBatchAsync` for interrupted batches
4. Verify photo acquisition items in ReadyForEnrollment state
5. Monitor enrollment telemetry durations

**Escalation:** Enrollment platform team

---

## Queue Failure

**Symptoms:** Queue depth exceeds policy; workers not dequeuing.

**Recovery Steps:**
1. Verify hosted services are running
2. Check queue registration in DI
3. Review worker health checks
4. Restart application to reset in-memory queues
5. Resume processing from database-persisted state

**Escalation:** Worker platform team

---

## Worker Failure

**Symptoms:** Background worker not registered or not running.

**Recovery Steps:**
1. Confirm hosted service registration in DI
2. Check worker startup logs
3. Verify no unhandled exceptions in worker loop
4. Restart API host process
5. Validate `/health/ready` worker checks pass

**Escalation:** Platform operations team

---

## General Recovery Procedure

1. Assess impact via `IAIOperationalDashboardService`
2. Check active alerts via `IAIAlertManager`
3. Run smoke tests via `IProductionSmokeTestService`
4. Execute scenario validation via `IProductionValidationScenarioRunner`
5. Re-certify via `IGoLiveCertificationService` before restoring traffic

## Validation Checklist After Recovery

- [ ] All health checks pass
- [ ] Queue depth normalized
- [ ] No critical alerts active
- [ ] Smoke tests pass
- [ ] Go-live certification re-approved
