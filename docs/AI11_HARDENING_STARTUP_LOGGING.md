# AI11.HARDENING.2 — Startup Configuration Summary Logging

**Status: IMPLEMENTED**
**Date:** 2026-07-04
**Reviewer:** Chief Architect

---

## 1. Purpose

Give operators and developers an at-a-glance, structured log record of the effective runtime configuration every time the API starts — without needing to cross-reference `appsettings.json`, environment variables, and DI registrations by hand. This is a **diagnostics-only** change: no business logic, recognition logic, queue behavior, or controllers were modified.

This directly supports incident diagnosis: if a deployed instance behaves unexpectedly (e.g. recognition failing, wrong storage provider, unexpected worker state), the first thing to check is the startup log block, which now answers "what was this instance actually configured to do?" in one place.

---

## 2. Location and execution guarantee

Implemented in `Abhyanvaya.API/Program.cs`, as a local function `LogStartupConfigurationSummary(WebApplication app)`:

- Called **exactly once**, immediately after all middleware/endpoint configuration (`app.UseAuthentication()`, `app.UseAuthorization()`, `app.MapControllers()`) and **before** `app.Run()`.
- Not placed inside any loop, retry path, or per-request code — it runs once per process lifetime, at startup, by construction.
- Also fulfills AI11.HARDENING.1's "log the resolved `BackgroundServiceExceptionBehavior`" requirement as one line within this same block, rather than as a second, duplicate log statement — avoiding two separate startup log emissions that would otherwise both report the same value.

---

## 3. Sample startup output

```
info: Abhyanvaya.API.Program[0]
      ==========================================================
info: Abhyanvaya.API.Program[0]
      Abhyanvaya AI Attendance Startup
info: Abhyanvaya.API.Program[0]
      ==========================================================
info: Abhyanvaya.API.Program[0]
      Application Environment             : Development
info: Abhyanvaya.API.Program[0]
      Application Version                 : 1.0.0.0
info: Abhyanvaya.API.Program[0]
      BackgroundServiceExceptionBehavior  : Ignore
info: Abhyanvaya.API.Program[0]
      Recognition Worker                  : Enabled
info: Abhyanvaya.API.Program[0]
      Embedding Worker                    : Enabled
info: Abhyanvaya.API.Program[0]
      Storage Provider                    : Local File System
info: Abhyanvaya.API.Program[0]
      Recognition Engine                  : InsightFace
info: Abhyanvaya.API.Program[0]
      Recognition Pipeline Version        : InsightFace-1.0
info: Abhyanvaya.API.Program[0]
      Face Matching Engine                : Cosine Similarity
info: Abhyanvaya.API.Program[0]
      Database Provider                   : PostgreSQL
info: Abhyanvaya.API.Program[0]
      Started At UTC                      : 2026-07-04 07:02:11 UTC
info: Abhyanvaya.API.Program[0]
      ==========================================================
```

(Exact console rendering depends on the configured logging provider/formatter; the values and one-field-per-line structure are as shown above regardless of provider.)

---

## 4. Fields logged and their real source

Every field is read from the actual running configuration/DI container — nothing is hardcoded except field **labels**:

| Field | Source | Notes |
|-------|--------|-------|
| Application Environment | `app.Environment.EnvironmentName` | Real ASP.NET Core hosting environment name (`Development`, `Production`, or any custom value from `ASPNETCORE_ENVIRONMENT`). |
| Application Version | `Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()`, falling back to `Assembly.GetName().Version` | Reflects the actual built assembly's version metadata; no version string is hardcoded in `Program.cs`. |
| BackgroundServiceExceptionBehavior | `app.Services.GetRequiredService<IOptions<HostOptions>>().Value.BackgroundServiceExceptionBehavior` | Reads back the value actually resolved by the AI11.HARDENING.1 `Configure<HostOptions>` call (environment-dependent), not recomputed independently — guarantees the log always matches what the host is really using. |
| Recognition Worker | `app.Services.GetServices<IHostedService>()` checked for `ClassroomRecognitionBackgroundService` | Confirms the hosted service is actually registered in DI, rather than assuming it. |
| Embedding Worker | `app.Services.GetServices<IHostedService>()` checked for `StudentFaceEmbeddingBackgroundService` | Same approach as Recognition Worker. |
| Storage Provider | `IStorageProviderFactory.GetActiveProviderName()` (resolved from DI), mapped to a friendly display name (`local` → "Local File System", `s3` → "AWS S3") | Reflects the actual configured `Media:Provider` setting via the existing provider-factory abstraction — no duplicate configuration parsing. |
| Recognition Engine | `IFaceDetectionService.ProviderName` (resolved from DI) | Currently resolves to `InsightFace` via `InsightFaceDetectionService`, driven by the actual registered implementation rather than a literal string. |
| Recognition Pipeline Version | `IOptions<InsightFaceOptions>.Value.PipelineVersion` | Bound from `appsettings.json` → `InsightFace:PipelineVersion`. |
| Face Matching Engine | Literal label describing the sole registered `IFaceMatcher` implementation (`FaceMatcher`, which performs cosine-distance matching) | There is no configuration toggle for the matching algorithm today (only one implementation exists), so this documents the deployed implementation rather than a config value; it will need updating if additional face matchers are introduced. |
| Database Provider | `ApplicationDbContext.Database.ProviderName`, mapped to a friendly name (`Npgsql...` → "PostgreSQL") | Read directly from the live EF Core context, not assumed from the connection string. |
| Started At UTC | `DateTime.UtcNow` at the moment the summary is logged | Real timestamp, not a fixed value. |

---

## 5. Formatting approach

Per the requirement to avoid one large concatenated string, the summary is emitted as **one `logger.LogInformation(...)` call per line**, each with its own message template and structured arguments (e.g. `logger.LogInformation("Application Environment             : {Environment}", app.Environment.EnvironmentName);`). This means:

- Each field is independently searchable/filterable in structured log sinks (e.g. querying `{Environment}` or `{RecognitionEngine}` across log aggregation tools) rather than being buried inside one opaque multi-line string.
- The literal `==========...` separator lines and the title line are logged as plain `LogInformation` calls with no interpolated data, matching the visual banner style from the requirement while still going through the standard logging pipeline (so they respect configured log levels/sinks like any other log line).

---

## 6. No duplication guarantee

- The summary is invoked from a single call site (`LogStartupConfigurationSummary(app);`), placed once in `Program.cs`, executed once per process start.
- It is not registered as a hosted service, middleware, or event handler that could fire more than once (e.g. it does not hook `IHostApplicationLifetime.ApplicationStarted`, which could theoretically be invoked in edge cases with multiple listeners) — it is a direct, synchronous call in the linear startup sequence.
- It reuses (rather than duplicates) the `BackgroundServiceExceptionBehavior` value already configured by AI11.HARDENING.1, avoiding two separate log statements reporting the same fact.

---

## 7. Future maintenance guidance

When adding new configurable subsystems (e.g. a new storage provider, a second face-matching algorithm, a new background worker, or swapping PostgreSQL for another EF Core provider), update `LogStartupConfigurationSummary` in `Abhyanvaya.API/Program.cs` to:

1. Resolve the new setting from its real source (DI service, `IOptions<T>`, or hosting environment) — **do not hardcode** the new value as a literal.
2. Add one new `logger.LogInformation("<Label> : {Value}", value);` line, keeping the one-field-per-line convention and consistent label column alignment.
3. If the new subsystem has multiple possible implementations (like storage or face matching), prefer resolving the interface from DI and reading an identifying property (e.g. `ProviderName`) over hardcoding a mapping switch, unless a friendly display-name mapping is genuinely needed (as done here for storage/database provider names).
4. Keep the call site as the single point of invocation — do not add a second summary block elsewhere in the startup pipeline.
5. Re-run the verification steps below after any change to confirm the summary still executes exactly once and every field still resolves without throwing (a missing DI registration for a newly-referenced service would fail fast at startup, which is intentional — it surfaces configuration gaps immediately rather than silently).

---

## 8. Verification steps

1. **Build:** `dotnet build Abhyanvaya.sln` — succeeds with 0 errors.
2. **Single execution:** run the API and confirm the `====...` banner and all 11 field lines appear exactly once in the startup log output (not once per request, not twice at startup).
3. **Value accuracy:**
   - Change `ASPNETCORE_ENVIRONMENT` and confirm "Application Environment" and "BackgroundServiceExceptionBehavior" update accordingly.
   - Change `Media:Provider` in configuration and confirm "Storage Provider" reflects the change.
   - Confirm "Recognition Pipeline Version" matches `appsettings.json`'s `InsightFace:PipelineVersion`.
4. **No regressions:** existing upload, recognition, and controller endpoints behave identically — the change only adds log output after routing/middleware configuration and before `app.Run()`; no middleware, endpoint, or DI registration behavior was altered.

---

## 9. Files modified

| File | Change |
|------|--------|
| `Abhyanvaya.API/Program.cs` | Added `LogStartupConfigurationSummary(WebApplication app)` local function and a single call site before `app.Run()`. Added `using` directives for `Abhyanvaya.Infrastructure.BackgroundWorkers`, `Abhyanvaya.Infrastructure.InsightFace`, `Abhyanvaya.Infrastructure.Persistence`, `Microsoft.Extensions.Options`, and `System.Reflection`. (This file also contains the AI11.HARDENING.1 environment-specific `HostOptions` change — see that document for details.) |

No other files were modified for this milestone.

---

## 10. Build status

`dotnet build Abhyanvaya.sln` — **Build succeeded**, 0 errors.

## 11. Architecture impact

None. This is an additive diagnostics/logging change only:

- No controllers, business logic, recognition logic, or queue implementations were modified.
- No new DI registrations were added — all resolved services (`IStorageProviderFactory`, `IFaceDetectionService`, `IOptions<InsightFaceOptions>`, `ApplicationDbContext`, `IHostedService` collection) were already registered by existing `AddInfrastructure`/`AddApplication`/`AddMediaStorage` calls.
- One new scoped `IServiceScope` is created and disposed synchronously at startup to resolve scoped services (`IFaceDetectionService`, `ApplicationDbContext`) safely from outside a request context — this is the standard, supported pattern for resolving scoped services during startup and has no runtime cost beyond process start.
