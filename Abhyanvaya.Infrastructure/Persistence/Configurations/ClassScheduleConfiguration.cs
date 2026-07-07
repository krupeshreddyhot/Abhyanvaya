using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class ClassScheduleConfiguration : IEntityTypeConfiguration<ClassSchedule>
{
    public void Configure(EntityTypeBuilder<ClassSchedule> builder)
    {
        builder.ToTable("ClassSchedule");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.ScheduleDate)
            .HasColumnType("date");

        builder.Property(x => x.CreatedUtc)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("timezone('utc', now())");

        builder.HasOne(x => x.Staff)
            .WithMany()
            .HasForeignKey(x => x.StaffId)
            .OnDelete(DeleteBehavior.Restrict);

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

        builder.HasIndex(x => new { x.TenantId, x.ScheduleDate, x.PeriodNumber })
            .HasDatabaseName("IX_ClassSchedule_Tenant_Date_Period");

        builder.HasIndex(x => new { x.TenantId, x.StaffId, x.ScheduleDate })
            .HasDatabaseName("IX_ClassSchedule_Tenant_Staff_Date");
    }
}
