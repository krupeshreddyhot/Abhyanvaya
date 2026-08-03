using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class AttendanceRetryHistoryConfiguration : IEntityTypeConfiguration<AttendanceRetryHistory>
{
    public void Configure(EntityTypeBuilder<AttendanceRetryHistory> builder)
    {
        builder.ToTable("AttendanceRetryHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Stage).HasMaxLength(64);
        builder.Property(x => x.Action).HasMaxLength(64);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.Property(x => x.PerformedUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.TenantId, x.AttendanceSessionId, x.PerformedUtc });
        builder.HasOne(x => x.AttendanceSession)
            .WithMany()
            .HasForeignKey(x => x.AttendanceSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
