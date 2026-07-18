# AI21.PHASE4 – Deployment Guide

## Prerequisites

- PostgreSQL database provisioned and migrated
- Cloudflare R2 bucket configured under `ArtifactStorage:R2`
- JWT secrets configured
- ONNX models deployed to API content root
- All AI21 phases (PHASE1–PHASE3) deployed

## Deployment Steps

1. **Build and publish**
   ```powershell
   dotnet publish Abhyanvaya.API -c Release -o ./publish
   ```

2. **Apply database migrations**
   ```powershell
   dotnet ef database update --project Abhyanvaya.Infrastructure --startup-project Abhyanvaya.API
   ```

3. **Configure environment variables**
   - `ConnectionStrings__DefaultConnection`
   - `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`
   - `ArtifactStorage__R2__Endpoint`, `ArtifactStorage__R2__AccessKeyId`, `ArtifactStorage__R2__SecretAccessKey`
   - `ProductionReadinessPolicy__MinimumHealthScore`

4. **Start application**
   ```powershell
   dotnet Abhyanvaya.API.dll
   ```

5. **Verify health endpoints**
   - `GET /health/live` — process alive
   - `GET /health/ready` — readiness gates (DB, storage, workers)

6. **Run production readiness evaluation**
   - Inject `IProductionReadinessService` or `IGoLiveCertificationService`
   - Pass `DeploymentContext` with environment metadata
   - Review certification decision before traffic cutover

## Post-Deployment Validation

Execute via DI:

```csharp
var context = new DeploymentContext { /* ... */ };
var certification = await certificationService.CertifyAsync(context);
// Decision: "GO LIVE APPROVED" or "GO LIVE BLOCKED"
```

## Rollback

If certification is blocked:

1. Do not route production traffic
2. Review `CriticalIssues` in certification report
3. Resolve failing checks
4. Re-run certification

See `AI21_PHASE4_OPERATIONAL_RUNBOOK.md` for incident procedures.
