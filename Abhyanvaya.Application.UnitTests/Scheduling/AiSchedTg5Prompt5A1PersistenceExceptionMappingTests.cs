using Abhyanvaya.Application.Internal;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.5 Prompt 5A.1 — Narrow membership persistence exception mapping (retained).</summary>
public sealed class AiSchedTg5Prompt5A1PersistenceExceptionMappingTests
{
    [Fact]
    public void Non_postgres_DbUpdateException_is_not_mapped()
    {
        var ex = new DbUpdateException("generic failure", new InvalidOperationException("nope"));
        Assert.False(TeachingGroupMembershipPersistenceExceptionMapper
            .TryMapCurrentMembershipUniqueViolation(ex, out _));
    }

    [Fact]
    public void Rethrow_preserves_unrelated_DbUpdateException()
    {
        var original = new DbUpdateException("fk", new Exception("23503"));
        var thrown = Assert.Throws<DbUpdateException>(() =>
            TeachingGroupMembershipPersistenceExceptionMapper.RethrowUnlessCurrentMembershipUniqueViolation(original));
        Assert.Same(original, thrown);
    }

    [Fact]
    public void Mapper_constants_document_ef_logical_vs_approved_postgres_identity()
    {
        Assert.Equal(
            "IX_SchedulingTeachingGroupMembership_TenantId_TeachingGroupId_StudentId",
            TeachingGroupMembershipPersistenceExceptionMapper.EfLogicalIndexName);
        Assert.Equal(
            "IX_SchedulingTeachingGroupMembership_TenantId_TeachingGroupId_S",
            TeachingGroupMembershipPersistenceExceptionMapper.ApprovedPostgresConstraintName);
        Assert.Equal("23505", TeachingGroupMembershipPersistenceExceptionMapper.PostgresUniqueViolationSqlState);
        Assert.Equal(
            "A conflicting membership change was detected. Reload and try again.",
            TeachingGroupMembershipPersistenceExceptionMapper.ConflictMessage);
    }

    [Fact]
    public void Architecture_service_uses_narrow_mapper_not_blanket_catch()
    {
        var service = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "Abhyanvaya.Application",
            "Scheduling",
            "TeachingGroupMembershipApplicationService.cs"));
        Assert.Contains("TeachingGroupMembershipPersistenceExceptionMapper", service);
        Assert.Contains("RethrowUnlessCurrentMembershipUniqueViolation", service);
        Assert.DoesNotContain(
            "catch (DbUpdateException)\n        {\n            throw new ConcurrencyConflictException",
            service.Replace("\r\n", "\n"));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Infrastructure", "Abhyanvaya.Infrastructure.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
