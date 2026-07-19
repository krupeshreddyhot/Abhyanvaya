using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Background;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Enrollment.Background;

public sealed class EnrollmentLeaseManager : IEnrollmentLeaseManager
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;
    private readonly EnrollmentBackgroundOptions _options;
    private readonly ILogger<EnrollmentLeaseManager> _logger;

    public EnrollmentLeaseManager(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        IOptions<EnrollmentBackgroundOptions> options,
        ILogger<EnrollmentLeaseManager> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EnrollmentLease?> AcquireAsync(
        EnrollmentWorkItem workItem,
        string workerId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var utcNow = _clock.GetUtcNow().UtcDateTime;
        var leaseDuration = TimeSpan.FromSeconds(Math.Max(30, _options.LeaseDurationSeconds));
        var entity = new EnrollmentWorkLease
        {
            Id = Guid.NewGuid(),
            TenantId = workItem.TenantId,
            ItemId = workItem.ItemId,
            BatchId = workItem.BatchId,
            StudentId = workItem.StudentId,
            WorkerId = workerId,
            NodeId = nodeId,
            AcquiredUtc = utcNow,
            ExpiresUtc = utcNow.Add(leaseDuration),
            HeartbeatUtc = utcNow,
            RenewalCount = 0,
            CorrelationId = workItem.CorrelationId,
            LeaseVersion = Guid.NewGuid().ToByteArray(),
            PipelineState = EnrollmentWorkerState.Running,
            IsActive = true,
        };

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _context.AddAsync(entity);
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            _logger.LogWarning(
                "Lease acquisition failed due to duplicate active lease. ItemId={ItemId} WorkerId={WorkerId}",
                workItem.ItemId,
                workerId);
            return null;
        }

        _ = new LeaseAcquired(entity.Id, entity.ItemId, workerId, entity.ExpiresUtc);

        _logger.LogInformation(
            "Lease acquired. LeaseId={LeaseId} ItemId={ItemId} WorkerId={WorkerId} CorrelationId={CorrelationId}",
            entity.Id,
            entity.ItemId,
            workerId,
            entity.CorrelationId);

        return Map(entity);
    }

    public async Task<bool> RenewAsync(EnrollmentLease lease, CancellationToken cancellationToken = default)
    {
        var entity = await _context.EnrollmentWorkLeases
            .FirstOrDefaultAsync(l => l.Id == lease.LeaseId && l.IsActive, cancellationToken);

        if (entity == null || entity.WorkerId != lease.WorkerId)
        {
            return false;
        }

        var utcNow = _clock.GetUtcNow().UtcDateTime;
        var leaseDuration = TimeSpan.FromSeconds(Math.Max(30, _options.LeaseDurationSeconds));
        entity.ExpiresUtc = utcNow.Add(leaseDuration);
        entity.HeartbeatUtc = utcNow;
        entity.RenewalCount++;
        entity.LeaseVersion = Guid.NewGuid().ToByteArray();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _ = new LeaseRenewed(entity.Id, entity.ItemId, entity.WorkerId, entity.ExpiresUtc, entity.RenewalCount);
        return true;
    }

    public async Task ReleaseAsync(EnrollmentLease lease, CancellationToken cancellationToken = default)
    {
        var entity = await _context.EnrollmentWorkLeases
            .FirstOrDefaultAsync(l => l.Id == lease.LeaseId, cancellationToken);

        if (entity == null)
        {
            return;
        }

        entity.IsActive = false;
        entity.ReleasedUtc = _clock.GetUtcNow().UtcDateTime;
        entity.LeaseVersion = Guid.NewGuid().ToByteArray();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Lease released. LeaseId={LeaseId} ItemId={ItemId} WorkerId={WorkerId}",
            lease.LeaseId,
            lease.ItemId,
            lease.WorkerId);
    }

    public async Task<int> ExpireAbandonedLeasesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = _clock.GetUtcNow().UtcDateTime;
        var expired = await _context.EnrollmentWorkLeases
            .Where(l => l.IsActive && l.ExpiresUtc < utcNow)
            .ToListAsync(cancellationToken);

        foreach (var lease in expired)
        {
            lease.IsActive = false;
            lease.ReleasedUtc = utcNow;
            lease.PipelineState = EnrollmentWorkerState.Failed;
            lease.LeaseVersion = Guid.NewGuid().ToByteArray();
            _ = new LeaseExpired(lease.Id, lease.ItemId, lease.WorkerId);
        }

        if (expired.Count == 0)
        {
            return 0;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    private static EnrollmentLease Map(EnrollmentWorkLease entity) =>
        new()
        {
            LeaseId = entity.Id,
            WorkerId = entity.WorkerId,
            NodeId = entity.NodeId,
            ItemId = entity.ItemId,
            BatchId = entity.BatchId,
            TenantId = entity.TenantId,
            StudentId = entity.StudentId,
            AcquiredUtc = entity.AcquiredUtc,
            ExpiresUtc = entity.ExpiresUtc,
            HeartbeatUtc = entity.HeartbeatUtc,
            RenewalCount = entity.RenewalCount,
            CorrelationId = entity.CorrelationId,
            LeaseVersion = entity.LeaseVersion,
            PipelineState = entity.PipelineState,
        };
}
