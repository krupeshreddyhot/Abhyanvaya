using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class WorkspacePreferenceConfiguration : IEntityTypeConfiguration<WorkspacePreference>
{
    public void Configure(EntityTypeBuilder<WorkspacePreference> builder)
    {
        builder.ToTable("SchedulingWorkspacePreference");
        builder.Property(x => x.LandingPage).HasMaxLength(40);
        builder.Property(x => x.DashboardLayout).HasMaxLength(40);
        builder.Property(x => x.DefaultTimetableView).HasMaxLength(40);
        builder.Property(x => x.FavoriteQuickActionsCsv).HasMaxLength(500);
        builder.Property(x => x.ThemePreference).HasMaxLength(40);
        builder.Property(x => x.NotificationPreferencesJson).HasColumnType("text");
        builder.Property(x => x.RecoveryPreferencesJson).HasColumnType("text");
        builder.HasIndex(x => new { x.TenantId, x.StaffId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.UserId });
    }
}
