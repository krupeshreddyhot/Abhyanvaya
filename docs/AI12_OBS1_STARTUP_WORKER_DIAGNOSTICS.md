# AI12.OBS.1 — Startup Worker Health & Environment Diagnostics

**Status: IMPLEMENTED**
**Date:** 2026-07-04
**Reviewer:** Chief Software Architect

---

## 1. Objective

Replace the previous binary `Enabled`/`Disabled` worker log lines with real, host-derived health diagnostics for each hosted background worker, and explain *why* `BackgroundServiceExceptionBehavior` is configured the way it is at startup.

This is a diagnostics-only enhancement: no business logic, queue implementation, recognition logic, or controller was modified.

---

## 2. Before / after

**Before (AI11.HARDENING.2):**

```
Recognition Worker                  : Enabled
Embedding Worker                    : Enabled
```

**After (AI12.OBS.1):**

```
BackgroundServiceExceptionBehavior  : Ignore
  Reason                             : Development Environment
Recognition Worker
  Registered                         : Yes
  Running                             : Yes
  Startup Status                      : Started
  Health                              : Healthy
Embedding Worker
  Registered                         : Yes
  Running                             : Yes
  Startup Status                      : Started
  Health                              : Healthy
```

---

## 3. How values are determined (no hardcoding)

### 3.1 A critical prerequisite: logging after the host actually starts

The previous startup summary was logged **before** `app.Run()`. Under ASP.NET Core's Generic Host, hosted services (`IHostedService.StartAsync`, which for a `BackgroundService` kicks off `ExecuteAsync`) are only started when the host itself starts — which happens *inside* `app.Run()`. Logging before that point would have meant "Running: Yes" was never actually true yet; it would have been a guess, not a fact.

To fix this without changing behavior, `Program.cs` now splits what `app.Run()` already does internally:

```csharp
await app.StartAsync();
LogStartupConfigurationSummary(app);
await app.WaitForShutdownAsync();
```

This is **not a behavior change** — `WebApplication.Run()` is documented to perform exactly this sequence (`StartAsync` then block until shutdown via `WaitForShutdownAsync`, then dispose). Splitting it only moves the startup summary to a point where hosted services are guaranteed to have already been started, so "Running" reflects reality instead of a pre-start assumption.

### 3.2 Worker diagnostics: `BackgroundWorkerInspector`

New file: `Abhyanvaya.API/Diagnostics/BackgroundWorkerInspector.cs`.

```csharp
public static BackgroundWorkerStatus Inspect(IServiceProvider services, Type workerType)
{
    var instance = services.GetServices<IHostedService>()
        .FirstOrDefault(service => service.GetType() == workerType);
    ...
    var executeTask = ((BackgroundService)instance).ExecuteTask;
    ...
}
```

- `services.GetServices<IHostedService>()` returns the **actual singleton instances** the Generic Host is running (`AddHostedService<T>()` registers `T` as a singleton `IHostedService`), so this is not a fresh/fake instance — it's the live worker.
- **Registered**: `Yes` if a hosted service of that exact `Type` is present in the DI container's `IHostedService` collection; `No` otherwise. This directly answers "is this worker wired into the host at all", independent of whether it's currently running.
- **Running**: derived from the public `BackgroundService.ExecuteTask` property — `Yes` when the task exists and is neither faulted nor completed (i.e. still actively looping); `No` otherwise.
- **Startup Status**: one of `NotRegistered`, `NotStarted`, `Started`, `Faulted`, `Stopped` — a coarse state machine derived purely from `ExecuteTask`'s state (`null` / running / `IsFaulted` / `IsCompleted`).
- **Health**: `Healthy` when `Started`, `Unhealthy` for any other state (not registered, not started, faulted, or stopped-while-host-is-up).

**No polling is introduced.** This is a single, synchronous, point-in-time read of already-resolved state (`Task.IsFaulted` / `Task.IsCompleted` are property reads, not blocking calls) — not a timer, background loop, or repeated check. The same `Inspect` method is reused by the `/health` and `/health/ready` endpoints (AI12.OBS.6) so a request-time health check and the one-time startup log use identical logic.

**No worker behavior was changed.** `ClassroomRecognitionBackgroundService` and `StudentFaceEmbeddingBackgroundService` were not modified at all — `BackgroundService.ExecuteTask` is a pre-existing public property on the base class from `Microsoft.Extensions.Hosting` that every `BackgroundService` already exposes; this diagnostic simply reads it.

### 3.3 `BackgroundServiceExceptionBehavior` reason

```csharp
var backgroundServiceExceptionBehaviorReason = backgroundServiceExceptionBehavior == "StopHost"
    ? "Production Fail-Fast"
    : "Development Environment";
```

The reason is derived from the already-resolved `IOptions<HostOptions>` value (itself set by the AI11.HARDENING.1 `builder.Environment.IsDevelopment()` branch), not recomputed independently — so the reason can never drift from the actual configured behavior.

---

## 4. Architecture impact

- No new hosted services, middleware, or long-running services were introduced.
- No changes to `ClassroomRecognitionBackgroundService`, `StudentFaceEmbeddingBackgroundService`, the queues, or the recognition pipeline.
- `Program.cs`'s host startup sequence changed from `app.Run()` to `await app.StartAsync(); ...; await app.WaitForShutdownAsync();` — functionally identical, required only to observe genuine post-start worker state.
- New file `Abhyanvaya.API/Diagnostics/BackgroundWorkerInspector.cs` (shared by the startup log and the AI12.OBS.6 health endpoints).

---

## 5. Verification steps

1. **Build:** `dotnet build Abhyanvaya.sln` — 0 errors.
2. **Single execution:** confirm the summary (including both worker blocks) is logged exactly once per process start.
3. **Healthy state:** on a normal startup, confirm both workers report `Registered: Yes`, `Running: Yes`, `Startup Status: Started`, `Health: Healthy`.
4. **Environment check:** run with `ASPNETCORE_ENVIRONMENT=Development` and confirm `Reason: Development Environment`; run with a non-Development value and confirm `Reason: Production Fail-Fast`.
5. **Faulted worker (manual test):** temporarily throw an unhandled exception from inside `ClassroomRecognitionBackgroundService.ExecuteAsync` (outside its per-job `try/catch`) and confirm a subsequent request to `/health` (AI12.OBS.6) reports `Startup Status: Faulted`, `Health: Unhealthy` for that worker — without needing to restart the process to observe the change (undo the test change afterward).
6. **No regression:** confirm application startup time, HTTP endpoint availability, and existing upload/recognition functionality are unchanged.

---

## 6. Files created/modified

| File | Change |
|------|--------|
| `Abhyanvaya.API/Diagnostics/BackgroundWorkerInspector.cs` | New — `BackgroundWorkerStatus` record + `BackgroundWorkerInspector.Inspect(...)`, derives Registered/Running/StartupStatus/Health from the live `IHostedService`/`BackgroundService.ExecuteTask`. |
| `Abhyanvaya.API/Program.cs` | Startup sequence changed to `StartAsync()` → log summary → `WaitForShutdownAsync()`; summary now logs rich per-worker diagnostics and the `BackgroundServiceExceptionBehavior` reason. (This file also contains the AI12.OBS.2–5 changes and the AI12.OBS.6 health endpoints — see their respective documents.) |

---

## 7. Build status

`dotnet build Abhyanvaya.sln` — **Build succeeded**, 0 errors.

## 8. Acceptance criteria

- ✅ Startup summary executes exactly once (single call site, before `WaitForShutdownAsync()`).
- ✅ Build succeeds.
- ✅ No business logic, queue, recognition, or controller changes.
