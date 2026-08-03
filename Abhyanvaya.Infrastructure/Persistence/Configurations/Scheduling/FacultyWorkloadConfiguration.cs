using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class FacultyWorkloadConfiguration : IEntityTypeConfiguration<FacultyWorkload>
{
    public void Configure(EntityTypeBuilder<FacultyWorkload> builder)
    {
        builder.ToTable("SchedulingFacultyWorkload");
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.TeachingLoadHours).HasPrecision(6, 2);
        builder.Property(x => x.LabLoadHours).HasPrecision(6, 2);
        builder.Property(x => x.MentoringLoadHours).HasPrecision(6, 2);
        builder.Property(x => x.AdministrativeLoadHours).HasPrecision(6, 2);
        builder.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.StaffId }).IsUnique();
    }
}
