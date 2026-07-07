using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.AttendanceRecognition;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Abhyanvaya.IntegrationTests.Attendance;

[Collection(nameof(PostgreSqlCollection))]
public sealed class AttendanceFinalizationIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public AttendanceFinalizationIntegrationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FinalizeSession_creates_attendance_detail_snapshot_and_approves_session()
    {
        var currentUser = new TestCurrentUserService();
        await using var context = _fixture.CreateDbContext(currentUser);
        var scenario = await new AttendanceTestDataFactory(context).CreateReviewReadyScenarioAsync();

        await using var provider = _fixture.CreateServiceProvider(currentUser);
        var reviewService = provider.GetRequiredService<IAttendanceRecognitionReviewService>();
        var finalizer = provider.GetRequiredService<IAttendanceSessionFinalizer>();

        await reviewService.ReviewRecognitionAsync(
            new AttendanceRecognitionReviewRequest
            {
                RecognitionId = scenario.Recognition.Id,
                Action = RecognitionReviewAction.Approve
            });

        var summary = await finalizer.FinalizeAttendanceSessionAsync(scenario.Session.Id);
        summary.Present.Should().BeGreaterThan(0);

        await using var verifyContext = _fixture.CreateDbContext(currentUser);
        var session = await verifyContext.AttendanceSessions
            .AsNoTracking()
            .SingleAsync(s => s.Id == scenario.Session.Id);
        session.Status.Should().Be(AttendanceSessionStatus.Approved);

        var attendances = await verifyContext.Attendances
            .AsNoTracking()
            .Where(a => a.AttendanceSessionId == scenario.Session.Id)
            .ToListAsync();
        attendances.Should().NotBeEmpty();

        var details = await verifyContext.AttendanceDetails
            .AsNoTracking()
            .Where(d => attendances.Select(a => a.Id).Contains(d.AttendanceId))
            .ToListAsync();
        details.Should().NotBeEmpty();
        details.Should().OnlyContain(d => !string.IsNullOrWhiteSpace(d.RecognitionSnapshotJson));
    }

    [Fact]
    public async Task FinalizeSession_is_idempotent_when_already_approved()
    {
        var currentUser = new TestCurrentUserService();
        await using var context = _fixture.CreateDbContext(currentUser);
        var scenario = await new AttendanceTestDataFactory(context).CreateReviewReadyScenarioAsync();

        await using var provider = _fixture.CreateServiceProvider(currentUser);
        var reviewService = provider.GetRequiredService<IAttendanceRecognitionReviewService>();
        var finalizer = provider.GetRequiredService<IAttendanceSessionFinalizer>();

        await reviewService.ReviewRecognitionAsync(
            new AttendanceRecognitionReviewRequest
            {
                RecognitionId = scenario.Recognition.Id,
                Action = RecognitionReviewAction.Approve
            });

        var firstSummary = await finalizer.FinalizeAttendanceSessionAsync(scenario.Session.Id);
        firstSummary.AlreadyFinalized.Should().BeFalse();

        var attendanceCount = await context.Attendances
            .AsNoTracking()
            .CountAsync(a => a.AttendanceSessionId == scenario.Session.Id);

        var secondSummary = await finalizer.FinalizeAttendanceSessionAsync(scenario.Session.Id);
        secondSummary.AlreadyFinalized.Should().BeTrue();
        secondSummary.Present.Should().Be(firstSummary.Present);
        secondSummary.Absent.Should().Be(firstSummary.Absent);

        await using var verifyContext = _fixture.CreateDbContext(currentUser);
        var attendanceCountAfter = await verifyContext.Attendances
            .AsNoTracking()
            .CountAsync(a => a.AttendanceSessionId == scenario.Session.Id);
        attendanceCountAfter.Should().Be(attendanceCount);
    }
}
