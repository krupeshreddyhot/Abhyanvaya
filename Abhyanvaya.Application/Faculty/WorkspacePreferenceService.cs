using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Faculty;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Faculty;

public interface IWorkspacePreferenceService
{
    Task<WorkspacePreferenceDto> GetAsync(CancellationToken cancellationToken = default);
    Task<WorkspacePreferenceDto> UpsertAsync(UpdateWorkspacePreferenceRequest request, CancellationToken cancellationToken = default);
}

public sealed class WorkspacePreferenceService : IWorkspacePreferenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public WorkspacePreferenceService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<WorkspacePreferenceDto> GetAsync(CancellationToken cancellationToken = default)
    {
        EnsureStaff();
        var entity = await FindAsync(cancellationToken);
        return entity is null ? DefaultDto() : Map(entity);
    }

    public async Task<WorkspacePreferenceDto> UpsertAsync(
        UpdateWorkspacePreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureStaff();
        var entity = await FindAsync(cancellationToken);
        if (entity is null)
        {
            entity = new WorkspacePreference
            {
                TenantId = _currentUser.TenantId,
                StaffId = _currentUser.StaffId,
                UserId = _currentUser.UserId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId
            };
            await _db.AddAsync(entity);
        }

        if (!string.IsNullOrWhiteSpace(request.LandingPage))
            entity.LandingPage = request.LandingPage.Trim();
        if (!string.IsNullOrWhiteSpace(request.DashboardLayout))
            entity.DashboardLayout = request.DashboardLayout.Trim();
        if (!string.IsNullOrWhiteSpace(request.DefaultTimetableView))
            entity.DefaultTimetableView = request.DefaultTimetableView.Trim();
        if (request.FavoriteQuickActions is not null)
            entity.FavoriteQuickActionsCsv = string.Join(',', request.FavoriteQuickActions.Where(a => !string.IsNullOrWhiteSpace(a)));
        if (!string.IsNullOrWhiteSpace(request.ThemePreference))
            entity.ThemePreference = request.ThemePreference.Trim();
        if (request.NotificationPreferences is not null)
            entity.NotificationPreferencesJson = JsonSerializer.Serialize(request.NotificationPreferences, JsonOptions);
        if (request.OneHandedMode.HasValue)
            entity.OneHandedMode = request.OneHandedMode.Value;
        if (request.HighContrast.HasValue)
            entity.HighContrast = request.HighContrast.Value;

        entity.UpdatedDate = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    private async Task<WorkspacePreference?> FindAsync(CancellationToken cancellationToken) =>
        await _db.SchedulingWorkspacePreferences
            .FirstOrDefaultAsync(p =>
                p.TenantId == _currentUser.TenantId &&
                p.StaffId == _currentUser.StaffId &&
                !p.IsDeleted, cancellationToken);

    private void EnsureStaff()
    {
        if (_currentUser.StaffId <= 0)
            throw new InvalidOperationException("Workspace preferences require a faculty StaffId.");
    }

    private WorkspacePreferenceDto DefaultDto() =>
        Map(new WorkspacePreference
        {
            StaffId = _currentUser.StaffId,
            UserId = _currentUser.UserId
        });

    private static WorkspacePreferenceDto Map(WorkspacePreference entity)
    {
        IReadOnlyDictionary<string, bool> prefs;
        try
        {
            prefs = JsonSerializer.Deserialize<Dictionary<string, bool>>(entity.NotificationPreferencesJson, JsonOptions)
                    ?? new Dictionary<string, bool>();
        }
        catch
        {
            prefs = new Dictionary<string, bool>();
        }

        return new WorkspacePreferenceDto
        {
            Id = entity.Id,
            StaffId = entity.StaffId,
            UserId = entity.UserId,
            LandingPage = entity.LandingPage,
            DashboardLayout = entity.DashboardLayout,
            DefaultTimetableView = entity.DefaultTimetableView,
            FavoriteQuickActions = entity.FavoriteQuickActionsCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            ThemePreference = entity.ThemePreference,
            NotificationPreferences = prefs,
            OneHandedMode = entity.OneHandedMode,
            HighContrast = entity.HighContrast,
            UpdatedUtc = entity.UpdatedDate
        };
    }
}
