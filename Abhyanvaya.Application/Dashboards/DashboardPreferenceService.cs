using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Dashboards;
using Abhyanvaya.Domain.Entities.Dashboards;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Dashboards;

public interface IDashboardPreferenceService
{
    Task<DashboardPreferenceDto> GetAsync(string? roleScope = null, CancellationToken cancellationToken = default);
    Task<DashboardPreferenceDto> UpsertAsync(UpdateDashboardPreferenceRequest request, CancellationToken cancellationToken = default);
}

/// <summary>AI31.6.9 / AI31.8.4 — DB-persisted dashboard preferences (per user + tenant + role scope).</summary>
public sealed class DashboardPreferenceService : IDashboardPreferenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DashboardPreferenceService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DashboardPreferenceDto> GetAsync(string? roleScope = null, CancellationToken cancellationToken = default)
    {
        var scope = NormalizeScope(roleScope ?? InferScope());
        try
        {
            var entity = await FindAsync(scope, cancellationToken);
            return entity is null ? DefaultDto(scope) : Map(entity);
        }
        catch
        {
            return DefaultDto(scope);
        }
    }

    public async Task<DashboardPreferenceDto> UpsertAsync(
        UpdateDashboardPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = NormalizeScope(request.RoleScope ?? InferScope());
        try
        {
            if (request.RestoreDefaults == true)
            {
                var existing = await FindAsync(scope, cancellationToken);
                if (existing is not null)
                {
                    existing.IsDeleted = true;
                    existing.UpdatedDate = DateTime.UtcNow;
                    existing.UpdatedBy = _currentUser.UserId;
                    await _db.SaveChangesAsync(cancellationToken);
                }
                return DefaultDto(scope);
            }

            var entity = await FindAsync(scope, cancellationToken);
            if (entity is null)
            {
                entity = new DashboardPreference
                {
                    TenantId = _currentUser.TenantId,
                    UserId = _currentUser.UserId,
                    RoleScope = scope,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = _currentUser.UserId
                };
                await _db.AddAsync(entity);
            }

            if (!string.IsNullOrWhiteSpace(request.DefaultLandingPage))
                entity.DefaultLandingPage = request.DefaultLandingPage.Trim();
            if (request.CompactMode.HasValue)
                entity.CompactMode = request.CompactMode.Value;
            if (request.HiddenWidgets is not null)
                entity.HiddenWidgetsJson = JsonSerializer.Serialize(request.HiddenWidgets, JsonOptions);
            if (request.WidgetOrder is not null)
                entity.WidgetOrderJson = JsonSerializer.Serialize(request.WidgetOrder, JsonOptions);
            if (request.PinnedWidgets is not null)
                entity.PinnedWidgetsJson = JsonSerializer.Serialize(request.PinnedWidgets, JsonOptions);
            if (request.Filters is not null)
                entity.FilterJson = JsonSerializer.Serialize(request.Filters, JsonOptions);
            if (request.RefreshIntervalSeconds.HasValue)
                entity.RefreshIntervalSeconds = NormalizeRefresh(request.RefreshIntervalSeconds.Value);
            if (request.HighContrast.HasValue)
                entity.HighContrast = request.HighContrast.Value;

            entity.UpdatedDate = DateTime.UtcNow;
            entity.UpdatedBy = _currentUser.UserId;
            await _db.SaveChangesAsync(cancellationToken);
            return Map(entity);
        }
        catch
        {
            // Schema not applied — return in-memory merge of defaults + request (no cross-user impact).
            var dto = DefaultDto(scope);
            return new DashboardPreferenceDto
            {
                Id = dto.Id,
                RoleScope = scope,
                DefaultLandingPage = request.DefaultLandingPage ?? dto.DefaultLandingPage,
                CompactMode = request.CompactMode ?? dto.CompactMode,
                HiddenWidgets = request.HiddenWidgets ?? dto.HiddenWidgets,
                WidgetOrder = request.WidgetOrder ?? dto.WidgetOrder,
                PinnedWidgets = request.PinnedWidgets ?? dto.PinnedWidgets,
                Filters = request.Filters ?? dto.Filters,
                RefreshIntervalSeconds = request.RefreshIntervalSeconds.HasValue
                    ? NormalizeRefresh(request.RefreshIntervalSeconds.Value)
                    : dto.RefreshIntervalSeconds,
                HighContrast = request.HighContrast ?? dto.HighContrast
            };
        }
    }

    private async Task<DashboardPreference?> FindAsync(string scope, CancellationToken cancellationToken) =>
        await _db.DashboardPreferences
            .FirstOrDefaultAsync(p =>
                p.TenantId == _currentUser.TenantId &&
                p.UserId == _currentUser.UserId &&
                p.RoleScope == scope &&
                !p.IsDeleted, cancellationToken);

    private string InferScope()
    {
        var role = (_currentUser.Role ?? "").Trim();
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase)) return "Admin";
        if (role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)) return "SuperAdmin";
        if (role.Equals("Faculty", StringComparison.OrdinalIgnoreCase)) return "Faculty";
        return "Faculty";
    }

    private static string NormalizeScope(string scope) =>
        string.IsNullOrWhiteSpace(scope) ? "Faculty" : scope.Trim();

    private static int NormalizeRefresh(int seconds) =>
        seconds switch
        {
            0 or 30 or 60 or 120 or 300 => seconds,
            _ => 60
        };

    private DashboardPreferenceDto DefaultDto(string scope) =>
        Map(new DashboardPreference
        {
            UserId = _currentUser.UserId,
            RoleScope = scope,
            DefaultLandingPage = scope.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                                 scope.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                ? "admin-operations"
                : "command-center",
            RefreshIntervalSeconds = 60
        });

    private static DashboardPreferenceDto Map(DashboardPreference entity) =>
        new()
        {
            Id = entity.Id,
            RoleScope = entity.RoleScope,
            DefaultLandingPage = entity.DefaultLandingPage,
            CompactMode = entity.CompactMode,
            HiddenWidgets = DeserializeList(entity.HiddenWidgetsJson),
            WidgetOrder = DeserializeList(entity.WidgetOrderJson),
            PinnedWidgets = DeserializeList(entity.PinnedWidgetsJson),
            Filters = DeserializeFilter(entity.FilterJson),
            RefreshIntervalSeconds = NormalizeRefresh(entity.RefreshIntervalSeconds <= 0 && entity.Id == 0
                ? 60
                : entity.RefreshIntervalSeconds),
            HighContrast = entity.HighContrast
        };

    private static IReadOnlyList<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? []; }
        catch { return []; }
    }

    private static DashboardFilterRequest? DeserializeFilter(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return null;
        try { return JsonSerializer.Deserialize<DashboardFilterRequest>(json, JsonOptions); }
        catch { return null; }
    }
}
