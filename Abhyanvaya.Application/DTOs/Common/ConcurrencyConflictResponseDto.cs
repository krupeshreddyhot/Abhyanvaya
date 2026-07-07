namespace Abhyanvaya.Application.DTOs.Common;

/// <summary>
/// Standard HTTP 409 response body for optimistic concurrency conflicts.
/// </summary>
public sealed class ConcurrencyConflictResponseDto
{
    public required string Message { get; init; }

    public required string Code { get; init; }

    public bool ReloadRequired { get; init; }
}
