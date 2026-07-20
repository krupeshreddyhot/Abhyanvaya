using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.EnrollmentApi;
using Abhyanvaya.Application.TenantContext;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.EnrollmentApi;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.EnrollmentApi;

public sealed class EnrollmentAuthorizationServiceTests
{
    private readonly Mock<ITenantContextService> _tenantContext = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IStudentEnrollmentBatchRepository> _batchRepository = new();
    private readonly Mock<IEnrollmentActorPermissions> _permissions = new();
    private readonly Mock<IEnrollmentAuthorizationTelemetry> _telemetry = new();
    private readonly Mock<IAuditService> _auditService = new();
    private readonly EnrollmentAuthorizationService _service;

    public EnrollmentAuthorizationServiceTests()
    {
        _permissions.SetupGet(p => p.CanViewEnrollment).Returns(true);
        _permissions.SetupGet(p => p.CanManageEnrollment).Returns(true);
        _auditService.Setup(a => a.RecordAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AuditAction>(),
                It.IsAny<object>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new EnrollmentAuthorizationService(
            _tenantContext.Object,
            _currentUser.Object,
            _batchRepository.Object,
            _permissions.Object,
            _telemetry.Object,
            _auditService.Object,
            NullLogger<EnrollmentAuthorizationService>.Instance);
    }

    [Fact]
    public async Task CanSubscribeBatch_Allows_valid_tenant_batch()
    {
        var batchId = Guid.NewGuid();
        SetupTenant(1, 100);
        SetupBatch(batchId, 1);

        var result = await _service.CanSubscribeBatchAsync(batchId);

        Assert.True(result.IsAllowed);
        Assert.Equal(1, result.TenantId);
    }

    [Fact]
    public async Task CanSubscribeBatch_Denies_cross_tenant_batch()
    {
        var batchId = Guid.NewGuid();
        SetupTenant(1, 100);
        _batchRepository.Setup(r => r.GetBatchAsync(batchId, 1, It.IsAny<CancellationToken>())).ReturnsAsync((StudentEnrollmentBatch?)null);

        var result = await _service.CanSubscribeBatchAsync(batchId);

        Assert.Equal(EnrollmentAuthorizationDecision.Forbidden, result.Decision);
        _telemetry.Verify(t => t.RecordSubscriptionFailure(batchId, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CanSubscribeBatch_Allows_super_admin_via_batch_tenant_when_context_missing()
    {
        var batchId = Guid.NewGuid();
        _currentUser.SetupGet(u => u.Role).Returns(nameof(UserRole.SuperAdmin));
        _tenantContext.Setup(t => t.ResolveForOperation())
            .Returns(TenantContextResolution.ContextRequired());
        _batchRepository.Setup(r => r.GetBatchAsync(batchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StudentEnrollmentBatch
            {
                Id = batchId,
                TenantId = 1,
                CollegeId = 10,
                UniversityId = 1,
                AcademicYear = 2025,
                Status = BatchStatus.Running,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = 100,
                TotalStudents = 1,
                PhotoProviderName = "ExamBranch",
            });
        SetupBatch(batchId, 1);

        var result = await _service.CanSubscribeBatchAsync(batchId);

        Assert.True(result.IsAllowed);
        Assert.Equal(1, result.TenantId);
    }

    [Fact]
    public async Task CanSubscribeBatch_Requires_operational_context_for_non_super_admin()
    {
        var batchId = Guid.NewGuid();
        _currentUser.SetupGet(u => u.Role).Returns(nameof(UserRole.Admin));
        _tenantContext.Setup(t => t.ResolveForOperation())
            .Returns(TenantContextResolution.ContextRequired());

        var result = await _service.CanSubscribeBatchAsync(batchId);

        Assert.Equal(EnrollmentAuthorizationDecision.ContextRequired, result.Decision);
    }

    [Fact]
    public async Task CanCancelBatch_Denies_without_manage_permission()
    {
        var batchId = Guid.NewGuid();
        SetupTenant(1, 100);
        SetupBatch(batchId, 1);
        _permissions.SetupGet(p => p.CanManageEnrollment).Returns(false);

        var result = await _service.CanCancelBatchAsync(batchId);

        Assert.Equal(EnrollmentAuthorizationDecision.Forbidden, result.Decision);
    }

    [Fact]
    public async Task ValidateTenantAccess_Allows_college_admin_own_tenant()
    {
        SetupTenant(2, 200);

        var result = await _service.ValidateTenantAccessAsync();

        Assert.True(result.IsAllowed);
        Assert.Equal(2, result.TenantId);
    }

    private void SetupTenant(int tenantId, int userId)
    {
        _currentUser.SetupGet(u => u.Role).Returns(nameof(UserRole.Admin));
        _tenantContext.Setup(t => t.ResolveForOperation())
            .Returns(TenantContextResolution.FromContext(new TenantContextSnapshot
            {
                UserId = userId,
                Role = nameof(UserRole.Admin),
                TenantId = tenantId,
                SelectedCollegeId = 10,
                ContextType = ContextType.College,
                CreatedUtc = DateTime.UtcNow,
                IsGlobal = false,
                ContextSource = "Test",
            }));
    }

    private void SetupBatch(Guid batchId, int tenantId)
    {
        _batchRepository.Setup(r => r.GetBatchAsync(batchId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StudentEnrollmentBatch
            {
                Id = batchId,
                TenantId = tenantId,
                CollegeId = 10,
                UniversityId = 1,
                AcademicYear = 2025,
                Status = BatchStatus.Running,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = 100,
                TotalStudents = 1,
                PhotoProviderName = "ExamBranch",
            });
    }
}
