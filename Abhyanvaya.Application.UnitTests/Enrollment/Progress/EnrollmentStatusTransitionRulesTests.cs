using Abhyanvaya.Application.Enrollment.Progress;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Progress;

public sealed class EnrollmentStatusTransitionRulesTests
{
    [Theory]
    [InlineData(EnrollmentStatus.Pending, EnrollmentStatus.Downloading, true)]
    [InlineData(EnrollmentStatus.Downloading, EnrollmentStatus.Downloaded, true)]
    [InlineData(EnrollmentStatus.Downloaded, EnrollmentStatus.Validating, true)]
    [InlineData(EnrollmentStatus.Validating, EnrollmentStatus.Embedding, true)]
    [InlineData(EnrollmentStatus.Embedding, EnrollmentStatus.Completed, true)]
    [InlineData(EnrollmentStatus.Completed, EnrollmentStatus.Downloading, false)]
    [InlineData(EnrollmentStatus.Downloading, EnrollmentStatus.Embedding, false)]
    public void IsAllowed_ReturnsExpected(EnrollmentStatus from, EnrollmentStatus to, bool expected) =>
        Assert.Equal(expected, EnrollmentStatusTransitionRules.IsAllowed(from, to));

    [Fact]
    public void EnsureAllowed_ThrowsForIllegalTransition()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            EnrollmentStatusTransitionRules.EnsureAllowed(
                EnrollmentStatus.Completed,
                EnrollmentStatus.Downloading));

        Assert.Contains("Illegal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
