using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.FacultyPreferences.Validators;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase1B;

public sealed class FacultyTeachingPreferenceValidationTests
{
    [Fact]
    public void CreateValidator_RejectsZeroMaximumContinuousClasses()
    {
        var validator = new CreateFacultyTeachingPreferenceRequestValidator();
        var result = validator.Validate(new CreateFacultyTeachingPreferenceRequest
        {
            StaffId = 1,
            AcademicYearId = 1,
            MaximumContinuousClasses = 0,
            MinimumBreakBetweenClasses = 0,
            PreferredTeachingMode = PreferredTeachingMode.Any,
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateValidator_RejectsFirstPeriodGreaterThanLast()
    {
        var validator = new CreateFacultyTeachingPreferenceRequestValidator();
        var result = validator.Validate(new CreateFacultyTeachingPreferenceRequest
        {
            StaffId = 1,
            AcademicYearId = 1,
            PreferredFirstPeriod = 5,
            PreferredLastPeriod = 2,
            MaximumContinuousClasses = 2,
            MinimumBreakBetweenClasses = 1,
            PreferredTeachingMode = PreferredTeachingMode.Morning,
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateValidator_AcceptsValidPeriodRange()
    {
        var validator = new CreateFacultyTeachingPreferenceRequestValidator();
        var result = validator.Validate(new CreateFacultyTeachingPreferenceRequest
        {
            StaffId = 1,
            AcademicYearId = 1,
            PreferredFirstPeriod = 1,
            PreferredLastPeriod = 4,
            MaximumContinuousClasses = 3,
            MinimumBreakBetweenClasses = 1,
            PreferredTeachingMode = PreferredTeachingMode.Afternoon,
        });
        Assert.True(result.IsValid);
    }
}
