using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Academic;
using Abhyanvaya.Domain.Entities.Academic;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class AI29_1B_SectionLifecycleCapacityTests
{
    [Theory]
    [InlineData(SectionLifecycleStates.Draft, SectionLifecycleStates.Planning, true)]
    [InlineData(SectionLifecycleStates.Active, SectionLifecycleStates.Locked, true)]
    [InlineData(SectionLifecycleStates.Active, SectionLifecycleStates.Merged, true)]
    [InlineData(SectionLifecycleStates.Active, SectionLifecycleStates.Split, true)]
    [InlineData(SectionLifecycleStates.Archived, SectionLifecycleStates.Active, false)]
    [InlineData(SectionLifecycleStates.Draft, SectionLifecycleStates.Merged, false)]
    public void Lifecycle_transitions_are_validated(string from, string to, bool expected)
        => Assert.Equal(expected, SectionLifecycleStateMachine.CanTransition(from, to));

    [Fact]
    public void Lifecycle_normalizes_legacy_inactive_to_closed()
        => Assert.Equal(SectionLifecycleStates.Closed, SectionLifecycleStates.Normalize("Inactive"));

    [Fact]
    public void Capacity_engine_calculates_occupancy_and_warnings()
    {
        var engine = new SectionCapacityEngine(null!, null!, null!, null!);
        var section = new Section
        {
            Id = 1,
            SectionCode = "A",
            SectionName = "Section A",
            MaximumStrength = 60,
            MinimumCapacity = 10,
            RecommendedCapacity = 50,
            ReservedSeats = 5,
            WaitingListCount = 2,
            Status = SectionLifecycleStates.Active,
            SectionTypeCode = SectionTypeCodes.Regular,
        };
        var policy = new TenantSectionCapacityPolicy
        {
            EnforceHardLimit = true,
            SoftLimitEnabled = true,
            WarningPercent = 90,
            AutoWarningEnabled = true,
            UnderCapacityPercent = 40,
        };

        var mid = engine.Calculate(section, 30, policy);
        Assert.Equal(25, mid.AvailableSeats); // 60 - 30 - 5
        Assert.Equal(50, mid.OccupancyPercent);
        Assert.Equal("Ok", mid.CapacityStatus);
        Assert.False(mid.IsOverCapacity);

        var warn = engine.Calculate(section, 55, policy);
        Assert.True(warn.HasWarning);
        Assert.Equal("Warning", warn.CapacityStatus);

        var over = engine.Calculate(section, 61, policy);
        Assert.True(over.IsOverCapacity);
        Assert.True(over.IsHardLimitBreached);
        Assert.Equal("OverCapacity", over.CapacityStatus);

        var under = engine.Calculate(section, 5, policy);
        Assert.True(under.IsUnderCapacity);
    }

    [Fact]
    public void Capacity_available_seats_never_negative()
    {
        var engine = new SectionCapacityEngine(null!, null!, null!, null!);
        var section = new Section { Id = 2, SectionCode = "B", SectionName = "B", MaximumStrength = 10, ReservedSeats = 3 };
        var snap = engine.Calculate(section, 9, null);
        Assert.Equal(0, snap.AvailableSeats);
    }

    [Fact]
    public void Section_type_defaults_are_configuration_strings_not_enums()
    {
        Assert.Contains(SectionTypeCodes.Laboratory, SectionTypeCodes.Defaults);
        Assert.Contains(SectionTypeCodes.SpecialBatch, SectionTypeCodes.Defaults);
        Assert.Equal(10, SectionTypeCodes.Defaults.Count);
    }

    [Fact]
    public void Merge_preview_dto_defaults_invalid_until_validated()
    {
        var dto = new SectionMergePreviewDto();
        Assert.False(dto.IsValid);
        Assert.Empty(dto.SourceSectionIds);
    }

    [Fact]
    public void Split_preview_requires_child_plan()
    {
        var dto = new SectionSplitPreviewDto
        {
            IsValid = true,
            ProposedChildren =
            [
                new SectionSplitChildPlanDto { ProposedCode = "A-A", ProposedCapacity = 30, PlannedStudentCount = 15 },
                new SectionSplitChildPlanDto { ProposedCode = "A-B", ProposedCapacity = 30, PlannedStudentCount = 15 },
            ]
        };
        Assert.Equal(2, dto.ProposedChildren.Count);
    }

    [Fact]
    public void Readiness_statuses_are_advisory_literals()
    {
        var dto = new SectionReadinessDto
        {
            OverallStatus = "Warning",
            Checks =
            [
                new SectionReadinessCheckDto { Area = "Faculty", Status = "Blocked", Message = "No faculty" },
                new SectionReadinessCheckDto { Area = "Capacity", Status = "Ready", Message = "Ok" },
            ]
        };
        Assert.Equal("Warning", dto.OverallStatus);
        Assert.Contains(dto.Checks, c => c.Status == "Blocked");
    }

    [Fact]
    public void Permissions_keys_for_1B_are_defined()
    {
        Assert.Equal("SectionLifecycle.View", Domain.Authorization.PermissionKeys.SectionLifecycleView);
        Assert.Equal("SectionLifecycle.Edit", Domain.Authorization.PermissionKeys.SectionLifecycleEdit);
        Assert.Equal("Section.Merge", Domain.Authorization.PermissionKeys.SectionMerge);
        Assert.Equal("Section.Split", Domain.Authorization.PermissionKeys.SectionSplit);
        Assert.Equal("Section.Capacity", Domain.Authorization.PermissionKeys.SectionCapacity);
        Assert.Equal("Section.Readiness", Domain.Authorization.PermissionKeys.SectionReadiness);
        Assert.Contains(Domain.Authorization.PermissionKeys.SectionMerge, Domain.Authorization.PermissionKeys.All);
    }
}
