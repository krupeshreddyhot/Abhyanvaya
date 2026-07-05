# AI11.HARDENING.1 — Environment-Specific `BackgroundServiceExceptionBehavior`

**Status: IMPLEMENTED**
**Date:** 2026-07-04
**Reviewer:** Chief Architect

---

## 1. Objective

Configure `HostOptions.BackgroundServiceExceptionBehavior` so it differs by environment:

| Environment | Behavior | Rationale |
|-------------|----------|-----------|
| Development | `Ignore` | Developer productivity — a background worker exception must not kill the API mid-debug session. |
| Production  | `StopHost` | Fail-fast reliability — a critical, unhandled background failure should stop the process so the host platform restarts it. |

---

## 2. Background

This setting governs what `Microsoft.Extensions.Hosting.Host` does when a registered `BackgroundService.ExecuteAsync` throws an exception that escapes its own error handling:

- `BackgroundServiceExceptionBehavior.StopHost` (the framework default) — the host logs the failure as `Critical` and calls `IHostApplicationLifetime.StopApplication()`, terminating the whole process.
- `BackgroundServiceExceptionBehavior.Ignore` — the host logs the failure but keeps running; the failed `BackgroundService` simply stops looping (it is **not** automatically restarted).

Abhyanvaya has two long-running `BackgroundService` hosted services (`ClassroomRecognitionBackgroundService`, `StudentFaceEmbeddingBackgroundService`) that consume in-memory queues. A defect in job-processing code (as happened in AI11.BG.2/AI11.BG.3 — see §5) can throw from inside `ExecuteAsync`'s loop. Prior to this change the app always used `Ignore` (added as an emergency fix for that incident); this milestone makes the behavior intentional and environment-aware instead of a blanket override.

---

## 3. Implementation

`Abhyanvaya.API/Program.cs`:

```csharp
// Background worker failure policy: Development favors developer productivity (keep the host
// alive so a single bad job doesn't kill the debug session); Production favors fail-fast so an
// orchestrator (IIS / Kubernetes / Docker / Azure App Service) can detect and restart the process.
builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = builder.Environment.IsDevelopment()
        ? BackgroundServiceExceptionBehavior.Ignore
        : BackgroundServiceExceptionBehavior.StopHost;
});
```

- Uses **only** `builder.Environment.IsDevelopment()` — no hardcoded environment name strings (`"Development"`, `"Production"`, etc. never appear as comparison literals).
- `IWebHostEnvironment.IsDevelopment()` reads `ASPNETCORE_ENVIRONMENT` (or `DOTNET_ENVIRONMENT`) under the hood, so behavior automatically follows however each deployment target sets that variable — no per-environment code branches needed elsewhere.
- No changes were made to background workers, the queue implementations, the recognition pipeline, DI registrations, hosted service registration, logging categories, or any business logic — this is purely a `HostOptions` policy change.

---

## 4. Why `StopHost` is correct for production

Microsoft's guidance for `BackgroundService` (see [.NET docs: BackgroundService base class](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers) and the `Host.CreateDefaultBuilder` remarks on `BackgroundServiceExceptionBehavior`) is that an unhandled exception in a background service typically indicates a program state the service cannot safely recover from on its own. Recommended practice is:

- Handle **expected, recoverable** failures (bad input, transient I/O errors, a single malformed job) *inside* the service's own loop with `try/catch` around each unit of work — which Abhyanvaya's workers already do (see `docs/AI11_BG2_QUEUE_REVIEW.md` and `docs/AI11_BG3_PIPELINE_TRACE.md`).
- Let **unexpected, systemic** failures (a bug that escapes the per-job `try/catch`, resource exhaustion, a corrupted internal state) propagate and stop the host, because continuing to run a process with a dead worker thread silently drops all future AI recognition jobs with no operator visibility.

In production, Abhyanvaya runs behind a process supervisor (IIS Application Pool recycling, a Kubernetes `Deployment`/`Pod` restart policy, Docker's restart policy, or Azure App Service's process monitor). All of these are designed to detect an exited process and restart it automatically. `StopHost` turns a silently-degraded worker into a visible process restart + alertable log line (`Critical: The HostOptions.BackgroundServiceExceptionBehavior is configured to StopHost...`), which is far safer than an API that looks healthy (HTTP endpoints still respond) while silently no longer processing any classroom photo or embedding jobs.

---

## 5. Why `Ignore` is useful during development

During local debugging in Visual Studio:

- A developer iterating on the recognition pipeline, queue, or matching logic will frequently hit exceptions while a fix is in progress.
- Under `StopHost`, every such exception kills the entire debug session (all controllers, Swagger, the whole API) — not just the affected worker — which is highly disruptive and unrelated to whatever the developer was actually testing (e.g. a controller endpoint unrelated to recognition).
- This is exactly what happened in practice: a bug in `InMemoryClassroomPhotoQueue.Count` (see `docs/AI11_BG2_QUEUE_REVIEW.md`, `docs/AI11_BG3_PIPELINE_TRACE.md`) threw `NotSupportedException` inside `ClassroomRecognitionBackgroundService.ExecuteAsync`, and because the host was defaulting to `StopHost`, the entire `Abhyanvaya.API.exe` process exited every time a classroom photo was uploaded — even though the HTTP request itself had already completed successfully.
- `Ignore` lets the developer see the error logged (`Error: BackgroundService failed`) and keep working against the rest of the API while diagnosing and fixing the worker in isolation.

---

## 6. Microsoft `BackgroundService` recommendations (summary)

From the official ASP.NET Core / .NET Generic Host guidance:

1. `BackgroundService.ExecuteAsync` should run for the lifetime of the application; unhandled exceptions there are treated as fatal to that service.
2. `HostOptions.BackgroundServiceExceptionBehavior` exists specifically to let applications choose between "fail fast" (`StopHost`, the default) and "log and continue" (`Ignore`) at the **host** level.
3. Ignore does **not** restart the failed `BackgroundService` — the guidance is that if a service can safely recover, it should catch and handle the exception itself inside its own loop (which Abhyanvaya's workers do for expected per-job failures); `HostOptions` only governs what happens when that per-job handling is itself insufficient.
4. Production systems should generally prefer `StopHost` combined with an external process supervisor/orchestrator restart policy, rather than relying on `Ignore` to paper over defects.

This implementation follows that guidance directly: environment-appropriate defaults, with the workers' own internal `try/catch` (per AI11.BG.2/AI11.BG.3 hardening) remaining the first line of defense for expected failures.

---

## 7. Verification steps

1. **Build:** `dotnet build Abhyanvaya.sln` — succeeds with 0 errors (verified below in the final report).
2. **Development behavior check:**
   - Run the API with `ASPNETCORE_ENVIRONMENT=Development` (the default for `dotnet run`/F5 in Visual Studio).
   - On startup, confirm the log line `BackgroundServiceExceptionBehavior  : Ignore` appears (emitted by the AI11.HARDENING.2 startup summary — see `docs/AI11_HARDENING_STARTUP_LOGGING.md`).
   - Trigger a background worker exception (e.g. temporarily throw inside `ClassroomRecognitionBackgroundService.ExecuteAsync`) and confirm the API keeps responding to other HTTP endpoints afterward.
3. **Production behavior check:**
   - Run with `ASPNETCORE_ENVIRONMENT=Production` (or any non-Development value).
   - Confirm the startup log line reads `BackgroundServiceExceptionBehavior  : StopHost`.
   - Trigger the same worker exception and confirm the host logs `Critical: ... StopHost ...` and the process exits — verifying the orchestrator would see a process restart.
4. **No hardcoded environment strings:** confirmed by code inspection — the only environment check in this change is `builder.Environment.IsDevelopment()`.
5. **No regression to unrelated code:** confirmed — no changes to background workers, queue implementations, `ClassroomRecognitionPipeline`, DI registrations (`AddInfrastructure`, `AddApplication`), hosted service registration, logging configuration, or controllers.

---

## 8. Files modified

| File | Change |
|------|--------|
| `Abhyanvaya.API/Program.cs` | `HostOptions.BackgroundServiceExceptionBehavior` is now resolved from `builder.Environment.IsDevelopment()` instead of being hardcoded to `Ignore`. (This file also contains the AI11.HARDENING.2 startup summary logging — see that document for details.) |

No other files were modified for this milestone.

---

## 9. Build status

`dotnet build Abhyanvaya.sln` — **Build succeeded**, 0 errors.

## 10. Architecture impact

None. This is a `HostOptions` policy configuration change only:

- No changes to background workers, queue implementations, the recognition pipeline, DI registrations, hosted service registration, logging, or business logic.
- No new dependencies introduced.
- Fully backward compatible — Development behavior is unchanged from the prior emergency fix (`Ignore`); only Production now correctly fails fast instead of silently continuing with a dead worker.
