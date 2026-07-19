# AI21.PHASE4 – Go-Live Checklist

## Environment

- [ ] Production environment configured
- [ ] Secrets loaded (JWT, R2 credentials)
- [ ] Connection strings validated
- [ ] Database migrations applied
- [ ] Configuration validation passed

## Platform Components

- [ ] Photo Acquisition (AI21.PHASE1) — health check pass
- [ ] Face Enrollment (AI21.PHASE2) — pipeline registered
- [ ] Artifact Storage (AI21.PHASE3) — R2 connectivity verified
- [ ] Recognition (AI20.PHASE2.3) — health check pass
- [ ] Attendance (AI20.PHASE2.4) — health check pass
- [ ] Governance (AI20.PHASE2.5) — health check pass
- [ ] Operations (AI20.PHASE2.6) — telemetry and tracing active
- [ ] Workers — all hosted services registered

## Smoke Tests

- [ ] Application starts
- [ ] `/health/live` returns 200
- [ ] `/health/ready` returns 200
- [ ] Database connectivity verified
- [ ] R2 storage connectivity verified
- [ ] Artifact upload queue accessible
- [ ] Background workers registered
- [ ] Telemetry snapshot collected
- [ ] Tracing context created

## Performance

- [ ] Load test enrollment count within policy
- [ ] Concurrent upload capacity validated
- [ ] Recognition throughput acceptable
- [ ] Average latency within policy
- [ ] Queue depth within policy
- [ ] Worker utilization acceptable

## Security

- [ ] JWT authentication configured
- [ ] Authorization services registered
- [ ] Tenant isolation verified
- [ ] Storage credentials secured
- [ ] No PII in operational logs

## Backup & Recovery

- [ ] Database backup procedure documented
- [ ] Artifact metadata accessible for backup
- [ ] Configuration backup verified
- [ ] Recovery runbooks available
- [ ] Disaster recovery validation passed

## Certification

- [ ] `IGoLiveCertificationService.CertifyAsync` executed
- [ ] Overall score ≥ `MinimumHealthScore`
- [ ] No critical issues
- [ ] Decision: **GO LIVE APPROVED**

---

**Sign-off**

| Role | Name | Date | Decision |
|------|------|------|----------|
| Operations | | | |
| Security | | | |
| Architecture | | | |
