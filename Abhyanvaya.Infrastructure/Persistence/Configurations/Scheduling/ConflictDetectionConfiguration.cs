using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class ConflictDetectionRunConfiguration : IEntityTypeConfiguration<ConflictDetectionRun>
{
    public void Configure(EntityTypeBuilder<ConflictDetectionRun> builder)
    {
        builder.ToTable("SchedulingConflictDetectionRun");
        builder.Property(x => x.Status).HasMaxLength(40);
        builder.Property(x => x.TriggerSource).HasMaxLength(40);
        builder.HasOne(x => x.Timetable).WithMany().HasForeignKey(x => x.TimetableId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.StartedUtc });
        builder.HasIndex(x => new { x.TenantId, x.TimetableId, x.StartedUtc });
    }
}

public sealed class ConflictFindingConfiguration : IEntityTypeConfiguration<ConflictFinding>
{
    public void Configure(EntityTypeBuilder<ConflictFinding> builder)
    {
        builder.ToTable("SchedulingConflictFinding");
        builder.Property(x => x.RuleCode).HasMaxLength(80);
        builder.Property(x => x.RuleName).HasMaxLength(160);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.WhyOccurred).HasMaxLength(2000);
        builder.Property(x => x.SuggestedResolution).HasMaxLength(2000);
        builder.Property(x => x.NavigationPath).HasMaxLength(500);
        builder.Property(x => x.Category).HasConversion<byte>();
        builder.Property(x => x.Severity).HasConversion<byte>();
        builder.HasOne(x => x.ConflictDetectionRun).WithMany(r => r.Findings)
            .HasForeignKey(x => x.ConflictDetectionRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.ConflictDetectionRunId, x.Category, x.Severity });
        builder.HasIndex(x => new { x.TenantId, x.StaffId });
        builder.HasIndex(x => new { x.TenantId, x.RoomId });
        builder.HasIndex(x => new { x.TenantId, x.TimetableEntryId });
    }
}
