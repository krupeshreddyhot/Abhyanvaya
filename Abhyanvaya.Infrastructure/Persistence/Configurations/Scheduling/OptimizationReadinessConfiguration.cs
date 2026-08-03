using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class OptimizationSimulationRunConfiguration : IEntityTypeConfiguration<OptimizationSimulationRun>
{
    public void Configure(EntityTypeBuilder<OptimizationSimulationRun> builder)
    {
        builder.ToTable("SchedulingOptimizationSimulationRun");
        builder.Property(x => x.ScenarioName).HasMaxLength(160);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.Property(x => x.MetricsJson).HasColumnType("text");
        builder.Property(x => x.ProposedChangesJson).HasColumnType("text");
        builder.Property(x => x.StrategyKind).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.CurrentScore).HasPrecision(18, 4);
        builder.Property(x => x.ProjectedScore).HasPrecision(18, 4);
        builder.Property(x => x.ScoreDelta).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.TenantId, x.SimulationId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.StartedUtc });
        builder.Ignore(x => x.AppliesTimetableChanges);
    }
}

public sealed class OptimizationMetricSnapshotConfiguration : IEntityTypeConfiguration<OptimizationMetricSnapshot>
{
    public void Configure(EntityTypeBuilder<OptimizationMetricSnapshot> builder)
    {
        builder.ToTable("SchedulingOptimizationMetricSnapshot");
        builder.Property(x => x.MetricName).HasMaxLength(160);
        builder.Property(x => x.Unit).HasMaxLength(40);
        builder.Property(x => x.MetricKind).HasConversion<byte>();
        builder.Property(x => x.Value).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.CapturedUtc });
        builder.HasIndex(x => x.SnapshotId);
    }
}

public sealed class OptimizationTelemetryAggregateConfiguration : IEntityTypeConfiguration<OptimizationTelemetryAggregate>
{
    public void Configure(EntityTypeBuilder<OptimizationTelemetryAggregate> builder)
    {
        builder.ToTable("SchedulingOptimizationTelemetryAggregate");
        builder.Property(x => x.MetricKey).HasMaxLength(120);
        builder.Property(x => x.AverageValue).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.TenantId, x.MetricKey }).IsUnique();
    }
}
