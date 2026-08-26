using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Internal;

/// <summary>
/// AI-SCHED-TG.5 Prompt 5A.1A — Maps ONLY the approved current-membership unique
/// PostgreSQL constraint/index identity to <see cref="ConcurrencyConflictException"/>.
/// </summary>
internal static class TeachingGroupMembershipPersistenceExceptionMapper
{
    /// <summary>
    /// Actual PostgreSQL catalog identity for the filtered unique index created by
    /// TG.3 migration <c>20260817153000_AI_SCHED_TG_3_TeachingGroup</c>.
    /// PostgreSQL NAMEDATALEN truncates identifiers to 63 bytes; the EF logical name is
    /// 71 characters, so the live catalog / <c>PostgresException.ConstraintName</c> value is
    /// this truncated form (verified via pg_indexes + duplicate-insert ConstraintName).
    /// </summary>
    public const string ApprovedPostgresConstraintName =
        "IX_SchedulingTeachingGroupMembership_TenantId_TeachingGroupId_S";

    /// <summary>EF / migration logical name (may exceed PostgreSQL identifier limit).</summary>
    public const string EfLogicalIndexName =
        "IX_SchedulingTeachingGroupMembership_TenantId_TeachingGroupId_StudentId";

    public const string MembershipTableName = "SchedulingTeachingGroupMembership";

    public const string ConflictMessage =
        "A conflicting membership change was detected. Reload and try again.";

    /// <summary>PostgreSQL unique_violation.</summary>
    public const string PostgresUniqueViolationSqlState = "23505";

    public static bool TryMapCurrentMembershipUniqueViolation(
        DbUpdateException exception,
        out ConcurrencyConflictException conflict)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (Exception? inner = exception; inner is not null; inner = inner.InnerException)
        {
            if (inner is AggregateException aggregate)
            {
                foreach (var nested in aggregate.Flatten().InnerExceptions)
                {
                    if (TryMapPostgresUnique(nested, out conflict))
                        return true;
                }

                continue;
            }

            if (TryMapPostgresUnique(inner, out conflict))
                return true;
        }

        conflict = null!;
        return false;
    }

    /// <summary>
    /// Test seam: evaluates SQLSTATE + ConstraintName only (no table-name authorization).
    /// </summary>
    internal static bool MatchesApprovedMembershipUniqueViolation(string? sqlState, string? constraintName)
        => string.Equals(sqlState, PostgresUniqueViolationSqlState, StringComparison.Ordinal)
           && string.Equals(constraintName, ApprovedPostgresConstraintName, StringComparison.Ordinal);

    private static bool TryMapPostgresUnique(Exception exception, out ConcurrencyConflictException conflict)
    {
        conflict = null!;
        if (!IsPostgresException(exception, out var sqlState, out var constraintName, out _, out _))
            return false;

        if (!MatchesApprovedMembershipUniqueViolation(sqlState, constraintName))
            return false;

        conflict = new ConcurrencyConflictException(ConflictMessage);
        return true;
    }

    public static void RethrowUnlessCurrentMembershipUniqueViolation(DbUpdateException exception)
    {
        if (TryMapCurrentMembershipUniqueViolation(exception, out var conflict))
            throw conflict;
        throw exception;
    }

    /// <summary>
    /// Avoids a hard Application→Npgsql package dependency; uses Npgsql.PostgresException shape via reflection.
    /// </summary>
    private static bool IsPostgresException(
        Exception exception,
        out string? sqlState,
        out string? constraintName,
        out string? tableName,
        out string? message)
    {
        sqlState = null;
        constraintName = null;
        tableName = null;
        message = exception.Message;

        var type = exception.GetType();
        if (!string.Equals(type.FullName, "Npgsql.PostgresException", StringComparison.Ordinal))
            return false;

        sqlState = type.GetProperty("SqlState")?.GetValue(exception) as string;
        constraintName = type.GetProperty("ConstraintName")?.GetValue(exception) as string;
        tableName = type.GetProperty("TableName")?.GetValue(exception) as string;
        return true;
    }
}
