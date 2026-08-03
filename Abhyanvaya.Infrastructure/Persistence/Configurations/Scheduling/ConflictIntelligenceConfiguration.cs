using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class ConflictRuleThresholdSettingConfiguration : IEntityTypeConfiguration<ConflictRuleThresholdSetting>
{
    public void Configure(EntityTypeBuilder<ConflictRuleThresholdSetting> builder)
    {
        builder.ToTable("SchedulingConflictRuleThresholdSetting");
        builder.Property(x => x.ThresholdKey).HasMaxLength(80);
        builder.Property(x => x.DisplayName).HasMaxLength(160);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Unit).HasMaxLength(40);
        builder.Property(x => x.Value).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.TenantId, x.ThresholdKey }).IsUnique();
    }
}

public sealed class ConflictRuleConfigChangeHistoryConfiguration : IEntityTypeConfiguration<ConflictRuleConfigChangeHistory>
{
    public void Configure(EntityTypeBuilder<ConflictRuleConfigChangeHistory> builder)
    {
        builder.ToTable("SchedulingConflictRuleConfigChangeHistory");
        builder.Property(x => x.ThresholdKey).HasMaxLength(80);
        builder.Property(x => x.ChangeReason).HasMaxLength(500);
        builder.Property(x => x.OldValue).HasPrecision(18, 4);
        builder.Property(x => x.NewValue).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.TenantId, x.ThresholdKey, x.ChangedUtc });
    }
}

public sealed class ConflictWorkspacePinConfiguration : IEntityTypeConfiguration<ConflictWorkspacePin>
{
    public void Configure(EntityTypeBuilder<ConflictWorkspacePin> builder)
    {
        builder.ToTable("SchedulingConflictWorkspacePin");
        builder.Property(x => x.RuleCode).HasMaxLength(80);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.ConflictDetectionRunId, x.RuleCode, x.TimetableEntryId });
    }
}

public sealed class ConflictWorkspaceBookmarkConfiguration : IEntityTypeConfiguration<ConflictWorkspaceBookmark>
{
    public void Configure(EntityTypeBuilder<ConflictWorkspaceBookmark> builder)
    {
        builder.ToTable("SchedulingConflictWorkspaceBookmark");
        builder.Property(x => x.Name).HasMaxLength(160);
        builder.Property(x => x.FilterJson).HasMaxLength(4000);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Name });
    }
}

public sealed class ConflictWorkspaceNoteConfiguration : IEntityTypeConfiguration<ConflictWorkspaceNote>
{
    public void Configure(EntityTypeBuilder<ConflictWorkspaceNote> builder)
    {
        builder.ToTable("SchedulingConflictWorkspaceNote");
        builder.Property(x => x.RuleCode).HasMaxLength(80);
        builder.Property(x => x.NoteText).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.ConflictDetectionRunId });
    }
}
