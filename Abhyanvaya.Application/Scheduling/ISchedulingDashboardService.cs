using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public interface ISchedulingDashboardService
{
    Task<SchedulingDashboardDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
