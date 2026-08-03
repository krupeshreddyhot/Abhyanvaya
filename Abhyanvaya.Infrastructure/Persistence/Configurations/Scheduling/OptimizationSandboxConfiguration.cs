using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class OptimizationScenarioConfiguration : IEntityTypeConfiguration<OptimizationScenario>
{
    public void Configure(EntityTypeBuilder<OptimizationScenario> builder)
    {
        builder.ToTable("SchedulingOptimizationScenario");
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.TagsCsv).HasMaxLength(500);
        builder.Property(x => x.Category).HasMaxLength(80);
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.CurrentScore).HasPrecision(18, 4);
        builder.Property(x => x.ProjectedScore).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.TenantId, x.ScenarioId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.OwnerUserId, x.IsFavorite });
        builder.Ignore(x => x.ModifiesProductionTimetable);
        builder.HasMany(x => x.Snapshots).WithOne(x => x.Scenario).HasForeignKey(x => x.OptimizationScenarioId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class OptimizationSnapshotConfiguration : IEntityTypeConfiguration<OptimizationSnapshot>
{
    public void Configure(EntityTypeBuilder<OptimizationSnapshot> builder)
    {
        builder.ToTable("SchedulingOptimizationSnapshot");
        builder.Property(x => x.Label).HasMaxLength(120);
        builder.Property(x => x.TimetableSummaryJson).HasColumnType("text");
        builder.Property(x => x.SimulationJson).HasColumnType("text");
        builder.Property(x => x.ScoresJson).HasColumnType("text");
        builder.Property(x => x.ConflictSummaryJson).HasColumnType("text");
        builder.Property(x => x.MetricsJson).HasColumnType("text");
        builder.Property(x => x.RecommendationsJson).HasColumnType("text");
        builder.HasIndex(x => new { x.TenantId, x.SnapshotId }).IsUnique();
        builder.HasIndex(x => new { x.OptimizationScenarioId, x.Sequence });
    }
}

public sealed class OptimizationScenarioFavoriteConfiguration : IEntityTypeConfiguration<OptimizationScenarioFavorite>
{
    public void Configure(EntityTypeBuilder<OptimizationScenarioFavorite> builder)
    {
        builder.ToTable("SchedulingOptimizationScenarioFavorite");
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.OptimizationScenarioId });
    }
}

public sealed class OptimizationScenarioNoteConfiguration : IEntityTypeConfiguration<OptimizationScenarioNote>
{
    public void Configure(EntityTypeBuilder<OptimizationScenarioNote> builder)
    {
        builder.ToTable("SchedulingOptimizationScenarioNote");
        builder.Property(x => x.NoteText).HasMaxLength(2000);
    }
}

public sealed class OptimizationScenarioCommentConfiguration : IEntityTypeConfiguration<OptimizationScenarioComment>
{
    public void Configure(EntityTypeBuilder<OptimizationScenarioComment> builder)
    {
        builder.ToTable("SchedulingOptimizationScenarioComment");
        builder.Property(x => x.CommentText).HasMaxLength(2000);
    }
}

public sealed class OptimizationScenarioBookmarkConfiguration : IEntityTypeConfiguration<OptimizationScenarioBookmark>
{
    public void Configure(EntityTypeBuilder<OptimizationScenarioBookmark> builder)
    {
        builder.ToTable("SchedulingOptimizationScenarioBookmark");
        builder.Property(x => x.Name).HasMaxLength(160);
    }
}

public sealed class OptimizationScenarioApprovalRequestConfiguration : IEntityTypeConfiguration<OptimizationScenarioApprovalRequest>
{
    public void Configure(EntityTypeBuilder<OptimizationScenarioApprovalRequest> builder)
    {
        builder.ToTable("SchedulingOptimizationScenarioApprovalRequest");
        builder.Property(x => x.Status).HasMaxLength(40);
        builder.Property(x => x.Message).HasMaxLength(1000);
    }
}

public sealed class OptimizationScenarioShareConfiguration : IEntityTypeConfiguration<OptimizationScenarioShare>
{
    public void Configure(EntityTypeBuilder<OptimizationScenarioShare> builder)
    {
        builder.ToTable("SchedulingOptimizationScenarioShare");
        builder.HasIndex(x => new { x.TenantId, x.OptimizationScenarioId, x.SharedWithUserId });
    }
}

public sealed class OptimizationScenarioHistoryConfiguration : IEntityTypeConfiguration<OptimizationScenarioHistory>
{
    public void Configure(EntityTypeBuilder<OptimizationScenarioHistory> builder)
    {
        builder.ToTable("SchedulingOptimizationScenarioHistory");
        builder.Property(x => x.Action).HasConversion<byte>();
        builder.Property(x => x.Details).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.OptimizationScenarioId, x.OccurredUtc });
    }
}
