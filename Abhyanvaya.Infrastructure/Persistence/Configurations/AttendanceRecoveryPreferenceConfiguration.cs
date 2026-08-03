using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class AttendanceRecoveryPreferenceConfiguration : IEntityTypeConfiguration<AttendanceRecoveryPreference>
{
    public void Configure(EntityTypeBuilder<AttendanceRecoveryPreference> builder)
    {
        builder.ToTable("AttendanceRecoveryPreference");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DefaultLandingPage).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.StaffId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.UserId });
    }
}
