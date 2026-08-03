# AI22.8 Migration Guide

## Schema

- `AttendanceSession`: `WorkflowStatus`, `LastActivityUtc`, `ResumeCheckpointJson`, `WorkflowExpiredUtc`
- `AttendanceRetryHistory` table
- `SchedulingWorkspacePreference.RecoveryPreferencesJson`

## Apply

```powershell
dotnet ef database update --project Abhyanvaya.Infrastructure --startup-project Abhyanvaya.API
```

## Config

`appsettings.json` → `AttendanceRecovery` (DefaultExpirationHours 24|48|72).
