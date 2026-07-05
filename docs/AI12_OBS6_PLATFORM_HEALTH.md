# AI12.OBS.6 — Platform Health Endpoints

**Status: IMPLEMENTED**
**Date:** 2026-07-04
**Reviewer:** Chief Software Architect

---

## 1. Objective

Introduce production-ready, lightweight health endpoints — `GET /health`, `GET /health/live`, `GET /health/ready` — reusing the same diagnostics helpers (`BackgroundWorkerInspector`, `ModelAvailabilityChecker`, `StartupDiagnostics`) introduced for the startup summary in AI12.OBS.1–5, so the startup log and the runtime health endpoints can never disagree about the platform's state.

None of these endpoints execute recognition, load ONNX models, or perform any write operation. They are implemented as **minimal API endpoints** (`app.MapGet(...)` in `Program.cs`), not controllers, per the milestone's constraints.

---

## 2. Endpoints

### 2.1 `GET /health/live`

A trivial liveness probe: if the process can respond to this request at all, it is alive. No dependency checks (database, storage, models) are performed here by design — that is what `/health/ready` is for. Intended for orchestrator liveness probes (e.g. Kubernetes `livenessProbe`) that should only restart the container if the process itself is wedged, not if a downstream dependency is temporarily unavailable.

**Always returns `200 OK`** while the process is running.

```json
{
  "type": "https://abhyanvaya.app/health/live",
  "title": "Live",
  "status": 200,
  "detail": "The Abhyanvaya API process is running."
}
```

### 2.2 `GET /health/ready`

A readiness probe verifying the platform can actually serve recognition-related traffic:

| Check | How | Lightweight? |
|-------|-----|----------------|
| Database reachable | `ApplicationDbContext.Database.CanConnectAsync(ct)` | Yes — EF Core's standard, minimal "can I open a connection" check; no query against application tables. |
| Storage reachable | `IStorageProvider.CheckHealthAsync(ct)` (the exact same method `MediaStorageService.CheckStorageHealthAsync` already used) | Yes — for local storage this checks directory accessibility; for S3 it performs a tiny put+delete round trip. No new code was written for this — it's the pre-existing provider health check, reused. |
| Models present | `ModelAvailabilityChecker.Check(insightFaceOptions).AllModelsPresent` | Yes — `File.Exists`/`FileInfo.Length` only, shared with AI12.OBS.5. |
| Background workers started | `BackgroundWorkerInspector.Inspect(...).Running` for both workers | Yes — reads `BackgroundService.ExecuteTask` state, shared with AI12.OBS.1. |
| Queue available | Resolve `IClassroomPhotoQueue`/`IStudentPhotoEmbeddingQueue` from DI and read `.Count` (a `Volatile.Read` on an `int`, per AI11.BG.2) without throwing | Yes — no I/O, just confirms the queue services are registered and responsive. |

Returns `200 OK` with `title: "Ready"` if **all** checks pass, or `503 Service Unavailable` with `title: "NotReady"` and a `checks` extension object detailing which check(s) failed:

```json
{
  "type": "https://abhyanvaya.app/health/ready",
  "title": "NotReady",
  "status": 503,
  "detail": "One or more readiness checks failed; see the 'checks' extension for details.",
  "checks": {
    "database": "Reachable",
    "storage": "Unreachable",
    "modelsPresent": true,
    "recognitionWorkerStarted": true,
    "embeddingWorkerStarted": true,
    "queueAvailable": true
  }
}
```

The `503` status code is intentional and standard practice for Kubernetes/load-balancer `readinessProbe` integration: it signals "temporarily remove this instance from the traffic pool" without killing the process (that decision is left to `/health/live` + the orchestrator's restart policy).

### 2.3 `GET /health`

A comprehensive, human/dashboard-facing snapshot combining everything above plus descriptive metadata. Always returns `200 OK` (this endpoint is for visibility/diagnostics, not traffic-gating — see §4 for why `/health` and `/health/ready` have different status-code philosophies), with an `overallStatus` field (`Healthy`/`Degraded`) inside the payload for consumers that want to alert on it.

```json
{
  "type": "https://abhyanvaya.app/health",
  "title": "Healthy",
  "status": 200,
  "detail": "Abhyanvaya platform health snapshot.",
  "health": {
    "environment": "Development",
    "version": "1.0.0.0",
    "database": { "provider": "PostgreSQL", "status": "Healthy" },
    "storage": { "provider": "Local File System", "status": "Healthy" },
    "recognitionEngine": "InsightFace",
    "detectionModel": { "fileName": "det_10g.onnx", "status": "Found", "sizeMB": 17.4 },
    "embeddingModel": { "fileName": "w600k_r50.onnx", "status": "Found", "sizeMB": 123.6 },
    "recognitionWorker": { "status": "Healthy", "running": true },
    "embeddingWorker": { "status": "Healthy", "running": true },
    "backgroundQueue": { "classroomPhotoQueueDepth": 0, "embeddingQueueDepth": 0 },
    "cache": { "provider": "Memory" },
    "overallStatus": "Healthy"
  }
}
```

This directly satisfies the milestone's required field list: Environment, Version, Database, Storage, Recognition Engine, Detection Model, Embedding Model, Recognition Worker, Embedding Worker, Background Queue, Cache, Overall Status.

---

## 3. RFC7807 compatibility

Every response body is a `Microsoft.AspNetCore.Mvc.ProblemDetails` object serialized as `application/problem+json`, with the standard RFC7807 members (`type`, `title`, `status`, `detail`) always present, and endpoint-specific diagnostic data attached via the RFC7807 `extensions` mechanism (`checks` for `/health/ready`, `health` for `/health`) — which is exactly what RFC7807 §3.2 prescribes for problem-type-specific data ("extension members"). This reuses `ProblemDetails`, the same type the application's `GlobalExceptionHandler`/`AddProblemDetails()` already use for error responses, so health payloads follow the same conventions as the rest of the API.

---

## 4. Why `/health` and `/health/ready` use different status-code strategies

- `/health/ready` is meant to be machine-consumed by an orchestrator/load balancer to decide whether to route traffic to this instance — a `503` is the actionable, standard signal for "not ready", and orchestrators specifically look for non-2xx codes here.
- `/health` is meant to be a diagnostic dashboard/snapshot for humans and monitoring tools that want the *full picture* even when something is degraded (e.g. Grafana/Datadog scraping this on an interval to chart `overallStatus` over time) — returning a non-200 here would make many simple HTTP monitoring tools treat the endpoint itself as "down" and discard the body, which is the opposite of what's wanted for a diagnostics endpoint. The `overallStatus`/`title` field inside the body carries the actual health signal instead.

---

## 5. What is explicitly *not* done (per the milestone's constraints)

- **No recognition execution** — no code path in any of the three endpoints calls `IClassroomRecognitionPipeline`, `IFaceDetectionService.Detect(...)`, or `IFaceMatcher.Match(...)`.
- **No ONNX model loading** — `ModelAvailabilityChecker` never constructs an `InferenceSession`; `InsightFaceOnnxModelHost` is not referenced by any health endpoint.
- **No controllers were added or modified** — all three endpoints are minimal API routes registered directly on `WebApplication` in `Program.cs` via `MapPlatformHealthEndpoints(app)`, called once alongside `app.MapControllers()`.
- **No new hosted services or middleware** — endpoints are plain request-handling delegates, invoked only when a client calls them (not a background timer/poller).

---

## 6. Architecture impact

- No new DI registrations were required for the endpoints themselves; they resolve existing scoped/singleton services (`ApplicationDbContext`, `IStorageProviderFactory`, `IFaceDetectionService`, `IOptions<InsightFaceOptions>`, `IClassroomPhotoQueue`, `IStudentPhotoEmbeddingQueue`) via a short-lived `IServiceScope` created per request (`services.CreateScope()`), exactly like the startup summary does.
- All dependency checks (`IsDatabaseReachableAsync`, `IsStorageReachableAsync`, `IsRecognitionQueueAvailable`) wrap their calls in `try/catch`, so a downstream outage (e.g. database unreachable) degrades the reported status without throwing an unhandled exception out of the endpoint.
- No authentication/authorization was added to these endpoints — they are intended for infrastructure/orchestrator use (Kubernetes probes, load balancer health checks, uptime monitors). If public exposure is a concern for a given deployment, restrict access at the reverse-proxy/ingress level (a standard operational practice for health endpoints, since requiring JWT auth would break liveness/readiness probes that can't authenticate).

---

## 7. Verification steps

1. **Build:** `dotnet build Abhyanvaya.sln` — 0 errors.
2. **Liveness:** `curl -i http://localhost:<port>/health/live` → expect `200 OK`, `application/problem+json`, `title: "Live"`.
3. **Readiness (healthy):** with DB/storage/models/workers all healthy, `curl -i http://localhost:<port>/health/ready` → expect `200 OK`, `title: "Ready"`, all `checks` values indicating success.
4. **Readiness (degraded):** stop the PostgreSQL service (or point the connection string at an unreachable host) and re-request `/health/ready` → expect `503 Service Unavailable`, `title: "NotReady"`, `checks.database: "Unreachable"`.
5. **Full snapshot:** `curl -i http://localhost:<port>/health` → expect `200 OK` with the full `health` object; confirm `detectionModel`/`embeddingModel` sizes match the actual files on disk, and `backgroundQueue` depths match reality (e.g. upload a photo and observe `classroomPhotoQueueDepth` briefly increase before the worker drains it).
6. **No side effects:** confirm repeated calls to all three endpoints never enqueue jobs, never write to storage, never touch `AttendanceSession`/`AttendanceRecognition` tables, and never load an ONNX model (verify via logs — no `InsightFace {Label} ONNX model loaded from {Path}` log line should appear as a result of calling these endpoints).

---

## 8. Files created/modified

| File | Change |
|------|--------|
| `Abhyanvaya.API/Program.cs` | Added `MapPlatformHealthEndpoints(app)` local function mapping `/health`, `/health/live`, `/health/ready`, plus `IsDatabaseReachableAsync`, `IsStorageReachableAsync`, `IsRecognitionQueueAvailable` helper local functions. Called once, alongside `app.MapControllers()`. |

No other files were created or modified specifically for this sub-milestone (it reuses `BackgroundWorkerInspector`, `ModelAvailabilityChecker`, and `StartupDiagnostics` from AI12.OBS.1/2/4/5, and the existing `IStorageProvider.CheckHealthAsync`).

---

## 9. Build status

`dotnet build Abhyanvaya.sln` — **Build succeeded**, 0 errors.

## 10. Acceptance criteria

- ✅ Build succeeds.
- ✅ Health endpoints implemented and documented (`/health`, `/health/live`, `/health/ready`).
- ✅ RFC7807-compatible (`application/problem+json`, standard `type`/`title`/`status`/`detail` + extensions).
- ✅ No recognition execution, no ONNX model loading — lightweight checks only.
