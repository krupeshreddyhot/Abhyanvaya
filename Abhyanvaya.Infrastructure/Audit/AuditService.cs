using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.Audit;

/// <summary>
/// Persists generic <see cref="AuditEntry"/> rows for cross-module auditing.
/// </summary>
public sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AuditService(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task RecordAsync(
        string entityName,
        string entityId,
        AuditAction action,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditEntry
        {
            TenantId = _currentUser.TenantId,
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            OldValues = SerializeValues(oldValues),
            NewValues = SerializeValues(newValues),
            PerformedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            PerformedUtc = DateTime.UtcNow
        };

        await _context.AddAsync(entry);
    }

    private static string? SerializeValues(object? values) =>
        values == null ? null : JsonSerializer.Serialize(values, JsonOptions);
}
