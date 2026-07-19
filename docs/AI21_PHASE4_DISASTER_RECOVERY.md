# AI21.PHASE4 – Disaster Recovery Plan

## Objectives

- Restore database within RTO target
- Reconnect Cloudflare R2 storage
- Resume worker and queue processing
- Validate platform before traffic restoration

## Recovery Procedures

### Database Restore

1. Stop API application
2. Restore PostgreSQL from latest verified backup
3. Apply any pending migrations
4. Verify connectivity via `IDeploymentVerificationService`
5. Validate tenant data isolation

### Artifact Metadata Restore

1. Artifact binary content resides in R2 (not database)
2. Restore `ArtifactRegistryEntry` and `ArtifactStorageManifest` from database backup
3. Verify checksums via `IArtifactVerificationService` policy
4. Re-run artifact smoke tests

### Storage Reconnection

1. Update R2 credentials if rotated
2. Verify bucket accessibility
3. Run storage health check
4. Confirm artifact upload worker resumes

### Worker and Queue Recovery

1. Restart API host (recycles all hosted services)
2. In-memory queues reset; persistent work resumes from database
3. Run `IFaceEnrollmentRecoveryService` for incomplete enrollment batches
4. Run `IEnrollmentRecoveryService` for AI20 worker leases
5. Monitor queue depth until normalized

## Non-Production Validation

Disaster recovery validation (`IDisasterRecoveryValidator`) executes dry-run checks:

- Recovery service registration
- Runbook availability
- Database connectivity simulation
- Queue recovery procedure documented

These checks do not modify production data.

## Recovery Testing Schedule

| Test | Frequency | Owner |
|------|-----------|-------|
| Backup verification | Weekly | Operations |
| Recovery dry-run | Monthly | Platform |
| Full DR simulation | Quarterly | Architecture |

## Escalation

1. On-call engineer
2. Platform operations lead
3. Chief architect
4. Executive stakeholder notification (extended outage)

## Post-Recovery

1. Execute full production readiness evaluation
2. Generate go-live certification
3. Document incident timeline
4. Update runbooks with lessons learned
