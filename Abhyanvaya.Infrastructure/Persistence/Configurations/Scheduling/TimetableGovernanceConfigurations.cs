using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class ScheduleVersionConfiguration : IEntityTypeConfiguration<ScheduleVersion>
{
    public void Configure(EntityTypeBuilder<ScheduleVersion> builder)
    {
        builder.ToTable("SchedulingScheduleVersion");
        builder.Property(x => x.VersionName).HasMaxLength(200);
        builder.Property(x => x.Remarks).HasMaxLength(2000);
        builder.Property(x => x.ArchiveComments).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicTerm).WithMany().HasForeignKey(x => x.AcademicTermId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ParentVersion).WithMany().HasForeignKey(x => x.ParentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ArchiveReason).WithMany().HasForeignKey(x => x.ArchiveReasonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReferenceVersion).WithMany().HasForeignKey(x => x.ReferenceVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.AcademicTermId, x.VersionNumber }).IsUnique();
    }
}

public sealed class TimetableApprovalRequestConfiguration : IEntityTypeConfiguration<TimetableApprovalRequest>
{
    public void Configure(EntityTypeBuilder<TimetableApprovalRequest> builder)
    {
        builder.ToTable("SchedulingTimetableApprovalRequest");
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.HasOne(x => x.ScheduleVersion).WithMany().HasForeignKey(x => x.ScheduleVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Timetable).WithMany().HasForeignKey(x => x.TimetableId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TimetableApprovalStepConfiguration : IEntityTypeConfiguration<TimetableApprovalStep>
{
    public void Configure(EntityTypeBuilder<TimetableApprovalStep> builder)
    {
        builder.ToTable("SchedulingTimetableApprovalStep");
        builder.Property(x => x.RoleKey).HasMaxLength(100);
        builder.Property(x => x.Comments).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Decision).HasConversion<byte>();
        builder.HasOne(x => x.Request).WithMany(r => r.Steps).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.RequestId, x.StepOrder }).IsUnique();
    }
}

public sealed class TimetableApprovalHistoryConfiguration : IEntityTypeConfiguration<TimetableApprovalHistory>
{
    public void Configure(EntityTypeBuilder<TimetableApprovalHistory> builder)
    {
        builder.ToTable("SchedulingTimetableApprovalHistory");
        builder.Property(x => x.Comments).HasMaxLength(2000);
        builder.Property(x => x.Decision).HasConversion<byte>();
        builder.Property(x => x.OldStatus).HasConversion<byte>();
        builder.Property(x => x.NewStatus).HasConversion<byte>();
        builder.HasOne(x => x.Request).WithMany(r => r.History).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TimetableApprovalCommentConfiguration : IEntityTypeConfiguration<TimetableApprovalComment>
{
    public void Configure(EntityTypeBuilder<TimetableApprovalComment> builder)
    {
        builder.ToTable("SchedulingTimetableApprovalComment");
        builder.Property(x => x.Comment).HasMaxLength(4000);
        builder.HasOne(x => x.Request).WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.RequestId, x.OccurredUtc });
    }
}

public sealed class TimetableDecisionHistoryConfiguration : IEntityTypeConfiguration<TimetableDecisionHistory>
{
    public void Configure(EntityTypeBuilder<TimetableDecisionHistory> builder)
    {
        builder.ToTable("SchedulingTimetableDecisionHistory");
        builder.Property(x => x.Action).HasMaxLength(100);
        builder.Property(x => x.Comment).HasMaxLength(2000);
        builder.Property(x => x.DecisionNotes).HasMaxLength(2000);
        builder.Property(x => x.ReviewerRemarks).HasMaxLength(2000);
        builder.Property(x => x.Decision).HasConversion<byte>();
        builder.Property(x => x.OldStatus).HasConversion<byte>();
        builder.Property(x => x.NewStatus).HasConversion<byte>();
        builder.HasOne(x => x.Request).WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.RequestId, x.OccurredUtc });
    }
}

public sealed class ArchiveReasonLookupConfiguration : IEntityTypeConfiguration<ArchiveReasonLookup>
{
    public void Configure(EntityTypeBuilder<ArchiveReasonLookup> builder)
    {
        builder.ToTable("SchedulingArchiveReason");
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Code).HasConversion<byte>();
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public sealed class TimetableCloneJobConfiguration : IEntityTypeConfiguration<TimetableCloneJob>
{
    public void Configure(EntityTypeBuilder<TimetableCloneJob> builder)
    {
        builder.ToTable("SchedulingTimetableCloneJob");
        builder.Property(x => x.PayloadJson).HasMaxLength(8000);
        builder.Property(x => x.Summary).HasMaxLength(2000);
        builder.Property(x => x.Error).HasMaxLength(4000);
        builder.Property(x => x.JobType).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.HasOne(x => x.SourceTimetable).WithMany().HasForeignKey(x => x.SourceTimetableId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TargetTimetable).WithMany().HasForeignKey(x => x.TargetTimetableId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TimetableChangeHistoryConfiguration : IEntityTypeConfiguration<TimetableChangeHistory>
{
    public void Configure(EntityTypeBuilder<TimetableChangeHistory> builder)
    {
        builder.ToTable("SchedulingTimetableChangeHistory");
        builder.Property(x => x.OldValueJson).HasMaxLength(8000);
        builder.Property(x => x.NewValueJson).HasMaxLength(8000);
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.Operation).HasConversion<byte>();
        builder.HasOne(x => x.Timetable).WithMany().HasForeignKey(x => x.TimetableId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.TimetableId, x.OccurredUtc });
    }
}

public sealed class TimetableWarningDismissalConfiguration : IEntityTypeConfiguration<TimetableWarningDismissal>
{
    public void Configure(EntityTypeBuilder<TimetableWarningDismissal> builder)
    {
        builder.ToTable("SchedulingTimetableWarningDismissal");
        builder.Property(x => x.WarningCode).HasMaxLength(100);
        builder.HasOne(x => x.Timetable).WithMany().HasForeignKey(x => x.TimetableId).OnDelete(DeleteBehavior.Cascade);
    }
}
