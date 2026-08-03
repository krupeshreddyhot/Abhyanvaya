using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;

public sealed class TimeSlotConfiguration : IEntityTypeConfiguration<TimeSlot>
{
    public void Configure(EntityTypeBuilder<TimeSlot> builder)
    {
        builder.ToTable("SchedulingTimeSlot");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.TimeSlotSet).WithMany(y => y.TimeSlots).HasForeignKey(x => x.TimeSlotSetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.TimeSlotSetId, x.DayOfWeek, x.PeriodNumber });
    }
}
