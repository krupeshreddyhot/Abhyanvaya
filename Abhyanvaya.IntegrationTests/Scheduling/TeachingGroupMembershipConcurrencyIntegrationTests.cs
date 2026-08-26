using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Abhyanvaya.Infrastructure.Persistence;
using Abhyanvaya.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.IntegrationTests.Scheduling;

/// <summary>
/// AI-SCHED-TG.5 Prompt 5A.1 / 5A.1A — Genuine concurrent membership race + strict constraint mapping.
/// </summary>
[Collection(nameof(PostgreSqlCollection))]
public sealed class TeachingGroupMembershipConcurrencyIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public TeachingGroupMembershipConcurrencyIntegrationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Approved_current_membership_unique_index_identity_matches_postgres_catalog()
    {
        var currentUser = new TestCurrentUserService { TenantId = 1, UserId = 1 };
        await using var db = _fixture.CreateDbContext(currentUser);

        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        if (cmd.Connection!.State != System.Data.ConnectionState.Open)
            await cmd.Connection.OpenAsync();

        cmd.CommandText = """
            SELECT indexname, indexdef
            FROM pg_indexes
            WHERE tablename = 'SchedulingTeachingGroupMembership'
              AND indexdef ILIKE '%UNIQUE%'
              AND indexdef ILIKE '%IsCurrent%'
              AND indexdef ILIKE '%IsDeleted%'
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "expected filtered unique current-membership index");
        var indexName = reader.GetString(0);
        var def = reader.GetString(1);
        Assert.False(await reader.ReadAsync(), "expected exactly one filtered unique current-membership index");

        indexName.Should().Be(TeachingGroupMembershipPersistenceExceptionMapper.ApprovedPostgresConstraintName);
        def.Should().ContainEquivalentOf("unique");
        TeachingGroupMembershipPersistenceExceptionMapper.EfLogicalIndexName.Length.Should().BeGreaterThan(63);
        TeachingGroupMembershipPersistenceExceptionMapper.ApprovedPostgresConstraintName.Length.Should().BeLessOrEqualTo(63);
    }

    [Fact]
    public async Task Duplicate_insert_ConstraintName_equals_approved_postgres_identity()
    {
        var currentUser = new TestCurrentUserService { TenantId = 1, UserId = 1 };
        TeachingGroup tg;
        Domain.Entities.Student student;
        await using (var seedDb = _fixture.CreateDbContext(currentUser))
        {
            (tg, student) = await new TeachingGroupMembershipPgSeed(seedDb)
                .SeedExplicitTeachingGroupWithStudentAsync();
        }

        await using var db1 = _fixture.CreateDbContext(currentUser);
        await db1.AddAsync(NewCurrentMembership(tg.Id, student.Id));
        await db1.SaveChangesAsync();

        await using var db2 = _fixture.CreateDbContext(currentUser);
        await db2.AddAsync(NewCurrentMembership(tg.Id, student.Id));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());

        var inner = ex.InnerException!;
        var constraintName = inner.GetType().GetProperty("ConstraintName")?.GetValue(inner) as string;
        var sqlState = inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string;

        sqlState.Should().Be("23505");
        constraintName.Should().Be(TeachingGroupMembershipPersistenceExceptionMapper.ApprovedPostgresConstraintName);

        TeachingGroupMembershipPersistenceExceptionMapper
            .TryMapCurrentMembershipUniqueViolation(ex, out var conflict)
            .Should().BeTrue();
        conflict.Should().BeOfType<ConcurrencyConflictException>();
        conflict.Message.Should().Be(TeachingGroupMembershipPersistenceExceptionMapper.ConflictMessage);
    }

    [Fact]
    public async Task Genuine_concurrent_duplicate_current_membership_one_succeeds_one_conflicts()
    {
        var currentUser = new TestCurrentUserService { TenantId = 1, UserId = 1 };
        TeachingGroup tg;
        Domain.Entities.Student student;
        await using (var seedDb = _fixture.CreateDbContext(currentUser))
        {
            (tg, student) = await new TeachingGroupMembershipPgSeed(seedDb)
                .SeedExplicitTeachingGroupWithStudentAsync();
        }

        await using var db1 = _fixture.CreateDbContext(currentUser);
        await using var db2 = _fixture.CreateDbContext(currentUser);

        using var bothReady = new CountdownEvent(2);

        async Task<(bool Succeeded, Exception? Failure)> RaceAsync(ApplicationDbContext db)
        {
            await db.AddAsync(NewCurrentMembership(tg.Id, student.Id));
            bothReady.Signal();
            bothReady.Wait();

            try
            {
                await db.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateException ex)
            {
                if (TeachingGroupMembershipPersistenceExceptionMapper.TryMapCurrentMembershipUniqueViolation(
                        ex, out var conflict))
                    return (false, conflict);

                return (false, ex);
            }
            catch (Exception ex)
            {
                return (false, ex);
            }
        }

        var results = await Task.WhenAll(Task.Run(() => RaceAsync(db1)), Task.Run(() => RaceAsync(db2)));

        results.Count(r => r.Succeeded).Should().Be(1);
        results.Count(r => !r.Succeeded).Should().Be(1);

        var failure = results.Single(r => !r.Succeeded).Failure;
        failure.Should().BeOfType<ConcurrencyConflictException>();
        failure!.Message.Should().Be(TeachingGroupMembershipPersistenceExceptionMapper.ConflictMessage);
        failure.Message.Should().NotContain("SQL");
        failure.Message.Should().NotContain("Npgsql");
        failure.Message.Should().NotContain("23505");
        failure.Message.Should().NotContain("IX_");

        await using var verify = _fixture.CreateDbContext(currentUser);
        var currentCount = await verify.Set<TeachingGroupMembership>()
            .CountAsync(m => m.TeachingGroupId == tg.Id
                             && m.StudentId == student.Id
                             && m.IsCurrent
                             && !m.IsDeleted);
        currentCount.Should().Be(1);
    }

    [Fact]
    public async Task Sequential_duplicate_insert_second_maps_to_membership_conflict()
    {
        var currentUser = new TestCurrentUserService { TenantId = 1, UserId = 1 };
        TeachingGroup tg;
        Domain.Entities.Student student;
        await using (var seedDb = _fixture.CreateDbContext(currentUser))
        {
            (tg, student) = await new TeachingGroupMembershipPgSeed(seedDb)
                .SeedExplicitTeachingGroupWithStudentAsync();
        }

        await using var db1 = _fixture.CreateDbContext(currentUser);
        await db1.AddAsync(NewCurrentMembership(tg.Id, student.Id));
        await db1.SaveChangesAsync();

        await using var db2 = _fixture.CreateDbContext(currentUser);
        await db2.AddAsync(NewCurrentMembership(tg.Id, student.Id));
        var act = async () => await db2.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();

        TeachingGroupMembershipPersistenceExceptionMapper
            .TryMapCurrentMembershipUniqueViolation(ex.Which, out var conflict)
            .Should().BeTrue(
                "expected membership unique map; detail={0}",
                DescribePostgres(ex.Which));
        conflict.Message.Should().Be(TeachingGroupMembershipPersistenceExceptionMapper.ConflictMessage);
    }

    [Fact]
    public async Task Foreign_key_violation_is_not_mapped_to_membership_concurrency_conflict()
    {
        var currentUser = new TestCurrentUserService { TenantId = 1, UserId = 1 };
        TeachingGroup tg;
        await using (var seedDb = _fixture.CreateDbContext(currentUser))
        {
            (tg, _) = await new TeachingGroupMembershipPgSeed(seedDb)
                .SeedExplicitTeachingGroupWithStudentAsync();
        }

        await using var db = _fixture.CreateDbContext(currentUser);
        await db.AddAsync(NewCurrentMembership(tg.Id, studentId: 2_147_483_646));
        var act = async () => await db.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();

        TeachingGroupMembershipPersistenceExceptionMapper
            .TryMapCurrentMembershipUniqueViolation(ex.Which, out _)
            .Should().BeFalse("FK violations must not become membership concurrency conflicts");
    }

    [Fact]
    public async Task Unrelated_unique_violation_is_not_mapped_to_membership_concurrency_conflict()
    {
        var currentUser = new TestCurrentUserService { TenantId = 1, UserId = 1 };
        await using var db = _fixture.CreateDbContext(currentUser);
        var (tg, _) = await new TeachingGroupMembershipPgSeed(db)
            .SeedExplicitTeachingGroupWithStudentAsync();

        var sa = await db.Set<SubjectAllocation>().AsNoTracking()
            .FirstAsync(x => x.Id == tg.SubjectAllocationId);

        await db.AddAsync(new SubjectAllocation
        {
            TenantId = sa.TenantId,
            AcademicYearId = sa.AcademicYearId,
            SubjectId = sa.SubjectId,
            StaffId = sa.StaffId,
            CourseId = sa.CourseId,
            GroupId = sa.GroupId,
            SemesterId = sa.SemesterId,
            DepartmentId = sa.DepartmentId,
            WeeklyHours = sa.WeeklyHours,
            EffectiveFrom = sa.EffectiveFrom,
            CreatedDate = DateTime.UtcNow,
        });

        var act = async () => await db.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();

        TeachingGroupMembershipPersistenceExceptionMapper
            .TryMapCurrentMembershipUniqueViolation(ex.Which, out _)
            .Should().BeFalse("Other unique indexes must not map to membership concurrency conflict");
    }

    [Fact]
    public async Task Application_service_duplicate_add_is_idempotent_under_postgres()
    {
        var currentUser = new TestCurrentUserService { TenantId = 1, UserId = 1 };
        await using var db = _fixture.CreateDbContext(currentUser);
        var (tg, student) = await new TeachingGroupMembershipPgSeed(db)
            .SeedExplicitTeachingGroupWithStudentAsync();

        var resolver = new TeachingGroupMembershipResolver(db);
        var memberships = new TeachingGroupMembershipApplicationService(db, db, currentUser, resolver);

        await memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest { StudentIds = [student.Id] });
        await memberships.AddMembersAsync(tg.Id, new AddTeachingGroupMembersRequest { StudentIds = [student.Id] });

        var count = await db.Set<TeachingGroupMembership>()
            .CountAsync(m => m.TeachingGroupId == tg.Id && m.IsCurrent && !m.IsDeleted);
        count.Should().Be(1);
    }

    private static string DescribePostgres(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null)
            return "no-inner";
        var t = inner.GetType();
        var sql = t.GetProperty("SqlState")?.GetValue(inner) as string;
        var c = t.GetProperty("ConstraintName")?.GetValue(inner) as string;
        var table = t.GetProperty("TableName")?.GetValue(inner) as string;
        return $"{t.FullName}; SqlState={sql}; Constraint={c}; Table={table}; Msg={inner.Message}";
    }

    private static TeachingGroupMembership NewCurrentMembership(int tgId, int studentId) => new()
    {
        TenantId = 1,
        TeachingGroupId = tgId,
        StudentId = studentId,
        Inclusion = TeachingGroupMembershipInclusion.Include,
        EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
        IsCurrent = true,
        CreatedDate = DateTime.UtcNow,
    };
}
