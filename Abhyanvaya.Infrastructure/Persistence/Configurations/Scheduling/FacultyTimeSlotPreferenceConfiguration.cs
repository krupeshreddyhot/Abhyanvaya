using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class FacultyTimeSlotPreferenceConfiguration : IEntityTypeConfiguration<FacultyTimeSlotPreference>
{
    public void Configure(EntityTypeBuilder<FacultyTimeSlotPreference> builder)
    {
        builder.ToTable("SchedulingFacultyTimeSlotPreference");
        builder.HasOne(x => x.FacultyWorkload).WithMany(y => y.TimeSlotPreferences).HasForeignKey(x => x.FacultyWorkloadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TimeSlot).WithMany().HasForeignKey(x => x.TimeSlotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.FacultyWorkloadId, x.TimeSlotId }).IsUnique();
    }
}
