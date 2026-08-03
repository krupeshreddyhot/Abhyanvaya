using Abhyanvaya.Domain.Entities.Scheduling;

using Microsoft.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore.Metadata.Builders;



namespace Abhyanvaya.Infrastructure.Persistence.Configurations.Scheduling;



public sealed class TimeSlotTemplateConfiguration : IEntityTypeConfiguration<TimeSlotTemplate>

{

    public void Configure(EntityTypeBuilder<TimeSlotTemplate> builder)

    {

        builder.ToTable("SchedulingTimeSlotTemplate");

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();

        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.Name });

    }

}

