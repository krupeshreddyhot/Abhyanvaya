using Abhyanvaya.Application.DTOs.Attendance;
using Abhyanvaya.Domain.Enums;
using Xunit;

namespace Abhyanvaya.Application.UnitTests;

/// <summary>
/// AI22.7A Phase 2 — multi-image classroom photo collection contracts.
/// </summary>
public sealed class ClassroomPhotoCollectionTests
{
    [Fact]
    public void Max_images_per_session_is_ten()
    {
        Assert.Equal(10, ClassroomPhotoCollectionLimits.MaxImagesPerSession);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public void Supported_collection_sizes_are_within_limit(int count)
    {
        Assert.InRange(count, 1, ClassroomPhotoCollectionLimits.MaxImagesPerSession);
    }

    [Fact]
    public void Session_image_status_values_are_stable_for_api_clients()
    {
        Assert.Equal(1, (short)AttendanceSessionImageStatus.Uploaded);
        Assert.Equal(2, (short)AttendanceSessionImageStatus.Processing);
        Assert.Equal(3, (short)AttendanceSessionImageStatus.Processed);
        Assert.Equal(4, (short)AttendanceSessionImageStatus.Failed);
    }

    [Fact]
    public void Collection_upload_result_carries_image_and_count()
    {
        var imageId = Guid.NewGuid();
        var result = new ClassroomPhotoCollectionUploadResult
        {
            AttendanceSessionId = Guid.NewGuid(),
            Queued = true,
            ImageCount = 3,
            Image = new AttendanceSessionImageDto
            {
                Id = imageId,
                ImageSequence = 3,
                ImageStorageKey = "attendance/1/sessions/x/classroom_03.jpg",
                Status = AttendanceSessionImageStatus.Uploaded,
            },
        };

        Assert.True(result.Queued);
        Assert.Equal(3, result.ImageCount);
        Assert.Equal(imageId, result.Image.Id);
        Assert.Equal((short)3, result.Image.ImageSequence);
    }

    [Fact]
    public void Reorder_request_requires_ordered_image_ids()
    {
        var request = new ReorderSessionImagesRequestDto
        {
            ImageIds = [Guid.NewGuid(), Guid.NewGuid()],
        };

        Assert.Equal(2, request.ImageIds.Count);
        Assert.Equal(2, request.ImageIds.Distinct().Count());
    }
}
