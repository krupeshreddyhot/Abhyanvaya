using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Exceptions;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Abhyanvaya.IntegrationTests.Attendance;

[Collection(nameof(PostgreSqlCollection))]
public sealed class AttendanceTransactionIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public AttendanceTransactionIntegrationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FinalizeSession_with_unreviewed_recognition_rolls_back_and_leaves_session_unapproved()
    {
        var currentUser = new TestCurrentUserService();
        await using var context = _fixture.CreateDbContext(currentUser);
        var scenario = await new AttendanceTestDataFactory(context).CreateReviewReadyScenarioAsync();

        await using var provider = _fixture.CreateServiceProvider(currentUser);
        var finalizer = provider.GetRequiredService<IAttendanceSessionFinalizer>();

        var act = async () => await finalizer.FinalizeAttendanceSessionAsync(scenario.Session.Id);
        await act.Should().ThrowAsync<ValidationException>();

        await using var verifyContext = _fixture.CreateDbContext(currentUser);
        var session = await verifyContext.AttendanceSessions
            .AsNoTracking()
            .SingleAsync(s => s.Id == scenario.Session.Id);
        session.Status.Should().Be(AttendanceSessionStatus.AwaitingReview);

        var attendances = await verifyContext.Attendances
            .AsNoTracking()
            .Where(a => a.AttendanceSessionId == scenario.Session.Id)
            .ToListAsync();
        attendances.Should().BeEmpty();
    }
}
