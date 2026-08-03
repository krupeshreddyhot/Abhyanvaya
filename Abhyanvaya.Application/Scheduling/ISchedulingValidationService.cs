using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public interface ISchedulingValidationService
{
    Task<SchedulingValidationReportDto> GetReportAsync(CancellationToken cancellationToken = default);
}
