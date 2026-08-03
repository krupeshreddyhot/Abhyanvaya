using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class RoomAllocationRuleConfiguration : IEntityTypeConfiguration<RoomAllocationRule>
{
    public void Configure(EntityTypeBuilder<RoomAllocationRule> builder)
    {
        builder.ToTable("SchedulingRoomAllocationRule");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasOne(x => x.AcademicYear).WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreferredRoom).WithMany().HasForeignKey(x => x.PreferredRoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.Name });
    }
}
