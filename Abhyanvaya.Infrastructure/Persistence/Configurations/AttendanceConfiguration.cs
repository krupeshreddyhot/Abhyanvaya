using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.HasOne(a => a.Student)
            .WithMany()
            .HasForeignKey(a => a.StudentId);

        builder.HasOne(a => a.Subject)
            .WithMany()
            .HasForeignKey(a => a.SubjectId);

        builder.HasOne(a => a.AttendanceSession)
            .WithMany(s => s.Attendances)
            .HasForeignKey(a => a.AttendanceSessionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.TenantId, a.StudentId, a.SubjectId, a.Date })
            .IsUnique();

        builder.HasIndex(a => new { a.TenantId, a.SubjectId, a.Date })
            .HasDatabaseName("IX_Attendance_Tenant_Subject_Date");

        builder.HasIndex(a => a.AttendanceSessionId)
            .HasDatabaseName("IX_Attendance_AttendanceSessionId");

        builder.HasIndex(a => new { a.TenantId, a.AttendanceSessionId })
            .HasDatabaseName("IX_Attendance_Tenant_AttendanceSession");
    }
}
