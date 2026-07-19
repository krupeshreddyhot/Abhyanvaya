using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Abhyanvaya.Infrastructure.TenantContext;

public sealed class TenantContextCollegeCatalog : ITenantContextCollegeCatalog
{
    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public TenantContextCollegeCatalog(IApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<PagedCollegesResult> GetAccessibleCollegesAsync(
        int userId,
        string role,
        int jwtTenantId,
        AvailableCollegesQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<Domain.Entities.College> colleges = IsSuperAdmin(role)
            ? _context.Colleges.IgnoreQueryFilters().AsNoTracking().Where(c => !c.IsDeleted)
            : _context.Colleges.AsNoTracking().Where(c => c.TenantId == jwtTenantId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            colleges = colleges.Where(c =>
                c.Name.Contains(term) ||
                c.Code.Contains(term) ||
                (c.ShortName != null && c.ShortName.Contains(term)));
        }

        var total = await colleges.CountAsync(cancellationToken);
        var rows = await colleges
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.TenantId,
                c.Name,
                c.Code,
                c.ShortName,
                c.IsDeleted,
                c.UniversityId,
            })
            .ToListAsync(cancellationToken);

        var universityIds = rows.Select(r => r.UniversityId).Distinct().ToList();
        var universityNames = universityIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Universities.AsNoTracking()
                .Where(u => universityIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        var items = rows.Select(c => new AvailableCollegeDto
        {
            Id = c.Id,
            TenantId = c.TenantId,
            Name = c.Name,
            Code = c.Code,
            ShortName = c.ShortName,
            Status = c.IsDeleted ? "Deleted" : "Active",
            AiEnabled = IsAiEnabled(c.TenantId),
            UniversityName = universityNames.GetValueOrDefault(c.UniversityId),
        }).ToList();

        return new PagedCollegesResult
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<TenantContextValidationResult> ValidateCollegeSelectionAsync(
        int collegeId,
        int userId,
        string role,
        int jwtTenantId,
        CancellationToken cancellationToken = default)
    {
        var college = await FindCollegeAsync(collegeId, role, jwtTenantId, cancellationToken);
        if (college is null)
        {
            return TenantContextValidationResult.Failure("CollegeNotFound", "The selected college was not found or is not accessible.");
        }

        return ValidateCollegeRow(college);
    }

    internal TenantContextValidationResult ValidateCollegeRow(CollegeContextRow college)
    {
        if (college.IsDeleted)
        {
            return TenantContextValidationResult.Failure("CollegeDeleted", "The selected college has been deleted.");
        }

        if (!IsAiEnabled(college.TenantId))
        {
            return TenantContextValidationResult.Failure("AiDisabled", "AI features are disabled for this college.");
        }

        if (!IsSubscriptionActive(college.TenantId))
        {
            return TenantContextValidationResult.Failure("SubscriptionExpired", "The subscription for this college is not active.");
        }

        return TenantContextValidationResult.Success();
    }

    internal async Task<CollegeContextRow?> FindCollegeAsync(
        int collegeId,
        string role,
        int jwtTenantId,
        CancellationToken cancellationToken)
    {
        IQueryable<Domain.Entities.College> query = _context.Colleges
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.Id == collegeId);

        if (!IsSuperAdmin(role))
        {
            query = query.Where(c => c.TenantId == jwtTenantId);
        }

        return await query
            .Select(c => new CollegeContextRow(c.Id, c.Name, c.Code, c.TenantId, c.IsDeleted))
            .FirstOrDefaultAsync(cancellationToken);
    }

    internal sealed record CollegeContextRow(int Id, string Name, string Code, int TenantId, bool IsDeleted);

    private bool IsAiEnabled(int tenantId)
    {
        var disabled = _configuration.GetSection("TenantContext:DisabledAiTenantIds").Get<int[]>() ?? Array.Empty<int>();
        return !disabled.Contains(tenantId);
    }

    private bool IsSubscriptionActive(int tenantId)
    {
        var expired = _configuration.GetSection("TenantContext:ExpiredSubscriptionTenantIds").Get<int[]>() ?? Array.Empty<int>();
        return !expired.Contains(tenantId);
    }

    private static bool IsSuperAdmin(string role) =>
        string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase);
}
