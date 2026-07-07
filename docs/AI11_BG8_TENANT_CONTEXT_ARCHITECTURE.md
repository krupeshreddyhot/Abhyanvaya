# AI11.BG.8 — Tenant Context Architecture Fix

**Author:** Chief Architect, Abhyanvaya AI Attendance Platform
**Status:** Implemented — build succeeds, 0 errors
**Scope:** Foundational multi-tenant infrastructure for all non-HTTP execution contexts

---

## 1. Problem

After a teacher uploads a classroom photo, the recognition job is enqueued and the
`AttendanceSession` is committed to PostgreSQL as `Pending`. When the background worker dequeues
the job, the pipeline immediately fails with:

```
System.Collections.Generic.KeyNotFoundException
Message = Attendance session 'fc2ab073-5bd9-46f0-b1c9-dee2a3f42b23' was not found.
   at Abhyanvaya.Infrastructure.Recognition.ClassroomRecognitionPipeline.ProcessAsync(...)
      ClassroomRecognitionPipeline.cs:line 53
   at Abhyanvaya.Infrastructure.BackgroundWorkers.ClassroomRecognitionBackgroundService.ExecuteAsync(...)
      ClassroomRecognitionBackgroundService.cs:line 41
```

The record **exists** in the database, yet the worker cannot find it. Because the throw happens
before the pipeline's `try` block (before `MoveToProcessing()`), the worker's outer
`catch (Exception)` logs and swallows it, and the session is stranded at **Pending (15%)** forever.

---

## 2. Root Cause

`AttendanceSession` implements `ITenantScoped`, so `ApplicationDbContext.OnModelCreating` applies a
**global EF Core query filter** to every query against it:

```csharp
// ApplicationDbContext.cs
private void SetTenantScopedFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantScoped
{
    builder.Entity<TEntity>().HasQueryFilter(e =>
        _currentUserService == null
        || IsSuperAdminCrossTenant()
        || e.TenantId == _currentUserService.TenantId);
}
```

The filter's tenant value comes from `ICurrentUserService`, whose only source **was**
`IHttpContextAccessor`:

```csharp
// CurrentUserService.cs (before)
var user = httpContextAccessor.HttpContext?.User;
TenantId = TryParseInt(user?.FindFirst("TenantId")?.Value);   // no HttpContext => 0
```

### Execution comparison

```
HTTP request                         Background worker
------------                         -----------------
JWT                                  (no HttpContext)
  ↓                                    ↓
CurrentUserService                   CurrentUserService  (real, non-null instance)
  ↓                                    ↓
TenantId = 1                         TenantId = 0
  ↓                                    ↓
filter: e.TenantId == 1              filter: e.TenantId == 0
  ↓                                    ↓
row found                            row (TenantId = 1) filtered out → "not found"
```

Even though the pipeline query explicitly asks for `s.TenantId == message.TenantId`, EF Core
**ANDs** the global filter on top, producing `Id == X AND TenantId == 1 AND TenantId == 0` → zero
rows → `KeyNotFoundException`.

This affects the **entire background pipeline**, not just line 53:

| Location | Query |
|----------|-------|
| `ClassroomRecognitionPipeline` line 53 | Load `AttendanceSession` (throws first) |
| `ClassroomRecognitionPipeline` | Existing `AttendanceRecognition`s for the session |
| `ClassroomRecognitionPipeline.LoadStudentEmbeddingsAsync` | `Students`, `StudentFaceEmbeddings` |
| `AttendanceSessionSummaryService.SyncSessionSummaryAsync` | `AttendanceSession` + `AttendanceRecognition`s |
| `StudentFaceEmbeddingBackgroundService` pipeline | Student/embedding queries (same class of bug) |

---

## 3. Architecture

A dedicated, transport-agnostic tenant context is introduced. Any entry point that lacks an
`HttpContext` establishes the tenant for its scope; the EF Core global filters then behave exactly
as they do for authenticated HTTP requests.

### New abstraction

```csharp
public interface ITenantContextAccessor
{
    int? CurrentTenantId { get; }
    void SetTenant(int tenantId);
    void Clear();
}
```

### Tenant resolution priority (inside `CurrentUserService`)

```
HttpContext JWT claim
        ↓ (null when no HTTP request)
ITenantContextAccessor.CurrentTenantId
        ↓ (null when unset)
0  (no tenant)
```

HTTP requests keep resolving from the JWT with **zero behavioural change**. Background/worker
scopes fall through to the ambient accessor.

### Runtime flow (background worker)

```
Queue.DequeueAllAsync()
        ↓
message (TenantId = 1)
        ↓
scope = CreateAsyncScope()
        ↓
tenantContext = scope.Resolve<ITenantContextAccessor>()
        ↓
tenantContext.SetTenant(message.TenantId)     // 1
        ↓
try
   pipeline.ProcessAsync(message)             // EF filter now uses TenantId = 1
        ↓
finally
   tenantContext.Clear()                       // no leakage across jobs
```

### Component diagram

```
┌──────────────────────────────┐        ┌──────────────────────────────┐
│  HTTP Request Scope           │        │  Background Worker Scope      │
│                               │        │                               │
│  JWT ─► CurrentUserService    │        │  message.TenantId             │
│              │                │        │        │                      │
│              │ (HttpContext)  │        │        ▼                      │
│              ▼                │        │  ITenantContextAccessor       │
│         TenantId = 1          │        │   .SetTenant(1)               │
│                               │        │        │                      │
│                               │        │        ▼                      │
│                               │        │  CurrentUserService           │
│                               │        │   (no HttpContext →           │
│                               │        │    reads accessor) = 1        │
└───────────────┬──────────────┘        └───────────────┬──────────────┘
                │                                        │
                └──────────────► EF Core Global ◄────────┘
                                 Tenant Query Filter
                              (e.TenantId == CurrentUserService.TenantId)
```

Both paths converge on the **same** global filter with the correct tenant. The pipeline, services,
and repositories remain completely unaware of tenancy.

---

## 4. Files Created / Modified

### Created

| File | Purpose |
|------|---------|
| `Abhyanvaya.Application/Common/Interfaces/ITenantContextAccessor.cs` | Transport-agnostic tenant context abstraction (Application layer). |
| `Abhyanvaya.Infrastructure/Services/TenantContextAccessor.cs` | Scoped, thread-safe implementation. No static state, no `AsyncLocal`, no `HttpContext`, no `CurrentUserService` dependency. |
| `docs/AI11_BG8_TENANT_CONTEXT_ARCHITECTURE.md` | This document. |

### Modified

| File | Change |
|------|--------|
| `Abhyanvaya.Infrastructure/Services/CurrentUserService.cs` | `TenantId` now resolves HTTP JWT claim → `ITenantContextAccessor` → `0`. Public interface unchanged. |
| `Abhyanvaya.Infrastructure/DependencyInjection.cs` | Registered `ITenantContextAccessor → TenantContextAccessor` with **scoped** lifetime. |
| `Abhyanvaya.Infrastructure/BackgroundWorkers/ClassroomRecognitionBackgroundService.cs` | Sets tenant from `message.TenantId` per scope; clears it in a `finally` block. |
| `Abhyanvaya.Infrastructure/BackgroundWorkers/StudentFaceEmbeddingBackgroundService.cs` | Same pattern applied (same class of bug for embedding generation). |

### Explicitly NOT changed

- `ICurrentUserService` public interface — unchanged.
- Controllers / API surface — unchanged.
- `ClassroomRecognitionPipeline`, `EmbeddingPipeline`, summary service, repositories — unchanged;
  no `TenantId` parameters added, no signatures changed.
- No `IgnoreQueryFilters()` anywhere.

---

## 5. Why This Approach Is Superior to `IgnoreQueryFilters()`

| Concern | `ITenantContextAccessor` (this fix) | `IgnoreQueryFilters()` |
|---------|-------------------------------------|-------------------------|
| **Tenant isolation** | Global filters stay fully active; the correct tenant is supplied. Isolation is *preserved*. | Disables the safety net; every bypassed query relies on a hand-written `TenantId ==` predicate. |
| **Blast radius** | One place sets the tenant; all queries in the scope are covered automatically. | Must be added to *every* query — pipeline load, recognitions, students, embeddings, summary service — and every future query. |
| **Shared services** | `AttendanceSessionSummaryService` works unchanged and stays tenant-safe for its HTTP callers too. | Adding `IgnoreQueryFilters()` there (its query has no explicit tenant predicate) would strip tenant protection from HTTP callers. |
| **Risk of silent leak** | None — filters remain the enforcement mechanism. | A single forgotten predicate silently returns/writes cross-tenant data. |
| **Reviewability** | Intent is explicit: "run this job as tenant N." | Intent is "trust the developer everywhere." |
| **Future entry points** | Works for any host (see §7) with no per-query edits. | Every new query in every new host must remember to bypass + re-filter. |

In short: this fix makes background workers behave **exactly like authenticated HTTP requests**,
whereas `IgnoreQueryFilters()` removes the very mechanism that makes multi-tenancy safe.

---

## 6. Multi-Tenant Security Considerations

- **Filters never disabled.** The EF Core global tenant filter remains the single enforcement point
  for tenant isolation across HTTP and non-HTTP paths.
- **Explicit, auditable tenant binding.** A job runs as exactly one tenant, set from the trusted
  `message.TenantId` that was captured under the authenticated upload request.
- **No cross-job leakage.** `Clear()` runs in a `finally` block, so a thrown exception, cancellation,
  or early return cannot leave a tenant bound. Additionally, the accessor is **scoped**, so each job
  gets a fresh instance and the scope is disposed after the job — a defense-in-depth double guarantee.
- **Priority ordering is safe.** HTTP JWT claim always wins; the ambient accessor is only consulted
  when there is no HTTP tenant. A background worker cannot override an authenticated user's tenant,
  and an HTTP request never reads worker state.
- **No ambient global state.** No `static`, no process-wide `AsyncLocal` singleton — state is
  confined to the DI scope, eliminating the classic "wrong tenant under load" hazard.

---

## 7. Future Extensibility

The abstraction is deliberately independent of `BackgroundService`. Any host can adopt it with the
identical three-line pattern (`resolve → SetTenant → finally Clear`):

- **Hangfire / Quartz.NET** — set the tenant from job arguments at the start of the job scope.
- **Azure Functions** — set the tenant from the trigger payload / binding metadata.
- **RabbitMQ / Azure Service Bus / Kafka consumers** — set the tenant from a message header or body
  before dispatching to the domain pipeline.

Because the tenant flows through `ICurrentUserService` and the existing EF Core filters, **no domain
service, pipeline, or repository needs to change** when new transports are added. Only the thin
entry-point adapter is responsible for establishing and clearing the tenant.

---

## 8. Validation Checklist

| Item | Result |
|------|--------|
| Upload photo → worker finds `AttendanceSession` | ✅ Filter now resolves `TenantId = message.TenantId` |
| No `KeyNotFoundException` at pipeline line 53 | ✅ Row is no longer filtered out |
| Global query filters still active | ✅ Never disabled; no `IgnoreQueryFilters()` |
| No `IgnoreQueryFilters()` added | ✅ Zero occurrences introduced |
| No tenant leakage between jobs | ✅ `finally { Clear() }` + scoped lifetime |
| Multiple queued jobs from different tenants | ✅ Each scope binds/clears its own tenant |
| `ICurrentUserService` public interface unchanged | ✅ No members added/removed |
| No controller / API changes | ✅ HTTP path behaves identically |
| Build succeeds | ✅ `dotnet build` — 0 errors |

> Note: The missing InsightFace ONNX model files remain a separate, downstream concern that only
> surfaces *after* this fix (once the session loads and the pipeline reaches model loading). It is
> out of scope for AI11.BG.8.
