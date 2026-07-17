using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Background;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Enrollment.Background;

public sealed class EnrollmentHeartbeatService : IEnrollmentHeartbeatService
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public EnrollmentHeartbeatService(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task UpdateAsync(
        EnrollmentLease lease,
        EnrollmentWorkerState pipelineState,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.EnrollmentWorkLeases
            .FirstOrDefaultAsync(l => l.Id == lease.LeaseId && l.IsActive, cancellationToken);

        if (entity == null || entity.WorkerId != lease.WorkerId)
        {
            return;
        }

        var utcNow = _clock.GetUtcNow().UtcDateTime;
        entity.HeartbeatUtc = utcNow;
        entity.PipelineState = pipelineState;
        entity.LeaseVersion = Guid.NewGuid().ToByteArray();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
