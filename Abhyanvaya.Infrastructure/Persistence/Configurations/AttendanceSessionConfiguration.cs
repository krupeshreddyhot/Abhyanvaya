using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class AttendanceSessionConfiguration : IEntityTypeConfiguration<AttendanceSession>
{
    public void Configure(EntityTypeBuilder<AttendanceSession> builder)
    {
        builder.ToTable("AttendanceSession");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.AttendanceDate)
            .HasColumnType("date");

        builder.Property(x => x.AttendanceMethod)
            .HasConversion<int>()
            .HasDefaultValue(AttendanceMethod.Manual)
            .HasSentinel((AttendanceMethod)0);

        builder.Property(x => x.AttendanceSource)
            .HasConversion<int>()
            .HasDefaultValue(AttendanceSource.Web)
            .HasSentinel((AttendanceSource)0);

        builder.Property(x => x.ThumbnailImageKey)
            .HasMaxLength(500)
            .HasColumnType("character varying(500)");

        builder.Property(x => x.AnnotatedImageKey)
            .HasMaxLength(500)
            .HasColumnType("character varying(500)");

        builder.Property(x => x.OriginalFileName)
            .HasMaxLength(255)
            .HasColumnType("character varying(255)");

        builder.OwnsOne(x => x.ImageMetadata, metadata =>
        {
            metadata.Property(m => m.ImageKey)
                .HasColumnName("OriginalImageKey")
                .HasMaxLength(500)
                .HasColumnType("character varying(500)");

            metadata.Property(m => m.ImageHash)
                .HasColumnName("OriginalImageHash")
                .HasMaxLength(128)
                .HasColumnType("character varying(128)");

            metadata.Property(m => m.Width)
                .HasColumnName("ImageWidth");

            metadata.Property(m => m.Height)
                .HasColumnName("ImageHeight");

            metadata.Property(m => m.Orientation)
                .HasColumnName("ImageOrientation")
                .HasColumnType("smallint");

            metadata.Property(m => m.CaptureTimestamp)
                .HasColumnName("CaptureTimestamp")
                .HasColumnType("timestamp with time zone");

            metadata.Property(m => m.CaptureDevice)
                .HasColumnName("CaptureDevice")
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            metadata.Property(m => m.UploadedUtc)
                .HasColumnName("ImageUploadedUtc")
                .HasColumnType("timestamp with time zone");

            metadata.Property(m => m.FileSize)
                .HasColumnName("ImageFileSize");

            metadata.Property(m => m.AcquisitionMethod)
                .HasColumnName("ImageAcquisitionMethod")
                .HasMaxLength(32)
                .HasColumnType("character varying(32)");

            metadata.Property(m => m.CaptureLatitude)
                .HasColumnName("ImageCaptureLatitude");

            metadata.Property(m => m.CaptureLongitude)
                .HasColumnName("ImageCaptureLongitude");

            metadata.Property(m => m.BlurScore)
                .HasColumnName("ImageBlurScore");
        });

        builder.Property(x => x.SessionNumber)
            .HasColumnType("smallint")
            .HasDefaultValue((short)1);

        builder.Property(x => x.SessionName)
            .HasMaxLength(100)
            .HasColumnType("character varying(100)");

        builder.Property(x => x.RecognizedCount)
            .HasDefaultValue(0);

        builder.Property(x => x.UnknownCount)
            .HasDefaultValue(0);

        builder.Property(x => x.RejectedCount)
            .HasDefaultValue(0);

        builder.Property(x => x.IgnoredCount)
            .HasDefaultValue(0);

        builder.Property(x => x.DuplicateCount)
            .HasDefaultValue(0);

        builder.Property(x => x.ManualAssignmentCount)
            .HasDefaultValue(0);

        builder.Property(x => x.LowConfidenceCount)
            .HasDefaultValue(0);

        builder.Property(x => x.AverageConfidence)
            .HasPrecision(5, 2);

        builder.Property(x => x.RecognitionCompletionPercent)
            .HasPrecision(5, 2);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(AttendanceSessionStatus.Draft);

        // AI22.8 — additive workflow recovery fields (do not replace Status)
        builder.Property(x => x.WorkflowStatus)
            .HasConversion<int>()
            .HasDefaultValue(AttendanceWorkflowStatus.Created);
        builder.Property(x => x.LastActivityUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(x => x.ResumeCheckpointJson)
            .HasColumnType("text");
        builder.Property(x => x.WorkflowExpiredUtc)
            .HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.TenantId, x.WorkflowStatus, x.LastActivityUtc });
        builder.HasIndex(x => new { x.TenantId, x.StaffId, x.WorkflowExpiredUtc });

        builder.Property(x => x.RecognitionProvider)
            .HasMaxLength(100)
            .HasColumnType("character varying(100)");

        builder.Property(x => x.RecognitionModel)
            .HasMaxLength(100)
            .HasColumnType("character varying(100)");

        builder.Property(x => x.RecognitionPipelineVersion)
            .HasMaxLength(50)
            .HasColumnType("character varying(50)");

        builder.Property(x => x.ProcessingError)
            .HasMaxLength(1000)
            .HasColumnType("character varying(1000)");

        builder.Property(x => x.StartedUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CompletedUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CreatedUtc)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.ApprovedUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.RetryCount)
            .HasDefaultValue(0);

        builder.Property(x => x.RowVersion)
            .IsConcurrencyToken()
            .HasColumnType("bytea")
            .IsRequired();

        builder.HasOne(x => x.Course)
            .WithMany()
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Group)
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Semester)
            .WithMany()
            .HasForeignKey(x => x.SemesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Subject)
            .WithMany()
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Staff)
            .WithMany()
            .HasForeignKey(x => x.StaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ClassSchedule)
            .WithMany(s => s.AttendanceSessions)
            .HasForeignKey(x => x.ClassScheduleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ClassScheduleId)
            .HasDatabaseName("IX_AttendanceSession_ClassScheduleId");

        builder.HasIndex(x => x.StaffId)
            .HasDatabaseName("IX_AttendanceSession_StaffId");

        builder.HasIndex(x => new { x.TenantId, x.StaffId, x.AttendanceDate })
            .HasDatabaseName("IX_AttendanceSession_Tenant_Staff_Date");

        builder.HasIndex(x => new { x.TenantId, x.SubjectId, x.AttendanceDate })
            .HasDatabaseName("IX_AttendanceSession_Tenant_Subject_Date");

        builder.HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("IX_AttendanceSession_Tenant_Status");

        builder.HasIndex(x => new { x.TenantId, x.AttendanceDate, x.Status })
            .HasDatabaseName("IX_AttendanceSession_Tenant_Date_Status");

        builder.HasIndex(x => new { x.TenantId, x.AttendanceMethod })
            .HasDatabaseName("IX_AttendanceSession_Tenant_Method");

        builder.HasIndex(x => new { x.TenantId, x.AttendanceDate, x.PeriodNumber })
            .HasDatabaseName("IX_AttendanceSession_Tenant_Date_Period");

        builder.HasIndex(x => new { x.TenantId, x.SubjectId, x.AttendanceDate, x.PeriodNumber })
            .HasDatabaseName("IX_AttendanceSession_Tenant_Subject_Date_Period");

        builder.HasIndex(x => new
            {
                x.TenantId,
                x.CourseId,
                x.GroupId,
                x.SemesterId,
                x.SubjectId,
                x.AttendanceDate,
                x.PeriodNumber,
                x.SessionNumber
            })
            .HasDatabaseName("IX_AttendanceSession_Tenant_Context_SessionNumber");
    }
}
