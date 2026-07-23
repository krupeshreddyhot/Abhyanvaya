using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class AttendanceSessionImageConfiguration : IEntityTypeConfiguration<AttendanceSessionImage>
{
    public void Configure(EntityTypeBuilder<AttendanceSessionImage> builder)
    {
        builder.ToTable("AttendanceSessionImage");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ImageKey)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnType("character varying(500)");

        builder.Property(x => x.ImageHash)
            .HasMaxLength(128)
            .HasColumnType("character varying(128)");

        builder.Property(x => x.OriginalFileName)
            .HasMaxLength(255)
            .HasColumnType("character varying(255)");

        builder.Property(x => x.CaptureDevice)
            .HasMaxLength(100)
            .HasColumnType("character varying(100)");

        builder.Property(x => x.AcquisitionMethod)
            .HasMaxLength(32)
            .HasColumnType("character varying(32)");

        builder.Property(x => x.ThumbnailImageKey)
            .HasMaxLength(500)
            .HasColumnType("character varying(500)");

        builder.Property(x => x.AnnotatedImageKey)
            .HasMaxLength(500)
            .HasColumnType("character varying(500)");

        builder.Property(x => x.ProcessingError)
            .HasMaxLength(2000)
            .HasColumnType("character varying(2000)");

        builder.Property(x => x.Status)
            .HasConversion<short>();

        builder.HasIndex(x => new { x.AttendanceSessionId, x.ImageSequence })
            .IsUnique()
            .HasDatabaseName("IX_AttendanceSessionImage_Session_Sequence");

        builder.HasIndex(x => new { x.TenantId, x.AttendanceSessionId })
            .HasDatabaseName("IX_AttendanceSessionImage_Tenant_Session");

        builder.HasOne(x => x.AttendanceSession)
            .WithMany()
            .HasForeignKey(x => x.AttendanceSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
