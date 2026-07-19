using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Abstraction for retrieving one student's AI-enrollment reference photo from an external source.
/// Mirrors the shape of <see cref="IEmbeddingGenerator"/>/<see cref="IEmbeddingProviderFactory"/>:
/// callers (the future enrollment pipeline) depend only on this interface and
/// <see cref="IStudentPhotoProviderFactory"/>, never on a concrete provider — so new sources
/// (OU export, CSV mapping, Google Drive, Azure Blob, OneDrive, manual upload; see
/// <see cref="Domain.Constants.StudentPhotoProviders"/>) plug in by registering another
/// implementation, with no changes to this interface or any consumer.
/// </summary>
/// <remarks>
/// AI20.IMPLEMENT.4 scope: fetching raw bytes only. No image decoding, face detection, or embedding
/// generation happens here — that is the (not-yet-implemented) enrollment validation/engine stage
/// per docs/AI20_ENROLLMENT_ENGINE.md, which runs against whatever bytes this interface returns.
/// </remarks>
public interface IStudentPhotoProvider
{
    /// <summary>Stable machine-readable provider identifier (see <see cref="Domain.Constants.StudentPhotoProviders"/>).</summary>
    string ProviderName { get; }

    /// <summary>Human-readable name for logs/diagnostics/UI.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Retrieves the raw photo bytes for one student. Implementations must not throw for expected
    /// "photo unavailable" outcomes (not found, forbidden, source error) — those are reported via
    /// <see cref="StudentPhotoFetchResult.Success"/> and <see cref="StudentPhotoFetchResult.FailureCategory"/>
    /// so the caller can classify retry-vs-terminal without depending on exception types. Only genuinely
    /// unexpected/programmer errors (e.g. misconfiguration) should throw.
    /// </summary>
    Task<StudentPhotoFetchResult> FetchPhotoAsync(
        StudentPhotoFetchRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Identifying context a provider needs to locate one student's enrollment photo. Deliberately narrow
/// (mirrors <see cref="EmbeddingGenerationRequest"/>) — a provider that needs additional mapping data
/// (e.g. a CSV row, a Drive file id) resolves it internally, keyed by these fields, rather than widening
/// this contract for every future provider's own lookup needs.
/// </summary>
public sealed record StudentPhotoFetchRequest(
    int TenantId,
    int StudentId,
    string StudentNumber,
    string CollegeCode,
    int AcademicYear);

/// <summary>
/// Outcome of one photo-fetch attempt. Always carries <see cref="SourceReference"/> (even on failure) so
/// callers can persist an audit trail of exactly what was requested, per docs/AI20_ENROLLMENT_DATABASE.md
/// §3.2's <c>StudentEnrollmentItem.SourceUrl</c> column.
/// </summary>
public sealed record StudentPhotoFetchResult
{
    public required bool Success { get; init; }

    public byte[]? PhotoBytes { get; init; }

    public string? ContentType { get; init; }

    /// <summary>Provider-specific resolved location (URL, file path, or object id) — never null, even on failure.</summary>
    public required string SourceReference { get; init; }

    /// <summary>
    /// Set only on failure. Null on a transient failure that has no permanent classification yet
    /// (e.g. a timeout or 5xx) — callers should treat a failed result with a null category as
    /// retry-eligible; a non-null category is a signal from the provider that retrying is unlikely
    /// to help without a source-side change (see docs/AI20_ENROLLMENT_ENGINE.md §7's retry table).
    /// </summary>
    public FailureCategory? FailureCategory { get; init; }

    public string? FailureReason { get; init; }

    public static StudentPhotoFetchResult Successful(byte[] photoBytes, string? contentType, string sourceReference) =>
        new()
        {
            Success = true,
            PhotoBytes = photoBytes,
            ContentType = contentType,
            SourceReference = sourceReference,
        };

    public static StudentPhotoFetchResult Failure(string sourceReference, string failureReason, FailureCategory? failureCategory = null) =>
        new()
        {
            Success = false,
            SourceReference = sourceReference,
            FailureReason = failureReason,
            FailureCategory = failureCategory,
        };
}
