using Abhyanvaya.Application.DTOs.AttendanceRecognition;
using Abhyanvaya.Domain.Exceptions;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Teacher review commands and audit queries for AI recognition rows.
/// </summary>
/// <remarks>
/// Review commands persist through optimistic concurrency-aware saves. Conflicts raise
/// <see cref="ConcurrencyConflictException"/>.
/// </remarks>
public interface IAttendanceRecognitionReviewService
{
    Task<IReadOnlyList<AttendanceRecognitionReviewDto>> GetRecognitionsForSessionAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default);

    Task<RecognitionSummaryDto> GetRecognitionSummaryAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default);

    Task<AttendanceRecognitionDto> ReviewRecognitionAsync(
        AttendanceRecognitionReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceRecognitionDto>> ReviewBatchAsync(
        AttendanceRecognitionBatchReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<AttendanceRecognitionDto> ResetRecognitionAsync(
        Guid recognitionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceRecognitionReviewHistoryDto>> GetReviewHistoryForRecognitionAsync(
        Guid recognitionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceRecognitionReviewHistoryDto>> GetReviewHistoryForSessionAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default);
}
