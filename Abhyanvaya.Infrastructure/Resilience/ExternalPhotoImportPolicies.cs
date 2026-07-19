using System.Net;
using Polly;
using Polly.Retry;

namespace Abhyanvaya.Infrastructure.Resilience;

/// <summary>
/// Retry policy for outbound HTTP-based <see cref="Application.Common.Interfaces.IStudentPhotoProvider"/>
/// implementations (e.g. <see cref="Enrollment.PhotoProviders.ExamBranchPhotoProvider"/>).
/// <para>
/// Deliberately separate from <see cref="ResiliencePolicies"/> (which is tuned for Redis: 3s timeout,
/// fixed 2s retry wait) — an external, variable-latency photo host needs a longer per-attempt timeout
/// and exponential (not fixed) backoff so a bulk enrollment batch backs off progressively instead of
/// hammering a struggling host. See docs/AI20_PHOTO_IMPORT.md §2.
/// </para>
/// <para>
/// Retry is status-code-aware: 404/403 are never retried (retrying a missing/forbidden photo without a
/// source-side fix cannot succeed), while network errors, 5xx, and 429 are retried with exponential
/// backoff (2s, 4s, 8s) honoring <c>Retry-After</c> is left to a future enhancement — see
/// docs/AI20_PHOTO_IMPORT.md §2 "Retry" for the full classification table.
/// </para>
/// </summary>
public static class ExternalPhotoImportPolicies
{
    public static AsyncRetryPolicy<HttpResponseMessage> RetryPolicy =>
        Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult(IsTransientFailure)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    /// <summary>True for retry-eligible responses (5xx, 429); false for everything else, including 404/403.</summary>
    public static bool IsTransientFailure(HttpResponseMessage response) =>
        (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests;
}
