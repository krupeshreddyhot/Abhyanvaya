# AI20.ENROLLMENT.4 — External Photo Import Design

**Type:** Design only. No production code was written or modified to produce this document.

---

## 1. URL Pattern Review

Example given: `https://exambranch.com/PHOTOS/1053/2025/105325405001.jpg`, decomposed as `{collegeCode}/{academicYear}/{studentNumber}.jpg`.

**Recommendation: never hardcode `exambranch.com` or the `PHOTOS` path segment.** Configure the full template as a single setting, following the exact configuration-resolution convention already established for media/branding settings (`BrandingSettingsResolver`, `Abhyanvaya.API/Common/BrandingSettingsResolver.cs` — double-underscore/single-underscore environment variable overrides):

```json
"ExternalPhotoImport": {
  "BaseUrlTemplate": "https://exambranch.com/PHOTOS/{collegeCode}/{academicYear}/{studentNumber}.jpg",
  "TimeoutSeconds": 15,
  "MaxDegreeOfParallelism": 8,
  "MaxRetryAttempts": 3
}
```

`{collegeCode}` → `College.Code`, `{academicYear}` → `StudentEnrollmentBatch.AcademicYear` (per `docs/AI20_ENROLLMENT_DATABASE.md`), `{studentNumber}` → `Student.StudentNumber`. A simple placeholder-substitution helper (no templating library needed) builds the final `SourceUrl` stored on each `StudentEnrollmentJob` row for audit (§6). This makes the source host, path shape, and file extension entirely swappable per-environment (e.g. a college that migrates to a different exam-photo vendor later needs only a config change, not a code change) — directly mirroring why `MediaOptions`/`BrandingSettingsResolver` externalize storage config today rather than hardcoding a provider.

---

## 2. Download Strategy

### Timeouts

Two independent timeouts, because a single "job timeout" conflates two different failure modes:

- **Per-request HTTP timeout** (recommend 15s default, configurable): bounds a single download attempt. Implemented via `HttpClient.Timeout` on a dedicated named client (`IHttpClientFactory.CreateClient("ExternalPhotoImport")`), registered once via `AddHttpClient("ExternalPhotoImport", ...)` — this codebase does not currently have a named `HttpClient` registration to mirror, so this introduces the standard ASP.NET Core `IHttpClientFactory` pattern for the first time, which is the officially recommended approach specifically to avoid the well-known socket-exhaustion problems of manually newing up `HttpClient` instances.
- **Per-job overall timeout** (implicit, not a separate timer): bounded naturally by `MaxRetryAttempts × (PerRequestTimeout + backoff delay)` — no separate "job-level" timeout construct is needed beyond that, since the retry policy itself already caps total time spent on one job's download stage.

### Retry

**Reuse the existing Polly dependency** (`Abhyanvaya.Infrastructure.csproj` already references Polly 8.6.6; today used only by `ResiliencePolicies` for Redis, `Abhyanvaya.Infrastructure/Resilience/ResiliencePolicies.cs`). A new, separate policy is warranted (not literally reusing `ResiliencePolicies.WrapPolicy`, which is Redis-tuned — 3s timeout, fixed 2s retry wait, is too aggressive/short for a variable-latency external photo host), but the **same Polly primitives and the same "Timeout + Retry + CircuitBreaker wrapped" shape**:

```csharp
// New: Abhyanvaya.Infrastructure/Resilience/ExternalPhotoImportPolicies.cs (proposed name)
public static IAsyncPolicy<HttpResponseMessage> RetryPolicy =>
    Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .OrResult(r => IsTransientStatus(r.StatusCode))   // 5xx, 429 — NOT 404/403 (see below)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));  // 2s, 4s, 8s
```

Exponential backoff (rather than `ResiliencePolicies`'s fixed 2s wait) is deliberate: a bulk batch potentially hammering the same external host with hundreds of concurrent requests should back off progressively if that host is struggling, both to give it a chance to recover and to avoid the platform itself being blamed for/blocked as abusive traffic.

**Critically, retry is status-code-aware, not blanket** — this is the most important design decision in this section:

| Response | Retry? | Reasoning |
|---|---|---|
| Network error / timeout / connection reset | **Yes** | Transient — the host or network hiccuped |
| `5xx` | **Yes** | Transient — server-side issue, likely to resolve |
| `429 Too Many Requests` | **Yes**, with the backoff already increasing sleep time (and honoring `Retry-After` header if present) | Transient, but also a **signal to reduce parallelism** — see §Throttling |
| `404 Not Found` | **No** | The photo genuinely doesn't exist at that URL; retrying finds the same absence — see §5 |
| `403 Forbidden` | **No** | Access/auth issue; retrying without a config/credential change cannot succeed — see §5 |
| Downloaded body fails image decode | **No** (this is a post-download validation failure, not an HTTP-layer retry candidate) | See §5, invalid/corrupt image |

This mirrors the failure classification already established in `docs/AI20_ENROLLMENT_DATABASE.md`'s `FailureCategory` enum and `docs/AI20_ENROLLMENT_ENGINE.md` §7's retry table — this document is the authoritative source for *why* each category is or isn't retried.

### Parallelism / Throttling

Per `docs/AI20_ENROLLMENT_BACKGROUND.md` §3.5: download concurrency is capped independently from embedding concurrency (I/O-bound vs. CPU-bound), recommended default `MaxDegreeOfParallelism: 8`, implemented as a `SemaphoreSlim(8)` gate the download stage acquires before issuing each HTTP request. This is a **platform-side** cap (protects the API process and, secondarily, is polite to the external host) — it is deliberately conservative and configurable per-environment, since the "right" number depends entirely on the external host's actual capacity, which this design cannot know in advance. If the external host starts returning `429`s under the default concurrency, that is the operational signal to lower `MaxDegreeOfParallelism` via configuration — no code change needed.

No cross-batch global rate limiter is proposed for v1 (e.g. a token-bucket shared across all concurrently running batches) — with the DB-backed queue design (`docs/AI20_ENROLLMENT_BACKGROUND.md`), the practical expectation is one active batch at a time per college, and the semaphore already bounds total concurrent requests from a single `EnrollmentBackgroundService` instance. This is flagged as a v2 consideration if multiple simultaneous large batches across colleges become common and the external host's aggregate capacity becomes a concern.

---

## 3. HTTP Client Registration

```csharp
// Abhyanvaya.Infrastructure/DependencyInjection.cs (proposed addition)
services.AddHttpClient("ExternalPhotoImport", client =>
{
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Abhyanvaya-AI-Enrollment/1.0");
})
.AddPolicyHandler(ExternalPhotoImportPolicies.RetryPolicy);
```

`IHttpClientFactory`'s built-in `AddPolicyHandler` integration is the standard way to attach a Polly policy directly to a named client's request pipeline — this is a first-time introduction of `IHttpClientFactory` to this codebase (confirmed no existing `AddHttpClient` call), but is the officially recommended, low-risk, well-documented ASP.NET Core pattern for exactly this kind of outbound-HTTP-with-retry use case, not a novel/exotic addition.

A dedicated `IExternalPhotoSource` interface wraps this client (so the enrollment pipeline depends on an abstraction, not `IHttpClientFactory` directly — consistent with every other external dependency in this codebase sitting behind an interface):

```csharp
public interface IExternalPhotoSource
{
    Task<ExternalPhotoDownloadResult> DownloadAsync(
        string collegeCode, int academicYear, string studentNumber, CancellationToken cancellationToken);
}

public sealed record ExternalPhotoDownloadResult(
    bool Success,
    byte[]? Bytes,
    string? ContentType,
    HttpStatusCode? StatusCode,
    string ResolvedUrl,
    string? FailureReason);
```

---

## 4. Validation (Post-Download)

Two layers, both file-level (no ONNX/face-detection involvement — that's `docs/AI20_ENROLLMENT_ENGINE.md`'s concern, which runs *after* this stage succeeds):

1. **Magic-byte / content-type sanity check** — verify the downloaded bytes actually begin with a recognized image signature (JPEG `FF D8 FF`, PNG `89 50 4E 47`, WebP `RIFF....WEBP`) before attempting a full decode; a source that returns an HTML error page with a `200` status (a real-world failure mode for misconfigured web servers) is caught here rather than being mistaken for a valid photo.
2. **`ImageSharp.Image.Load` decode attempt** — if the magic bytes pass but the full decode throws (`UnknownImageFormatException`/`InvalidImageContentException`), classify as `CorruptImage`. This reuses the exact same `SixLabors.ImageSharp` package already a dependency of `Abhyanvaya.Infrastructure`/`Abhyanvaya.API` — no new imaging library.
3. **Minimum resolution / max file size** — reuse the same pattern (not the same code, since it lives in a different validator) `ClassroomImageValidator` already applies to classroom photos (640×480 floor, size cap) as the baseline for the *whole downloaded image*, before it's even considered for face detection.

Any failure here sets `StudentEnrollmentJob.FailureCategory` to `InvalidImage` or `CorruptImage` (per `docs/AI20_ENROLLMENT_DATABASE.md`) and the job goes straight to `Failed` (not `RetryRequired` — re-downloading the identical bytes from the identical URL will not fix a genuinely corrupt source file).

---

## 5. Failure Mode Reference

| Scenario | Detected at | Classification | Retry? | Job outcome |
|---|---|---|---|---|
| **Missing photo (404)** | HTTP response | `PhotoNotFound` | No | `Failed` — manually retriable later if the source photo is added |
| **403** | HTTP response | `AccessDenied` | No | `Failed` — flagged distinctly from 404 so an operator investigating a *batch* of 403s (vs. scattered 404s) immediately suspects a systemic access/credentials/IP-allowlist problem with the source host rather than thousands of individually missing photos. **Operational note:** if the real exambranch.com endpoint requires authentication, an IP allowlist, or a signed-URL scheme not yet known at design time, that must be confirmed with the photo-source operator before implementation — this design assumes an unauthenticated, directly-fetchable URL per the example given, and 403 handling here is a defensive default, not a confirmed integration detail |
| **Invalid image** (not an image at all — e.g. HTML error page with `200` status) | Post-download validation | `InvalidImage` | No | `Failed` |
| **Duplicate** (identical photo already successfully enrolled) | Checksum comparison (per `docs/AI20_ENROLLMENT_DATABASE.md` §4) | N/A — not a failure | N/A | Job short-circuits to `Completed` without re-downloading/re-embedding, referencing the existing `StudentFaceEmbedding` |
| **Corrupt image** (valid magic bytes, decode fails) | Post-download validation | `CorruptImage` | No | `Failed` |
| **Timeout / connection error** | HTTP layer | (transient, no permanent category — surfaces as `RetryRequired` while retries remain, escalates to `Failed`/`Timeout` category once exhausted, per `docs/AI20_ENROLLMENT_BACKGROUND.md`'s recovery sweep) | Yes (bounded) | `RetryRequired` → auto-retry → `Failed` (`Timeout`) if exhausted |
| **5xx from source** | HTTP layer | Same transient bucket as timeout | Yes (bounded) | Same as above |

---

## 6. Logging / Progress / Audit

- **Per-attempt structured log**, mirroring `RecognitionMediaService`'s "Upload Started/Completed/Failed" convention: `"Enrollment Photo Download Started/Completed/Failed. BatchId={BatchId} StudentId={StudentId} JobId={JobId} Url={ResolvedUrl} AttemptNumber={AttemptNumber}"` — on failure, includes `StatusCode`/`FailureReason`; **never logs image bytes**, matching the existing platform-wide convention (`RecognitionMediaService`'s logging explicitly avoids logging image data too).
- **Progress**: each transition (`Pending → Downloading → Downloaded`) updates `StudentEnrollmentJob` stage timestamps and the parent batch's counters, per `docs/AI20_ENROLLMENT_BACKGROUND.md` §3.2 — no separate progress mechanism specific to the download stage.
- **Audit**: `StudentEnrollmentJob.SourceUrl` (the fully-resolved URL actually requested), `ContentType`, `ByteSize`, `Checksum`, `ImageWidth`/`ImageHeight` are all persisted regardless of ultimate success/failure (persisted as soon as they're known, even if a later stage fails) — so a SuperAdmin investigating a failure can always see exactly what was fetched and when, per `docs/AI20_ENROLLMENT_DATABASE.md` §6.

---

## Constraints Confirmed

No `IExternalPhotoSource` implementation, `HttpClient` registration, or Polly policy was created to produce this document — all code shown above is illustrative/proposed, not implemented. No existing file was modified.
