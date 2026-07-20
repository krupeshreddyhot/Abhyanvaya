using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.EnrollmentApi;
using Abhyanvaya.Application.TenantContext;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.EnrollmentApi;
using Abhyanvaya.Infrastructure.Persistence.Repositories;
using Abhyanvaya.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.IntegrationTests.Enrollment;

[Collection(nameof(PostgreSqlCollection))]
public sealed class EnrollmentAuthorizationIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public EnrollmentAuthorizationIntegrationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task College_admin_can_access_own_tenant_batch_after_seed()
    {
        var batchId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService { TenantId = 1, UserId = 10, Role = nameof(UserRole.Admin) };
        await using var context = _fixture.CreateDbContext(currentUser);
        context.Set<StudentEnrollmentBatch>().Add(new StudentEnrollmentBatch
        {
            Id = batchId,
            TenantId = 1,
            CollegeId = 1,
            UniversityId = 1,
            AcademicYear = 2025,
            Status = BatchStatus.Created,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = 10,
            TotalStudents = 5,
            PhotoProviderName = "ExamBranch",
        });
        await context.SaveChangesAsync();

        var service = CreateAuthorizationService(context, currentUser, tenantId: 1);
        var result = await service.CanSubscribeBatchAsync(batchId);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task College_admin_cannot_access_other_tenant_batch()
    {
        var batchId = Guid.NewGuid();
        var ownerUser = new TestCurrentUserService { TenantId = 2, UserId = 20, Role = nameof(UserRole.Admin) };
        await using (var context = _fixture.CreateDbContext(ownerUser))
        {
            context.Set<StudentEnrollmentBatch>().Add(new StudentEnrollmentBatch
            {
                Id = batchId,
                TenantId = 2,
                CollegeId = 2,
                UniversityId = 1,
                AcademicYear = 2025,
                Status = BatchStatus.Created,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = 20,
                TotalStudents = 3,
                PhotoProviderName = "ExamBranch",
            });
            await context.SaveChangesAsync();
        }

        var otherUser = new TestCurrentUserService { TenantId = 1, UserId = 10, Role = nameof(UserRole.Admin) };
        await using var verifyContext = _fixture.CreateDbContext(otherUser);
        var service = CreateAuthorizationService(verifyContext, otherUser, tenantId: 1);
        var result = await service.CanSubscribeBatchAsync(batchId);

        result.Decision.Should().Be(EnrollmentAuthorizationDecision.Forbidden);
    }

    private static EnrollmentAuthorizationService CreateAuthorizationService(
        Abhyanvaya.Infrastructure.Persistence.ApplicationDbContext context,
        TestCurrentUserService currentUser,
        int tenantId)
    {
        var batchRepository = new StudentEnrollmentBatchRepository(context);
        var tenantContext = new Mock<ITenantContextService>();
        tenantContext.Setup(t => t.ResolveForOperation())
            .Returns(TenantContextResolution.FromContext(new TenantContextSnapshot
            {
                UserId = currentUser.UserId,
                Role = currentUser.Role,
                TenantId = tenantId,
                SelectedCollegeId = tenantId,
                ContextType = ContextType.College,
                CreatedUtc = DateTime.UtcNow,
                IsGlobal = false,
                ContextSource = "IntegrationTest",
            }));

        var permissions = new Mock<IEnrollmentActorPermissions>();
        permissions.SetupGet(p => p.CanViewEnrollment).Returns(true);
        permissions.SetupGet(p => p.CanManageEnrollment).Returns(true);

        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.RecordAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditAction>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new EnrollmentAuthorizationService(
            tenantContext.Object,
            currentUser,
            batchRepository,
            permissions.Object,
            new EnrollmentAuthorizationTelemetry(NullLogger<EnrollmentAuthorizationTelemetry>.Instance),
            audit.Object,
            NullLogger<EnrollmentAuthorizationService>.Instance);
    }
}
