using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Exceptions;
using Abhyanvaya.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.IntegrationTests.Attendance;

[Collection(nameof(PostgreSqlCollection))]
public sealed class AttendanceConcurrencyIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public AttendanceConcurrencyIntegrationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SaveChanges_with_stale_row_version_throws_concurrency_conflict()
    {
        var currentUser = new TestCurrentUserService();
        await using var context = _fixture.CreateDbContext(currentUser);
        var scenario = await new AttendanceTestDataFactory(context).CreateReviewReadyScenarioAsync();

        await using var context1 = _fixture.CreateDbContext(currentUser);
        await using var context2 = _fixture.CreateDbContext(currentUser);

        var recognition1 = await context1.AttendanceRecognitions
            .SingleAsync(r => r.Id == scenario.Recognition.Id);
        var recognition2 = await context2.AttendanceRecognitions
            .SingleAsync(r => r.Id == scenario.Recognition.Id);

        recognition1.ReviewNotes = "First edit";
        recognition2.ReviewNotes = "Second edit";

        await ConcurrencyExceptionHelper.SaveChangesAsync(context2);

        var act = async () => await ConcurrencyExceptionHelper.SaveChangesAsync(context1);
        await act.Should().ThrowAsync<ConcurrencyConflictException>();
    }
}
