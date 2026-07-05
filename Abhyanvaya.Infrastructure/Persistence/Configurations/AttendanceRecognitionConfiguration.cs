using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class AttendanceRecognitionConfiguration : IEntityTypeConfiguration<AttendanceRecognition>
{
    public void Configure(EntityTypeBuilder<AttendanceRecognition> builder)
    {
        builder.ToTable("AttendanceRecognition");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.RecognitionStatus)
            .HasConversion<int>()
            .HasDefaultValue(RecognitionStatus.Unknown);

        builder.Property(x => x.ConfidenceScore)
            .HasPrecision(5, 2);

        builder.Property(x => x.EmbeddingDistance)
            .HasPrecision(10, 6);

        builder.Property(x => x.ReviewNotes)
            .HasMaxLength(500)
            .HasColumnType("character varying(500)");

        builder.Property(x => x.VerifiedByTeacher)
            .HasDefaultValue(false);

        builder.Property(x => x.TeacherOverride)
            .HasDefaultValue(false);

        builder.Property(x => x.ImageSequence)
            .HasColumnType("smallint")
            .HasDefaultValue((short)1);

        builder.Property(x => x.FaceImageKey)
            .HasMaxLength(500)
            .HasColumnType("character varying(500)");

        builder.Property(x => x.CreatedUtc)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.RowVersion)
            .IsConcurrencyToken()
            .HasColumnType("bytea")
            .IsRequired();

        builder.HasOne(x => x.AttendanceSession)
            .WithMany(s => s.Recognitions)
            .HasForeignKey(x => x.AttendanceSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Student)
            .WithMany(s => s.AttendanceRecognitions)
            .HasForeignKey(x => x.StudentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AttendanceSessionId)
            .HasDatabaseName("IX_AttendanceRecognition_AttendanceSessionId");

        builder.HasIndex(x => new { x.AttendanceSessionId, x.ImageSequence, x.FaceNumber })
            .IsUnique()
            .HasDatabaseName("IX_AttendanceRecognition_Session_ImageSequence_FaceNumber");

        builder.HasIndex(x => new { x.AttendanceSessionId, x.ImageSequence })
            .HasDatabaseName("IX_AttendanceRecognition_Session_ImageSequence");

        builder.HasIndex(x => new { x.TenantId, x.AttendanceSessionId })
            .HasDatabaseName("IX_AttendanceRecognition_Tenant_Session");

        builder.HasIndex(x => new { x.TenantId, x.StudentId })
            .HasDatabaseName("IX_AttendanceRecognition_Tenant_Student");

        builder.HasIndex(x => new { x.TenantId, x.RecognitionStatus })
            .HasDatabaseName("IX_AttendanceRecognition_Tenant_Status");

        builder.HasIndex(x => new { x.TenantId, x.AttendanceSessionId, x.RecognitionStatus })
            .HasDatabaseName("IX_AttendanceRecognition_Tenant_Session_Status");
    }
}
