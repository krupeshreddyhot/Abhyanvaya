# AI11.BG.1 — Background Worker Registration Review

**Status: VERIFIED — CORRECTLY REGISTERED (no code changes required)**
**Review date:** 2026-07-04
**Reviewer:** Chief Architect

---

## Scope

Verify that the AI Recognition background worker(s) are registered as `IHostedService`/`BackgroundService` instances and actually start when `Abhyanvaya.API` boots, per the AI11.BG.1 checklist.

---

## Files reviewed

| File | Role |
|------|------|
| `Abhyanvaya.API/Program.cs` | Composition root — calls `AddInfrastructure` |
| `Abhyanvaya.Infrastructure/DependencyInjection.cs` | Registers hosted services |
| `Abhyanvaya.Infrastructure/BackgroundWorkers/ClassroomRecognitionBackgroundService.cs` | AI classroom photo recognition worker |
| `Abhyanvaya.Infrastructure/BackgroundWorkers/StudentFaceEmbeddingBackgroundService.cs` | Student face embedding worker |
| `Abhyanvaya.Infrastructure/Recognition/InMemoryClassroomPhotoQueue.cs` | Channel-backed queue feeding the recognition worker |
| `Abhyanvaya.Infrastructure/Embedding/InMemoryStudentPhotoEmbeddingQueue.cs` | Channel-backed queue feeding the embedding worker |
| `Abhyanvaya.Application/AttendancePhotoService.cs`, `AttendanceSessionCreator.cs` | Enqueue classroom photo jobs after upload commit |
| `Abhyanvaya.Application/StudentFaceEmbeddingService.cs` | Enqueues student embedding jobs |

## Search terms checked

`IHostedService`, `BackgroundService`, `RecognitionWorker`, `AttendanceRecognitionWorker`, `RecognitionQueueWorker`, `ChannelReader`, `QueueProcessor`, `Task.Run`, hosted background workers.

Result: the only matching background-processing types in the codebase are `ClassroomRecognitionBackgroundService` and `StudentFaceEmbeddingBackgroundService`. No `RecognitionWorker`, `AttendanceRecognitionWorker`, `RecognitionQueueWorker`, or ad-hoc `Task.Run`-based background loops exist anywhere in the solution.

---

## Findings

### 1. Registration site

```53:54:Abhyanvaya.Infrastructure/DependencyInjection.cs
services.AddHostedService<StudentFaceEmbeddingBackgroundService>();
services.AddHostedService<ClassroomRecognitionBackgroundService>();
```

`AddInfrastructure(...)` is called **exactly once**, from `Program.cs`:

```71:71:Abhyanvaya.API/Program.cs
builder.Services.AddInfrastructure(builder.Configuration);
```

No other call site registers `AddHostedService<ClassroomRecognitionBackgroundService>` or `AddHostedService<StudentFaceEmbeddingBackgroundService>` anywhere in the solution (verified via full-text search). Each worker type is registered **exactly once**.

### 2. Automatic startup

Both workers derive from `Microsoft.Extensions.Hosting.BackgroundService`, which implements `IHostedService`. `WebApplication.Build()` uses the generic host under the hood, which automatically calls `StartAsync` on every registered `IHostedService` when `app.Run()` executes — no manual `Start()`/`Task.Run` bypass exists. Startup is automatic and requires no additional wiring.

### 3. Startup logging

```27:27:Abhyanvaya.Infrastructure/BackgroundWorkers/ClassroomRecognitionBackgroundService.cs
_logger.LogInformation("Classroom recognition background worker started.");
```

```33:33:Abhyanvaya.Infrastructure/BackgroundWorkers/StudentFaceEmbeddingBackgroundService.cs
_logger.LogInformation("Student face embedding background worker started.");
```

Both `ExecuteAsync` overrides log a startup message as the first statement, before entering the processing loop.

### 4. Continuous looping

Both workers consume an unbounded `System.Threading.Channels.Channel<T>` via an `await foreach` over `IAsyncEnumerable<T>` (`DequeueAllAsync`), which blocks on `ChannelReader.WaitToReadAsync` / `ReadAllAsync` until new work arrives or the token is cancelled. This is a continuous, non-polling, allocation-light loop — not a one-shot execution.

- `ClassroomRecognitionBackgroundService.ExecuteAsync` → `await foreach (var message in _queue.DequeueAllAsync(stoppingToken))`
- `StudentFaceEmbeddingBackgroundService.ExecuteAsync` → `await foreach (var message in _queue.DequeueAllAsync(stoppingToken))`

Each iteration creates a fresh DI scope (`_scopeFactory.CreateAsyncScope()`) per job and catches/logs exceptions per-item so one bad job cannot crash the worker loop or stop subsequent processing.

### 5. Cancellation token support

`ExecuteAsync(CancellationToken stoppingToken)` — the token supplied by the host is threaded through to:
- `_queue.DequeueAllAsync(stoppingToken)` (channel read cancellation)
- `pipeline.ProcessAsync(message, stoppingToken)` / `pipeline.GenerateAsync(message, cancellationToken)` (per-job cancellation)

On host shutdown, `BackgroundService.StopAsync` signals `stoppingToken`, which unblocks `WaitToReadAsync`/`ReadAllAsync` and lets `ExecuteAsync` return gracefully.

### 6. No duplicate workers

- Each `AddHostedService<T>()` call appears exactly once.
- No competing worker classes (`RecognitionWorker`, `AttendanceRecognitionWorker`, `RecognitionQueueWorker`) exist.
- No `Task.Run`-based fire-and-forget processing loops exist outside the hosted services.
- `AddInfrastructure` itself is invoked exactly once from the composition root.

### 7. End-to-end wiring confirmed

- `AttendanceSessionCreator` / `AttendancePhotoService` call `QueueProcessingAsync` → `IClassroomPhotoQueue.EnqueueAsync` after the upload transaction commits, which is consumed by `ClassroomRecognitionBackgroundService`.
- `StudentFaceEmbeddingService` calls `IStudentPhotoEmbeddingQueue.EnqueueAsync`, consumed by `StudentFaceEmbeddingBackgroundService`.

---

## Checklist result

| Requirement | Result |
|-------------|--------|
| Worker is registered exactly once | ✅ Pass |
| Worker starts automatically when API starts | ✅ Pass |
| Worker logs startup | ✅ Pass |
| Worker loops continuously | ✅ Pass (channel-backed `await foreach`) |
| Worker supports cancellation token | ✅ Pass |
| No duplicate workers | ✅ Pass |

---

## Conclusion

Both AI-related background workers (`ClassroomRecognitionBackgroundService` for AI photo recognition, `StudentFaceEmbeddingBackgroundService` for face embedding generation) are registered correctly, exactly once, via `Abhyanvaya.Infrastructure.DependencyInjection.AddInfrastructure`, which is called exactly once from `Program.cs`. They start automatically with the API host, log on startup, loop continuously over an in-memory `Channel<T>`-backed queue, and respect the host's cancellation token on shutdown.

**No missing registration, no misconfiguration, and no duplicate registrations were found. No code changes were required.**
