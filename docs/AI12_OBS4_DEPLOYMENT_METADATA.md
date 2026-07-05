# AI12.OBS.4 — SaaS Deployment Metadata

**Status: IMPLEMENTED**
**Date:** 2026-07-04
**Reviewer:** Chief Software Architect

---

## 1. Objective

Extend the startup diagnostics with SaaS deployment metadata — tenant mode, deployment tier, database provider, cache provider, media provider, and recognition queue implementation — all read from actual configuration/DI, no hardcoded values.

---

## 2. Fields and their real source

| Field | Source | Notes |
|-------|--------|-------|
| Tenant Mode | `IConfiguration["Tenancy:Mode"]` (new key, added to `appsettings.json`) | `SingleTenant` → `Single Tenant`; anything else (including unset) → `Multi Tenant`, matching the platform's actual default architecture (see §3). |
| Deployment | `app.Environment.EnvironmentName` | Same underlying value as "Application Environment" (from AI11.HARDENING.2) — see §4 for why both are kept. |
| Database Provider | `ApplicationDbContext.Database.ProviderName`, mapped via `StartupDiagnostics.DescribeDatabaseProvider(...)` | Unchanged from AI11.HARDENING.2 — reused, not reimplemented. |
| Cache Provider | `IConfiguration.GetValue<bool>("UseRedis")` — the exact same configuration flag `Program.cs` already uses to decide between `AddStackExchangeRedisCache` and `AddDistributedMemoryCache` | `true` → `Redis`, `false` → `Memory`. No new configuration key was introduced; this reuses the single existing source of truth so the diagnostic can never disagree with the actual registered cache implementation. |
| Media Provider | `IStorageProvider.DisplayName` (see AI12.OBS.2) | Logged as `Media Provider` (the AI12.OBS.2 doc explains why the old `Storage Provider` label was consolidated into this one). |
| Recognition Queue | Concrete type of the registered `IClassroomPhotoQueue`, resolved from DI and mapped via `StartupDiagnostics.DescribeQueueImplementation(...)` | Today always resolves to `InMemory Channel` (the only registered implementation, `InMemoryClassroomPhotoQueue`, backed by `System.Threading.Channels.Channel<T>`). See §5 for forward-compatibility with RabbitMQ/Azure Queue. |

---

## 3. Tenant Mode: why `Multi Tenant` and why a new configuration key was needed

Abhyanvaya's domain model already scopes virtually every entity by `TenantId` (colleges/universities, students, attendance sessions, staff, etc.), enforced via EF Core global query filters and `TenantAccessGuard` — this is an architectural fact of the current codebase, not something that varies at runtime today. However, there was no existing configuration key that captured this as an explicit, loggable setting (the previous milestones never needed one).

Per the "no hardcoding" requirement, `Program.cs` does not simply print a literal `"Multi Tenant"` string — it reads `Tenancy:Mode` from configuration:

```json
"Tenancy": {
  "Mode": "MultiTenant"
}
```

added to `Abhyanvaya.API/appsettings.json`, with `Program.cs` mapping any value that isn't exactly `SingleTenant` (case-insensitive) to `Multi Tenant`. This makes tenancy mode an explicit, environment-overridable configuration value (e.g. a future single-tenant on-prem deployment could set `Tenancy__Mode=SingleTenant` via environment variable without any code change) rather than an assumption baked into `Program.cs`.

---

## 4. Deployment vs. Application Environment

Both `Application Environment` (AI11.HARDENING.2) and `Deployment` (this milestone) currently read the same underlying value, `app.Environment.EnvironmentName` (itself sourced from `ASPNETCORE_ENVIRONMENT`/`DOTNET_ENVIRONMENT`). They are intentionally kept as two separate, clearly-labeled log lines rather than deduplicated, because:

- `Application Environment` is the precise ASP.NET Core hosting term, useful when cross-referencing `IWebHostEnvironment` behavior (e.g. `IsDevelopment()` branches).
- `Deployment` is the SaaS/ops-facing term requested by this milestone (`Development` / `Staging` / `Production`), useful for dashboards and alerting that group by deployment tier rather than framework terminology.
- ASP.NET Core's `EnvironmentName` is a free-form string, not an enum restricted to `Development`/`Production` — `Staging` (or any custom tier name) is already fully supported without any code change, satisfying the three example values in the milestone spec.

---

## 5. Recognition Queue: forward-compatible without code changes

```csharp
public static string DescribeQueueImplementation(object queueInstance) => queueInstance.GetType().Name switch
{
    nameof(InMemoryClassroomPhotoQueue) => "InMemory Channel",
    nameof(InMemoryStudentPhotoEmbeddingQueue) => "InMemory Channel",
    _ => queueInstance.GetType().Name,
};
```

If a future milestone introduces a `RabbitMqClassroomPhotoQueue` or `AzureQueueClassroomPhotoQueue` implementing `IClassroomPhotoQueue`, this diagnostic will automatically report that type's name (e.g. `RabbitMqClassroomPhotoQueue`) without any change to `Program.cs` — it would just be a nicer, mapped label if a friendly name is added to the `switch`. This satisfies the milestone's forward-looking "Future RabbitMQ / Future Azure Queue" labels as a design property (the mechanism already generalizes), not as unused placeholder code.

---

## 6. Sample startup output (new lines only)

```
Tenant Mode                         : Multi Tenant
Deployment                          : Development
Database Provider                   : PostgreSQL
Cache Provider                      : Memory
Recognition Queue                   : InMemory Channel
```

(`Media Provider` is logged earlier in the summary, alongside the other engine/provider fields — see AI12.OBS.2.)

---

## 7. Architecture impact

- One new configuration section (`Tenancy:Mode`) was added to `appsettings.json` — purely additive, with a safe default (`MultiTenant`) matching current architecture; nothing depends on it functionally yet (diagnostics-only).
- No new DI registrations — `IClassroomPhotoQueue` was already registered as a singleton.
- No changes to caching behavior, database configuration, or queue processing — this milestone only *reports* on already-existing configuration/registrations.

---

## 8. Verification steps

1. **Build:** `dotnet build Abhyanvaya.sln` — 0 errors.
2. **Tenant mode:** confirm `Tenant Mode : Multi Tenant` by default; set `Tenancy:Mode=SingleTenant` in configuration and confirm the log switches to `Tenant Mode : Single Tenant`.
3. **Deployment:** change `ASPNETCORE_ENVIRONMENT` and confirm `Deployment` tracks it identically to `Application Environment`.
4. **Cache provider:** toggle `UseRedis` between `true`/`false` and confirm `Cache Provider` reflects `Redis`/`Memory` accordingly, matching which cache implementation was actually registered (`AddStackExchangeRedisCache` vs `AddDistributedMemoryCache`).
5. **Recognition queue:** confirm `Recognition Queue : InMemory Channel` matches the actual registered `IClassroomPhotoQueue` implementation (`InMemoryClassroomPhotoQueue`).

---

## 9. Files created/modified

| File | Change |
|------|--------|
| `Abhyanvaya.API/appsettings.json` | Added `Tenancy:Mode` configuration section (default `MultiTenant`). |
| `Abhyanvaya.API/Diagnostics/StartupDiagnostics.cs` | New — added `DescribeQueueImplementation(...)` (also houses `ResolveApplicationVersion()` and `DescribeDatabaseProvider(...)`, shared with the startup log and health endpoints). |
| `Abhyanvaya.API/Program.cs` | Startup summary extended with `Tenant Mode`, `Deployment`, `Cache Provider`, `Recognition Queue` lines. |

---

## 10. Build status

`dotnet build Abhyanvaya.sln` — **Build succeeded**, 0 errors.

## 11. Acceptance criteria

- ✅ Build succeeds.
- ✅ All values read from actual configuration/DI — no hardcoded diagnostic values (only field labels are literals).
