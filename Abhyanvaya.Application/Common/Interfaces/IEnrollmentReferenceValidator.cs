using Abhyanvaya.Application.Enrollment;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Validates enrollment scope references (college, course, group, subject).</summary>
public interface IEnrollmentReferenceValidator
{
    Task<EnrollmentReferenceValidationResult> ValidateAsync(
        EnrollmentBatchRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record EnrollmentReferenceValidationResult
{
    public required bool Succeeded { get; init; }
    public EnrollmentBatchFailureCode? FailureCode { get; init; }
    public string? FailureMessage { get; init; }
    public string? CollegeCode { get; init; }

    public static EnrollmentReferenceValidationResult Ok(string collegeCode) =>
        new() { Succeeded = true, CollegeCode = collegeCode };

    public static EnrollmentReferenceValidationResult Fail(
        EnrollmentBatchFailureCode code,
        string message) =>
        new() { Succeeded = false, FailureCode = code, FailureMessage = message };
}
