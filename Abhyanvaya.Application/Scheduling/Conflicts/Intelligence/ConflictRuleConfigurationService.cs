using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;

public interface IConflictRuleConfigurationService
{
    Task<ConflictRuleThresholds> GetThresholdsAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConflictRuleThresholdDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<ConflictRuleThresholdDto> UpdateAsync(UpdateConflictRuleThresholdRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConflictRuleConfigHistoryDto>> GetHistoryAsync(string? thresholdKey, CancellationToken cancellationToken = default);
}

public sealed class ConflictRuleConfigurationService : IConflictRuleConfigurationService
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ConflictRuleThresholds _appSettingsFallback;

    public ConflictRuleConfigurationService(
        IApplicationDbContext db,
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IOptions<ConflictRuleThresholds> options)
    {
        _db = db;
        _uow = uow;
        _currentUser = currentUser;
        _appSettingsFallback = Clone(options.Value ?? ConflictRuleThresholds.Defaults);
    }

    public async Task<ConflictRuleThresholds> GetThresholdsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var result = Clone(_appSettingsFallback);
        var rows = await _db.SchedulingConflictRuleThresholdSettings
            .Where(x => x.TenantId == tenantId && x.IsActive && !x.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
            Apply(result, row.ThresholdKey, row.Value);

        return result;
    }

    public async Task<IReadOnlyList<ConflictRuleThresholdDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var thresholds = await GetThresholdsAsync(tenantId, cancellationToken);
        var dbRows = await _db.SchedulingConflictRuleThresholdSettings
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Catalog().Select(def =>
        {
            var db = dbRows.FirstOrDefault(r => r.ThresholdKey == def.Key && r.IsActive);
            return new ConflictRuleThresholdDto
            {
                ThresholdKey = def.Key,
                DisplayName = def.DisplayName,
                Description = def.Description,
                Unit = def.Unit,
                Value = GetValue(thresholds, def.Key),
                Version = db?.Version ?? 0,
                Source = db is null ? "AppSettings" : "Database",
                IsActive = true
            };
        }).ToList();
    }

    public async Task<ConflictRuleThresholdDto> UpdateAsync(UpdateConflictRuleThresholdRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var def = Catalog().FirstOrDefault(c => c.Key == request.ThresholdKey);
        if (def.Key is null)
            throw new InvalidOperationException($"Unknown threshold key '{request.ThresholdKey}'.");

        var existing = await _db.SchedulingConflictRuleThresholdSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ThresholdKey == request.ThresholdKey && !x.IsDeleted, cancellationToken);

        var oldValue = existing?.Value ?? GetValue(await GetThresholdsAsync(tenantId, cancellationToken), request.ThresholdKey);
        if (existing is null)
        {
            existing = new ConflictRuleThresholdSetting
            {
                TenantId = tenantId,
                ThresholdKey = def.Key,
                DisplayName = def.DisplayName,
                Description = def.Description,
                Unit = def.Unit,
                Value = request.Value,
                Version = 1,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId
            };
            await _db.AddAsync(existing);
        }
        else
        {
            existing.Value = request.Value;
            existing.Version += 1;
            existing.UpdatedDate = DateTime.UtcNow;
            existing.UpdatedBy = _currentUser.UserId;
            existing.DisplayName = def.DisplayName;
            existing.Description = def.Description;
            existing.IsActive = true;
        }

        await _db.AddAsync(new ConflictRuleConfigChangeHistory
        {
            TenantId = tenantId,
            ThresholdKey = request.ThresholdKey,
            OldValue = oldValue,
            NewValue = request.Value,
            Version = existing.Version,
            ChangeReason = request.ChangeReason,
            ChangedByUserId = _currentUser.UserId,
            ChangedUtc = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        });

        await _uow.SaveChangesAsync(cancellationToken);

        return new ConflictRuleThresholdDto
        {
            ThresholdKey = existing.ThresholdKey,
            DisplayName = existing.DisplayName,
            Description = existing.Description,
            Unit = existing.Unit,
            Value = existing.Value,
            Version = existing.Version,
            Source = "Database",
            IsActive = existing.IsActive
        };
    }

    public async Task<IReadOnlyList<ConflictRuleConfigHistoryDto>> GetHistoryAsync(string? thresholdKey, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var query = _db.SchedulingConflictRuleConfigChangeHistories
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(thresholdKey))
            query = query.Where(x => x.ThresholdKey == thresholdKey);

        return await query.OrderByDescending(x => x.ChangedUtc).Take(100).AsNoTracking()
            .Select(x => new ConflictRuleConfigHistoryDto
            {
                ThresholdKey = x.ThresholdKey,
                OldValue = x.OldValue,
                NewValue = x.NewValue,
                Version = x.Version,
                ChangeReason = x.ChangeReason,
                ChangedByUserId = x.ChangedByUserId,
                ChangedUtc = x.ChangedUtc
            }).ToListAsync(cancellationToken);
    }

    private static ConflictRuleThresholds Clone(ConflictRuleThresholds src) => new()
    {
        MaximumContinuousClasses = src.MaximumContinuousClasses,
        MaximumDailyClasses = src.MaximumDailyClasses,
        MinimumBreakMinutes = src.MinimumBreakMinutes,
        FacultyTravelBufferMinutes = src.FacultyTravelBufferMinutes,
        RoomCapacityMarginPercent = src.RoomCapacityMarginPercent,
        LabUtilizationPercent = src.LabUtilizationPercent,
        LunchWindowEnabled = src.LunchWindowEnabled,
        ContiguousGapMinutes = src.ContiguousGapMinutes
    };

    private static void Apply(ConflictRuleThresholds t, string key, decimal value)
    {
        switch (key)
        {
            case ConflictRuleThresholds.Keys.MaximumContinuousClasses: t.MaximumContinuousClasses = (int)value; break;
            case ConflictRuleThresholds.Keys.MaximumDailyClasses: t.MaximumDailyClasses = (int)value; break;
            case ConflictRuleThresholds.Keys.MinimumBreakMinutes: t.MinimumBreakMinutes = (int)value; break;
            case ConflictRuleThresholds.Keys.FacultyTravelBufferMinutes: t.FacultyTravelBufferMinutes = (int)value; break;
            case ConflictRuleThresholds.Keys.RoomCapacityMarginPercent: t.RoomCapacityMarginPercent = value; break;
            case ConflictRuleThresholds.Keys.LabUtilizationPercent: t.LabUtilizationPercent = value; break;
            case ConflictRuleThresholds.Keys.LunchWindowEnabled: t.LunchWindowEnabled = value != 0; break;
            case ConflictRuleThresholds.Keys.ContiguousGapMinutes: t.ContiguousGapMinutes = (int)value; break;
        }
    }

    private static decimal GetValue(ConflictRuleThresholds t, string key) => key switch
    {
        ConflictRuleThresholds.Keys.MaximumContinuousClasses => t.MaximumContinuousClasses,
        ConflictRuleThresholds.Keys.MaximumDailyClasses => t.MaximumDailyClasses,
        ConflictRuleThresholds.Keys.MinimumBreakMinutes => t.MinimumBreakMinutes,
        ConflictRuleThresholds.Keys.FacultyTravelBufferMinutes => t.FacultyTravelBufferMinutes,
        ConflictRuleThresholds.Keys.RoomCapacityMarginPercent => t.RoomCapacityMarginPercent,
        ConflictRuleThresholds.Keys.LabUtilizationPercent => t.LabUtilizationPercent,
        ConflictRuleThresholds.Keys.LunchWindowEnabled => t.LunchWindowEnabled ? 1 : 0,
        ConflictRuleThresholds.Keys.ContiguousGapMinutes => t.ContiguousGapMinutes,
        _ => 0
    };

    private static IEnumerable<(string Key, string DisplayName, string Description, string Unit)> Catalog() =>
    [
        (ConflictRuleThresholds.Keys.MaximumContinuousClasses, "Maximum Continuous Classes", "Default max consecutive periods when faculty preference is unset.", "count"),
        (ConflictRuleThresholds.Keys.MaximumDailyClasses, "Maximum Daily Classes", "Configured daily class ceiling (stored for policy; no rule redesign).", "count"),
        (ConflictRuleThresholds.Keys.MinimumBreakMinutes, "Minimum Break", "Fallback minimum break minutes when preference is unset.", "minutes"),
        (ConflictRuleThresholds.Keys.FacultyTravelBufferMinutes, "Faculty Travel Buffer", "Minimum minutes between cross-campus consecutive classes.", "minutes"),
        (ConflictRuleThresholds.Keys.RoomCapacityMarginPercent, "Room Capacity Margin", "Percent headroom required below room capacity.", "percent"),
        (ConflictRuleThresholds.Keys.LabUtilizationPercent, "Lab Utilization %", "Configured lab utilization advisory threshold.", "percent"),
        (ConflictRuleThresholds.Keys.LunchWindowEnabled, "Lunch Window", "1 = enforce lunch overlap detection, 0 = disabled.", "flag"),
        (ConflictRuleThresholds.Keys.ContiguousGapMinutes, "Contiguous Gap", "Max gap minutes still treated as continuous teaching.", "minutes"),
    ];
}
