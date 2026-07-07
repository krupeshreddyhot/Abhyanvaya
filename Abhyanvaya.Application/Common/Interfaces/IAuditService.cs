using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Generic audit trail service for cross-module entity changes.
/// </summary>
public interface IAuditService
{
    Task RecordAsync(
        string entityName,
        string entityId,
        AuditAction action,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default);
}
