using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.AttendanceRecognition;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Abhyanvaya.IntegrationTests.Attendance;

[Collection(nameof(PostgreSqlCollection))]
public sealed class AttendanceReviewIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public AttendanceReviewIntegrationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ReviewRecognition_persists_history_and_marks_verified()
    {
        var currentUser = new TestCurrentUserService();
        await using var context = _fixture.CreateDbContext(currentUser);
        var scenario = await new AttendanceTestDataFactory(context).CreateReviewReadyScenarioAsync();

        await using var provider = _fixture.CreateServiceProvider(currentUser);
        var reviewService = provider.GetRequiredService<IAttendanceRecognitionReviewService>();

        var result = await reviewService.ReviewRecognitionAsync(
            new AttendanceRecognitionReviewRequest
            {
                RecognitionId = scenario.Recognition.Id,
                Action = RecognitionReviewAction.Approve
            });

        result.VerifiedByTeacher.Should().BeTrue();

        await using var verifyContext = _fixture.CreateDbContext(currentUser);
        var recognition = await verifyContext.AttendanceRecognitions
            .AsNoTracking()
            .SingleAsync(r => r.Id == scenario.Recognition.Id);
        recognition.VerifiedByTeacher.Should().BeTrue();

        var history = await verifyContext.AttendanceRecognitionReviewHistories
            .AsNoTracking()
            .Where(h => h.RecognitionId == scenario.Recognition.Id)
            .ToListAsync();
        history.Should().NotBeEmpty();
    }
}
