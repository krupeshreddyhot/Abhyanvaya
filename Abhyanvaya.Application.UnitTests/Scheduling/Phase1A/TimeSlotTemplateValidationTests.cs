using Abhyanvaya.Application.DTOs.Scheduling;

using Abhyanvaya.Application.Scheduling.Validators;

using Abhyanvaya.Domain.Enums.Scheduling;



namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase1A;



public sealed class TimeSlotTemplateValidationTests

{

    [Fact]

    public void CreateValidator_RejectsEmptyName()

    {

        var validator = new CreateTimeSlotTemplateRequestValidator();

        var result = validator.Validate(new CreateTimeSlotTemplateRequest

        {

            Name = "",

            TemplateType = TimeSlotTemplateType.Regular,

        });

        Assert.False(result.IsValid);

    }



    [Fact]

    public void UpdateValidator_AcceptsValidRequest()

    {

        var validator = new UpdateTimeSlotTemplateRequestValidator();

        var result = validator.Validate(new UpdateTimeSlotTemplateRequest

        {

            Id = 1,

            Name = "Regular Week",

            TemplateType = TimeSlotTemplateType.Regular,

        });

        Assert.True(result.IsValid);

    }

}

