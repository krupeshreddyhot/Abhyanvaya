using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class OptimizationEngineRunConfiguration : IEntityTypeConfiguration<OptimizationEngineRun>
{
    public void Configure(EntityTypeBuilder<OptimizationEngineRun> builder)
    {
        builder.ToTable("SchedulingOptimizationEngineRun");
        builder.Property(x => x.StrategyPipelineCsv).HasMaxLength(400);
        builder.Property(x => x.CurrentStrategy).HasMaxLength(120);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.StrategyKind).HasConversion<byte>();
        builder.Property(x => x.BaselineScore).HasPrecision(18, 4);
        builder.Property(x => x.ProjectedScore).HasPrecision(18, 4);
        builder.Property(x => x.ImprovementDelta).HasPrecision(18, 4);
        builder.Property(x => x.CandidatesJson).HasColumnType("text");
        builder.Property(x => x.ComparisonJson).HasColumnType("text");
        builder.Property(x => x.MetricsJson).HasColumnType("text");
        builder.Property(x => x.IntermediateResultsJson).HasColumnType("text");
        builder.HasIndex(x => new { x.TenantId, x.RunId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.StartedUtc });
        builder.Ignore(x => x.ModifiesProductionTimetable);
    }
}
