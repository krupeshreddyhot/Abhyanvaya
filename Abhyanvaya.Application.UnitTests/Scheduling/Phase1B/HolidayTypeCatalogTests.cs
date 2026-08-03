using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.HolidayTypes.Validators;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase1B;

public sealed class HolidayTypeCatalogTests
{
    [Fact]
    public void CreateValidator_RejectsEmptyCode()
    {
        var validator = new CreateHolidayTypeCatalogRequestValidator();
        var result = validator.Validate(new CreateHolidayTypeCatalogRequest
        {
            Code = "",
            Name = "National Holiday",
            Colour = "#FF0000",
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateValidator_AcceptsValidRequest()
    {
        var validator = new CreateHolidayTypeCatalogRequestValidator();
        var result = validator.Validate(new CreateHolidayTypeCatalogRequest
        {
            Code = "NationalHoliday",
            Name = "National Holiday",
            Colour = "#FF0000",
            Priority = 1,
            SortOrder = 1,
        });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateValidator_RequiresPositiveId()
    {
        var validator = new UpdateHolidayTypeCatalogRequestValidator();
        var result = validator.Validate(new UpdateHolidayTypeCatalogRequest
        {
            Id = 0,
            Code = "Festival",
            Name = "Festival",
            Colour = "#FF6600",
        });
        Assert.False(result.IsValid);
    }
}
