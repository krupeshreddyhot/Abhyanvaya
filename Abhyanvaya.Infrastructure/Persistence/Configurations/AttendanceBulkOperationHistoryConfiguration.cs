using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class AttendanceBulkOperationHistoryConfiguration : IEntityTypeConfiguration<AttendanceBulkOperationHistory>
{
    public void Configure(EntityTypeBuilder<AttendanceBulkOperationHistory> builder)
    {
        builder.ToTable("AttendanceBulkOperationHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Operation).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.StartedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.TenantId, x.StartedUtc });
    }
}
