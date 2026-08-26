using Abhyanvaya.Application.Internal;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.5 Prompt 5A.1A — Strict membership constraint identity mapping.</summary>
public sealed class AiSchedTg5Prompt5A1AStrictConstraintIdentityTests
{
    [Fact]
    public void Exact_approved_constraint_with_23505_maps()
    {
        Assert.True(TeachingGroupMembershipPersistenceExceptionMapper.MatchesApprovedMembershipUniqueViolation(
            "23505",
            TeachingGroupMembershipPersistenceExceptionMapper.ApprovedPostgresConstraintName));
    }

    [Fact]
    public void Same_table_unrelated_constraint_with_23505_does_not_map()
    {
        Assert.False(TeachingGroupMembershipPersistenceExceptionMapper.MatchesApprovedMembershipUniqueViolation(
            "23505",
            "IX_SchedulingTeachingGroupMembership_UnrelatedFutureUnique"));
    }

    [Fact]
    public void SqlState_alone_does_not_map()
    {
        Assert.False(TeachingGroupMembershipPersistenceExceptionMapper.MatchesApprovedMembershipUniqueViolation(
            "23505",
            null));
        Assert.False(TeachingGroupMembershipPersistenceExceptionMapper.MatchesApprovedMembershipUniqueViolation(
            "23505",
            ""));
    }

    [Fact]
    public void Ef_logical_name_alone_does_not_authorize_without_actual_postgres_identity()
    {
        // EF logical name is longer than NAMEDATALEN; it must not be treated as the live ConstraintName.
        Assert.NotEqual(
            TeachingGroupMembershipPersistenceExceptionMapper.EfLogicalIndexName,
            TeachingGroupMembershipPersistenceExceptionMapper.ApprovedPostgresConstraintName);
        Assert.False(TeachingGroupMembershipPersistenceExceptionMapper.MatchesApprovedMembershipUniqueViolation(
            "23505",
            TeachingGroupMembershipPersistenceExceptionMapper.EfLogicalIndexName));
    }

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
    public void Mapper_source_has_no_table_level_or_sqlstate_only_authorization()
    {
        var mapper = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "Abhyanvaya.Application",
            "Internal",
            "TeachingGroupMembershipPersistenceExceptionMapper.cs"));
        var normalized = mapper.Replace("\r\n", "\n");

        Assert.Contains("ApprovedPostgresConstraintName", normalized);
        Assert.Contains("MatchesApprovedMembershipUniqueViolation", normalized);
        Assert.DoesNotContain("string.Equals(tableName, MembershipTableName", normalized);
        Assert.DoesNotContain("TableName == \"SchedulingTeachingGroupMembership\"", normalized);
        Assert.DoesNotContain("StartsWith(\"IX_SchedulingTeachingGroupMembership_TenantId_TeachingGroupId\"", normalized);
        Assert.DoesNotContain("message.Contains(CurrentMembershipUniqueIndexName", normalized);
        Assert.DoesNotContain("message.Contains(EfLogicalIndexName", normalized);
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
