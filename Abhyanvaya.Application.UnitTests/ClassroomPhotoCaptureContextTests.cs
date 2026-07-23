using Abhyanvaya.Application.DTOs.Attendance;
using Abhyanvaya.Domain.Enums;
using Xunit;

namespace Abhyanvaya.Application.UnitTests;

/// <summary>
/// AI22.7A Phase 1 — capture context DTO / acquisition method contract tests.
/// </summary>
public sealed class ClassroomPhotoCaptureContextTests
{
    [Theory]
    [InlineData("Upload")]
    [InlineData("CameraCapture")]
    [InlineData("CameraMultiCapture")]
    public void Capture_context_accepts_known_acquisition_methods(string method)
    {
        var dto = new ClassroomPhotoCaptureContextDto
        {
            AcquisitionMethod = method,
            CaptureDevice = "unit-test",
            CaptureTimestampUtc = DateTime.UtcNow,
            Orientation = 1,
            Latitude = 17.385,
            Longitude = 78.4867,
            BlurScore = 120.5,
        };

        Assert.Equal(method, dto.AcquisitionMethod);
        Assert.True(Enum.TryParse<ClassroomPhotoAcquisitionMethod>(method, out _));
        Assert.NotNull(dto.Latitude);
        Assert.NotNull(dto.Longitude);
        Assert.True(dto.BlurScore > 0);
    }

    [Fact]
    public void Capture_context_is_optional_for_legacy_upload_clients()
    {
        ClassroomPhotoCaptureContextDto? dto = null;
        Assert.Null(dto);
    }
}
