using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class EnrollmentWorkLeaseConfiguration : IEntityTypeConfiguration<EnrollmentWorkLease>
{
    public void Configure(EntityTypeBuilder<EnrollmentWorkLease> builder)
    {
        builder.ToTable("EnrollmentWorkLease");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.WorkerId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.NodeId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.AcquiredUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ExpiresUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.HeartbeatUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ReleasedUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.PipelineState).HasConversion<int>();

        builder.Property(x => x.LeaseVersion)
            .IsConcurrencyToken()
            .HasColumnType("bytea")
            .IsRequired();

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ItemId)
            .IsUnique()
            .HasFilter("\"IsActive\" = TRUE")
            .HasDatabaseName("UX_EnrollmentWorkLease_ActiveItem");

        builder.HasIndex(x => new { x.IsActive, x.ExpiresUtc })
            .HasDatabaseName("IX_EnrollmentWorkLease_Active_Expires");
    }
}
