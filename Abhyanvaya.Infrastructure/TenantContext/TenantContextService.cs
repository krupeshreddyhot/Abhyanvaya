using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.TenantContext;

public sealed class TenantContextService : ITenantContextService
{
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantContextStore _store;
    private readonly ITenantContextCollegeCatalog _catalog;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IRecentContextService _recentContext;
    private readonly IContextExpirationService _expiration;
    private readonly IContextEventPublisher _events;
    private readonly IContextOperationalMetricsCollector _metrics;
    private readonly ILogger<TenantContextService> _logger;

    private TenantContextSnapshot? _requestCache;

    public TenantContextService(
        ICurrentUserService currentUser,
        ITenantContextStore store,
        ITenantContextCollegeCatalog catalog,
        ITenantContextAccessor tenantContextAccessor,
        IApplicationDbContext context,
        IAuditService auditService,
        IRecentContextService recentContext,
        IContextExpirationService expiration,
        IContextEventPublisher events,
        IContextOperationalMetricsCollector metrics,
        ILogger<TenantContextService> logger)
    {
        _currentUser = currentUser;
        _store = store;
        _catalog = catalog;
        _tenantContextAccessor = tenantContextAccessor;
        _context = context;
        _auditService = auditService;
        _recentContext = recentContext;
        _expiration = expiration;
        _events = events;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<TenantContextSnapshot?> GetCurrentContextAsync(CancellationToken cancellationToken = default)
    {
        if (_requestCache is not null)
        {
            return _requestCache;
        }

        if (IsSuperAdmin())
        {
            var stored = await _store.GetAsync(_currentUser.UserId, cancellationToken);
            if (stored is not null && _expiration.IsExpired(stored))
            {
                await ExpireStoredContextAsync(_currentUser.UserId, stored, cancellationToken);
                stored = null;
            }

            _requestCache = stored ?? BuildGlobalContext("Session");
            return _requestCache;
        }

        _requestCache = await BuildCollegeAdminContextAsync(cancellationToken);
        return _requestCache;
    }

    public async Task<TenantContextValidationResult> SetCurrentContextAsync(int collegeId, CancellationToken cancellationToken = default)
    {
        if (!IsSuperAdmin())
        {
            return TenantContextValidationResult.Failure("NotAllowed", "Only SuperAdmin can set operational college context.");
        }

        if (_catalog is not TenantContextCollegeCatalog catalogImpl)
        {
            return TenantContextValidationResult.Failure("InternalError", "College catalog is not available.");
        }

        var college = await catalogImpl.FindCollegeAsync(
            collegeId,
            _currentUser.Role,
            _currentUser.TenantId,
            cancellationToken);

        if (college is null)
        {
            return TenantContextValidationResult.Failure("CollegeNotFound", "The selected college was not found or is not accessible.");
        }

        var validation = catalogImpl.ValidateCollegeRow(college);
        if (!validation.IsValid)
        {
            _metrics.RecordContextValidationFailed();
            return validation;
        }

        var now = DateTime.UtcNow;
        var snapshot = new TenantContextSnapshot
        {
            UserId = _currentUser.UserId,
            Role = _currentUser.Role,
            SelectedCollegeId = college.Id,
            SelectedCollegeName = college.Name,
            SelectedCollegeCode = college.Code,
            TenantId = college.TenantId,
            ContextType = ContextType.College,
            CreatedUtc = now,
            ExpiresUtc = _expiration.ComputeExpiresUtc(now),
            IsGlobal = false,
            ContextSource = "OperationalSelection",
        };

        await _store.SetAsync(_currentUser.UserId, snapshot, cancellationToken);
        _requestCache = snapshot;

        await _recentContext.RecordCollegeSelectionAsync(
            _currentUser.UserId,
            new AvailableCollegeDto
            {
                Id = college.Id,
                TenantId = college.TenantId,
                Name = college.Name,
                Code = college.Code,
                Status = college.IsDeleted ? "Deleted" : "Active",
                AiEnabled = true,
            },
            cancellationToken);

        await _auditService.RecordAsync(
            "TenantContext",
            college.Id.ToString(),
            AuditAction.Custom,
            newValues: new { Action = "ContextSelected", college.Id, college.Name, college.TenantId, snapshot.ExpiresUtc });

        _metrics.RecordContextSwitch(college.TenantId);
        await _events.PublishContextChangedAsync(snapshot, cancellationToken);

        _logger.LogInformation(
            "Tenant context selected. UserId={UserId} CollegeId={CollegeId} TenantId={TenantId}",
            _currentUser.UserId,
            college.Id,
            college.TenantId);

        await ApplyOperationalTenantAsync(cancellationToken);

        return TenantContextValidationResult.Success();
    }

    public async Task ClearContextAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSuperAdmin())
        {
            return;
        }

        await _store.RemoveAsync(_currentUser.UserId, cancellationToken);
        _requestCache = BuildGlobalContext("Cleared");
        _tenantContextAccessor.Clear();

        await _auditService.RecordAsync(
            "TenantContext",
            _currentUser.UserId.ToString(),
            AuditAction.Custom,
            newValues: new { Action = "ContextCleared" });

        await _events.PublishContextClearedAsync(_currentUser.UserId, cancellationToken);

        _logger.LogInformation("Tenant context cleared for UserId={UserId}", _currentUser.UserId);
    }

    public async Task<TenantContextValidationResult> ValidateContextAsync(CancellationToken cancellationToken = default)
    {
        var context = await GetCurrentContextAsync(cancellationToken);
        if (context is null || context.IsGlobal)
        {
            _metrics.RecordContextValidationFailed();
            return TenantContextValidationResult.Failure("ContextRequired", "A college context is required.");
        }

        if (context.SelectedCollegeId is not int collegeId)
        {
            _metrics.RecordContextValidationFailed();
            return TenantContextValidationResult.Failure("ContextRequired", "A college context is required.");
        }

        if (_expiration.IsExpired(context))
        {
            await ExpireStoredContextAsync(_currentUser.UserId, context, cancellationToken);
            _metrics.RecordContextValidationFailed();
            return TenantContextValidationResult.Failure("ContextExpired", "Operational context has expired. Select a college again.");
        }

        var result = await _catalog.ValidateCollegeSelectionAsync(
            collegeId,
            _currentUser.UserId,
            _currentUser.Role,
            _currentUser.TenantId,
            cancellationToken);

        if (!result.IsValid)
        {
            _metrics.RecordContextValidationFailed();
        }

        return result;
    }

    public bool IsGlobalContext()
    {
        var context = _requestCache;
        return context?.IsGlobal == true || (IsSuperAdmin() && context?.SelectedCollegeId is null);
    }

    public bool IsCollegeContext()
    {
        var context = _requestCache;
        return context?.ContextType == ContextType.College && context.SelectedCollegeId is > 0;
    }

    public TenantContextResolution ResolveForOperation()
    {
        if (!IsSuperAdmin())
        {
            if (_currentUser.TenantId <= 0)
            {
                return TenantContextResolution.ContextRequired("Your account is not assigned to a college tenant.");
            }

            var cached = _requestCache;
            if (cached is not null && cached.TenantId > 0)
            {
                return TenantContextResolution.FromContext(cached);
            }

            return TenantContextResolution.FromContext(new TenantContextSnapshot
            {
                UserId = _currentUser.UserId,
                Role = _currentUser.Role,
                TenantId = _currentUser.TenantId,
                ContextType = ContextType.College,
                CreatedUtc = DateTime.UtcNow,
                IsGlobal = false,
                ContextSource = "JwtTenant",
            });
        }

        if (_requestCache is { IsGlobal: false, SelectedCollegeId: > 0, TenantId: > 0 })
        {
            if (_expiration.IsExpired(_requestCache))
            {
                return TenantContextResolution.ContextRequired("Operational context has expired. Select a college again.");
            }

            return TenantContextResolution.FromContext(_requestCache);
        }

        return TenantContextResolution.ContextRequired();
    }

    public async Task ApplyOperationalTenantAsync(CancellationToken cancellationToken = default)
    {
        var context = await GetCurrentContextAsync(cancellationToken);
        if (context is { IsGlobal: false, TenantId: > 0 })
        {
            _tenantContextAccessor.SetTenant(context.TenantId);
        }
    }

    private async Task ExpireStoredContextAsync(int userId, TenantContextSnapshot context, CancellationToken cancellationToken)
    {
        await _store.RemoveAsync(userId, cancellationToken);
        _requestCache = BuildGlobalContext("Expired");
        _tenantContextAccessor.Clear();

        await _auditService.RecordAsync(
            "TenantContext",
            userId.ToString(),
            AuditAction.Custom,
            newValues: new { Action = "ContextExpired", context.SelectedCollegeId });

        await _events.PublishContextExpiredAsync(userId, cancellationToken);
        _metrics.RecordContextExpired();
    }

    private async Task<TenantContextSnapshot?> BuildCollegeAdminContextAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.TenantId <= 0)
        {
            return null;
        }

        var college = await _context.Colleges.AsNoTracking()
            .Where(c => c.TenantId == _currentUser.TenantId)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Name, c.Code, c.TenantId })
            .FirstOrDefaultAsync(cancellationToken);

        return new TenantContextSnapshot
        {
            UserId = _currentUser.UserId,
            Role = _currentUser.Role,
            SelectedCollegeId = college?.Id,
            SelectedCollegeName = college?.Name,
            SelectedCollegeCode = college?.Code,
            TenantId = _currentUser.TenantId,
            ContextType = ContextType.College,
            CreatedUtc = DateTime.UtcNow,
            IsGlobal = false,
            ContextSource = "JwtTenant",
        };
    }

    private TenantContextSnapshot BuildGlobalContext(string source) =>
        new()
        {
            UserId = _currentUser.UserId,
            Role = _currentUser.Role,
            TenantId = 0,
            ContextType = ContextType.Global,
            CreatedUtc = DateTime.UtcNow,
            IsGlobal = true,
            ContextSource = source,
        };

    private bool IsSuperAdmin() =>
        string.Equals(_currentUser.Role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase);
}
