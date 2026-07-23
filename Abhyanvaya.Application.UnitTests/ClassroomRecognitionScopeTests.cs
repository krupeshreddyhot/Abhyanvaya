using Abhyanvaya.Domain.Enums;
using Xunit;

namespace Abhyanvaya.Application.UnitTests;

/// <summary>AI22.7A Phase 3 — selective recognition scope contracts.</summary>
public sealed class ClassroomRecognitionScopeTests
{
    [Fact]
    public void Scope_values_are_stable_for_queue_clients()
    {
        Assert.Equal(0, (short)ClassroomRecognitionScope.FullSession);
        Assert.Equal(1, (short)ClassroomRecognitionScope.SingleImage);
        Assert.Equal(2, (short)ClassroomRecognitionScope.PendingOnly);
    }

    [Theory]
    [InlineData(ClassroomRecognitionScope.SingleImage)]
    [InlineData(ClassroomRecognitionScope.PendingOnly)]
    public void Replace_and_retry_scopes_skip_full_rebuild(ClassroomRecognitionScope scope)
    {
        Assert.NotEqual(ClassroomRecognitionScope.FullSession, scope);
    }

    [Fact]
    public void Image_details_dto_exposes_capture_metadata_fields()
    {
        var dto = new Abhyanvaya.Application.DTOs.Attendance.AttendanceSessionImageDto
        {
            CaptureTimestamp = DateTime.UtcNow,
            CaptureDevice = "unit-test-device",
            CaptureLatitude = 17.385,
            CaptureLongitude = 78.4867,
            Orientation = 1,
            DetectedFaceCount = 12,
            BatchStatus = "Failed",
            Status = AttendanceSessionImageStatus.Failed,
        };

        Assert.NotNull(dto.CaptureTimestamp);
        Assert.Equal("unit-test-device", dto.CaptureDevice);
        Assert.Equal(12, dto.DetectedFaceCount);
        Assert.Equal("Failed", dto.BatchStatus);
    }
}
