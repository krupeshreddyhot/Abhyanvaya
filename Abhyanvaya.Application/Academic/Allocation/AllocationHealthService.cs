using Abhyanvaya.Application.Academic.Observability;

namespace Abhyanvaya.Application.Academic.Allocation;

public sealed class AllocationHealthService : IAllocationHealthService
{
    private readonly ISectionAllocationContextBuilder _builder;
    private readonly IAllocationReadinessService _readiness;
    private readonly ISectionAllocationContextValidator _validator;
    private readonly IAcademicTelemetryService _telemetry;

    public AllocationHealthService(
        ISectionAllocationContextBuilder builder,
        IAllocationReadinessService readiness,
        ISectionAllocationContextValidator validator,
        IAcademicTelemetryService telemetry)
    {
        _builder = builder;
        _readiness = readiness;
        _validator = validator;
        _telemetry = telemetry;
    }

    public Task<AllocationHealthReport> EvaluateAsync(
        AllocationScopeRequest scope,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.AllocationHealth,
            "AllocationHealth.Evaluate",
            ct => EvaluateCoreAsync(scope, ct),
            cancellationToken);

    private async Task<AllocationHealthReport> EvaluateCoreAsync(AllocationScopeRequest scope, CancellationToken ct)
    {
        var ctx = await _builder.BuildAsync(scope, ct);
        var ready = await _readiness.EvaluateAsync(scope, ct);
        var validation = await _validator.ValidateAsync(ctx, ct);

        var dims = new List<AllocationHealthDimension>
        {
            new()
            {
                Area = "Context",
                Status = ctx.Sections.Count == 0 ? "Critical" : "Healthy",
                Message = $"{ctx.Sections.Count} sections, checksum {ctx.Checksum[..Math.Min(8, ctx.Checksum.Length)]}…",
            },
            new()
            {
                Area = "Readiness",
                Status = ready.OverallStatus switch
                {
                    "Blocked" => "Critical",
                    "Warning" => "Warning",
                    _ => "Healthy"
                },
                Message = ready.OverallStatus,
            },
            new()
            {
                Area = "Policies",
                Status = ctx.Policies.Count == 0 && ctx.Sections.Count > 0 ? "Warning" : "Healthy",
                Message = $"{ctx.Policies.Count} policy lines",
            },
            new()
            {
                Area = "Capacity",
                Status = ctx.Capacities.Any(c => c.CurrentStrength > c.MaximumCapacity && c.MaximumCapacity > 0)
                    ? "Critical"
                    : ctx.Capacities.Any(c => c.OccupancyPercent >= 90) ? "Warning" : "Healthy",
                Message = $"Avg occupancy context health={ctx.OverallHealth}",
            },
            new()
            {
                Area = "Students",
                Status = ctx.Students.Count == 0 ? "Warning" : "Healthy",
                Message = $"{ctx.Students.Count} students",
            },
            new()
            {
                Area = "Sections",
                Status = validation.IsValid ? "Healthy" : "Critical",
                Message = validation.IsValid ? "Validation passed" : string.Join("; ", validation.Errors),
            },
        };

        var overall = dims.Any(d => d.Status == "Critical") ? "Critical"
            : dims.Any(d => d.Status == "Warning") ? "Warning"
            : "Healthy";

        return new AllocationHealthReport { OverallStatus = overall, Dimensions = dims };
    }
}
