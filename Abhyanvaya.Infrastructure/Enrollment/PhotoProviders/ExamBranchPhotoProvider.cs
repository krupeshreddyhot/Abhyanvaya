using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Constants;
using Abhyanvaya.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Enrollment.PhotoProviders;

/// <summary>
/// <see cref="IStudentPhotoProvider"/> for exam-branch-style HTTP photo hosts, where a student's photo
/// is retrievable at a predictable <c>{baseUrl}/{collegeCode}/{academicYear}/{studentNumber}.jpg</c>-shaped
/// URL. See docs/AI20_PHOTO_IMPORT.md for the full design this implementation follows.
/// </summary>
/// <remarks>
/// AI20.IMPLEMENT.4 scope: fetching raw bytes and classifying the HTTP-level outcome only. No image
/// decoding, face detection, or embedding generation happens here.
/// </remarks>
public sealed class ExamBranchPhotoProvider : IStudentPhotoProvider
{
    /// <summary>Named <see cref="IHttpClientFactory"/> client this provider uses — registered once in DI with the retry policy attached.</summary>
    public const string HttpClientName = "ExternalPhotoImport";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ExamBranchPhotoProviderOptions _options;
    private readonly ILogger<ExamBranchPhotoProvider> _logger;

    public ExamBranchPhotoProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ExamBranchPhotoProviderOptions> options,
        ILogger<ExamBranchPhotoProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => StudentPhotoProviders.ExamBranch;

    public string DisplayName => "Exam Branch Photo Host";

    public async Task<StudentPhotoFetchResult> FetchPhotoAsync(
        StudentPhotoFetchRequest request,
        CancellationToken cancellationToken = default)
    {
        var resolvedUrl = BuildResolvedUrl(request);

        if (string.IsNullOrWhiteSpace(_options.BaseUrlTemplate))
        {
            _logger.LogError(
                "ExamBranchPhotoProvider is not configured. StudentPhotoProvider:ExamBranch:BaseUrlTemplate is empty. StudentId={StudentId}",
                request.StudentId);
            return StudentPhotoFetchResult.Failure(
                resolvedUrl, "Provider is not configured (BaseUrlTemplate is empty).", FailureCategory.Unknown);
        }

        _logger.LogInformation(
            "Enrollment Photo Download Started. StudentId={StudentId} TenantId={TenantId} Url={ResolvedUrl}",
            request.StudentId, request.TenantId, resolvedUrl);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(resolvedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var contentType = response.Content.Headers.ContentType?.MediaType;

                _logger.LogInformation(
                    "Enrollment Photo Download Completed. StudentId={StudentId} Url={ResolvedUrl} ByteSize={ByteSize} ContentType={ContentType}",
                    request.StudentId, resolvedUrl, bytes.Length, contentType);

                return StudentPhotoFetchResult.Successful(bytes, contentType, resolvedUrl);
            }

            var (category, reason) = ClassifyFailureStatus(response.StatusCode);

            _logger.LogWarning(
                "Enrollment Photo Download Failed. StudentId={StudentId} Url={ResolvedUrl} StatusCode={StatusCode} Category={Category}",
                request.StudentId, resolvedUrl, (int)response.StatusCode, category);

            return StudentPhotoFetchResult.Failure(resolvedUrl, reason, category);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout expired — a transient failure, not a caller-requested cancellation.
            _logger.LogWarning(
                "Enrollment Photo Download Failed. StudentId={StudentId} Url={ResolvedUrl} Reason=Timeout",
                request.StudentId, resolvedUrl);
            return StudentPhotoFetchResult.Failure(resolvedUrl, "Request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "Enrollment Photo Download Failed. StudentId={StudentId} Url={ResolvedUrl} Reason=NetworkError",
                request.StudentId, resolvedUrl);
            return StudentPhotoFetchResult.Failure(resolvedUrl, ex.Message);
        }
    }

    private string BuildResolvedUrl(StudentPhotoFetchRequest request)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrlTemplate))
        {
            return _options.BaseUrlTemplate ?? string.Empty;
        }

        return _options.BaseUrlTemplate
            .Replace("{collegeCode}", Uri.EscapeDataString(request.CollegeCode), StringComparison.OrdinalIgnoreCase)
            .Replace("{academicYear}", request.AcademicYear.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{studentNumber}", Uri.EscapeDataString(request.StudentNumber), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 404/403 map to a permanent category (not auto-retried); everything else is left uncategorized
    /// (null) so the caller treats it as transient/retry-eligible per docs/AI20_ENROLLMENT_ENGINE.md §7.
    /// </summary>
    private static (FailureCategory? Category, string Reason) ClassifyFailureStatus(System.Net.HttpStatusCode statusCode) => statusCode switch
    {
        System.Net.HttpStatusCode.NotFound => (FailureCategory.PhotoNotFound, "Source returned 404 Not Found."),
        System.Net.HttpStatusCode.Forbidden => (FailureCategory.AccessDenied, "Source returned 403 Forbidden."),
        System.Net.HttpStatusCode.Unauthorized => (FailureCategory.AccessDenied, "Source returned 401 Unauthorized."),
        _ => (null, $"Source returned HTTP {(int)statusCode}."),
    };
}
