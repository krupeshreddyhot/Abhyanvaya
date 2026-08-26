using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3H (package 3HC1 / PromptCode P1-4-3HC1) —
/// Controlled pre-production wipe of Attendance + Timetable/Scheduling + Teaching Group
/// transactional data, then Student.SemesterId reconciliation via Group → Semester ownership.
/// ALL_OR_NOTHING; no master/Student deletes; no schema hardening; no GroupId invention.
/// </summary>
public sealed class PreProductionTransactionalResetService : IPreProductionTransactionalResetService
{
    public const string PromptCode = PreProductionTransactionalResetCodes.PromptCode;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<PreProductionTransactionalResetService> _logger;

    /// <summary>Unit-test hook: throw after mutations, before commit integrity, to prove rollback.</summary>
    internal Func<CancellationToken, Task>? TestFailureHook { get; set; }

    public PreProductionTransactionalResetService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILogger<PreProductionTransactionalResetService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<PreProductionTransactionalResetPreviewDto> PreviewAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            "Prompt 3HC1 PREVIEW — read-only; zero mutations.",
            "DELETE allowlist = Attendance + Timetable/Scheduling transactional + TeachingGroup + SubjectAllocation.",
            "PRESERVE = Student/Course/Group/Semester/Dept/Program/Tenant/Subject/Auth/Config/Sections/SA preferences.",
            "Student Semester resolved via Group → operational Semester (no hard-coded SemesterIds).",
            $"PromptCode={PromptCode}.",
        };

        var protectedBefore = await CountProtectedAsync(tenantId, cancellationToken);
        var allowlist = await CountAllowlistAsync(tenantId, cancellationToken);
        var denylist = BuildDenylistCounts(protectedBefore);
        var reconciliation = await BuildStudentReconciliationAsync(tenantId, cancellationToken);

        var failClosed = reconciliation.Count(r =>
            r.ResolutionStatus is not StudentSemesterResolutionStatuses.AlreadyCorrect
                and not StudentSemesterResolutionStatuses.UpdateRequired);
        var updateRequired = reconciliation.Count(r =>
            r.ResolutionStatus == StudentSemesterResolutionStatuses.UpdateRequired);
        var already = reconciliation.Count(r =>
            r.ResolutionStatus == StudentSemesterResolutionStatuses.AlreadyCorrect);

        var blockers = new List<string>();
        if (failClosed > 0)
            blockers.Add($"{failClosed} Student(s) cannot be deterministically reconciled (fail closed).");
        if (tenantId <= 0)
            blockers.Add("TenantId missing; fail closed.");

        return new PreProductionTransactionalResetPreviewDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            PromptCode = PromptCode,
            IsReadOnly = true,
            SaveChangesInvoked = false,
            IsCleanupReady = blockers.Count == 0,
            AbortReason = blockers.Count == 0 ? null : string.Join(" ", blockers),
            ProtectedBefore = protectedBefore,
            DeletionAllowlistCounts = allowlist,
            ProtectedDenylistCounts = denylist,
            DeletionOrder = PreProductionTransactionalResetAllowlist.DeletionOrder,
            TransactionalTotal = allowlist.Sum(a => a.Count),
            StudentsUpdateRequired = updateRequired,
            StudentsAlreadyCorrect = already,
            StudentsFailClosed = failClosed,
            StudentReconciliation = reconciliation,
            Blockers = blockers,
            Notes = notes,
            SchemaHardeningDeferred = true,
        };
    }

    public async Task<PreProductionTransactionalResetExecuteResultDto> ExecuteAsync(
        PreProductionTransactionalResetExecuteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var correlationId = Guid.NewGuid().ToString("N");
        PreProductionTransactionalResetExecuteResultDto? result = null;

        try
        {
            await _db.ExecuteInTransactionAsync(async ct =>
            {
                result = await ExecuteCoreAsync(request, correlationId, ct);
                if (result is null)
                    throw new DomainException("Pre-production reset produced no result.");
                if (!result.IsSuccessful
                    && !string.Equals(result.ExecutionStatus, "AlreadyComplete", StringComparison.Ordinal))
                    throw new DomainException(result.AbortReason ?? "Pre-production reset aborted.");
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException ex)
        {
            _logger.LogWarning(ex, "P1-4-3HC1 concurrency conflict; rolled back.");
            return Aborted(correlationId, ex.Message, result);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "P1-4-3HC1 EF concurrency conflict; rolled back.");
            return Aborted(correlationId, "Concurrency conflict during pre-production reset.", result);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "P1-4-3HC1 aborted and rolled back.");
            return Aborted(correlationId, ex.Message, result);
        }

        return result!;
    }

    private async Task<PreProductionTransactionalResetExecuteResultDto> ExecuteCoreAsync(
        PreProductionTransactionalResetExecuteRequest request,
        string correlationId,
        CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        var notes = new List<string>
        {
            "Prompt 3HC1 EXECUTE — ALL_OR_NOTHING.",
            $"CorrelationId={correlationId}; ActorUserId={_currentUser.UserId}.",
            "No TRUNCATE/DROP; allowlist Remove only; Student/master never deleted.",
        };

        if (!request.Confirm
            || !string.Equals(request.ConfirmationPhrase,
                PreProductionTransactionalResetCodes.ConfirmationPhrase, StringComparison.Ordinal))
        {
            return Fail(tenantId, correlationId,
                "Confirm=true and ConfirmationPhrase=PREPRODUCTION_TRANSACTIONAL_RESET are required.",
                notes);
        }

        if (tenantId <= 0)
            return Fail(tenantId, correlationId, "TenantId missing; fail closed.", notes);

        var preview = await PreviewAsync(ct);
        if (!preview.IsCleanupReady)
            return Fail(tenantId, correlationId, preview.AbortReason ?? "Preview not ready.", notes, preview);

        var protectedBefore = preview.ProtectedBefore;
        var transactionalTotal = preview.TransactionalTotal;
        var updateRequired = preview.StudentsUpdateRequired;

        if (transactionalTotal == 0 && updateRequired == 0)
        {
            notes.Add("AlreadyComplete — zero transactional rows and zero Student updates.");
            return new PreProductionTransactionalResetExecuteResultDto
            {
                GeneratedUtc = DateTime.UtcNow,
                TenantId = tenantId,
                PromptCode = PromptCode,
                CorrelationId = correlationId,
                IsSuccessful = true,
                ExecutionStatus = "AlreadyComplete",
                TransactionCommitted = true,
                TransactionModel = "ALL_OR_NOTHING",
                ProtectedBefore = protectedBefore,
                ProtectedAfter = protectedBefore,
                DeletedCounts = preview.DeletionAllowlistCounts,
                TotalDeleted = 0,
                StudentsUpdated = 0,
                StudentsAlreadyCorrect = preview.StudentsAlreadyCorrect,
                StudentReconciliation = preview.StudentReconciliation,
                IdempotentZeroMutation = true,
                PostIntegrityPassed = true,
                Notes = notes,
                SchemaHardeningDeferred = true,
            };
        }

        // Re-resolve students inside the transaction (authoritative).
        var reconciliation = await BuildStudentReconciliationAsync(tenantId, ct);
        if (reconciliation.Any(r =>
                r.ResolutionStatus is not StudentSemesterResolutionStatuses.AlreadyCorrect
                    and not StudentSemesterResolutionStatuses.UpdateRequired))
        {
            return Fail(tenantId, correlationId,
                "Student Semester reconciliation fail-closed; no mutations applied.",
                notes,
                studentRows: reconciliation);
        }

        var deletedCounts = new List<PreProductionEntityCountDto>();
        var totalDeleted = 0;

        async Task<int> WipeAsync<T>(string name, IQueryable<T> query) where T : class
        {
            var rows = await query.ToListAsync(ct);
            foreach (var row in rows)
                _db.Remove(row);
            deletedCounts.Add(new PreProductionEntityCountDto
            {
                Entity = name,
                Classification = PreProductionTransactionalResetAllowlist.Delete,
                Count = rows.Count,
                RecommendedAction = "DELETED",
            });
            return rows.Count;
        }

        // --- Attendance ---
        var recognitionIds = await _db.QueryIgnoringFilters<AttendanceRecognition>()
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.Id)
            .ToListAsync(ct);
        if (recognitionIds.Count > 0)
        {
            totalDeleted += await WipeAsync("AttendanceRecognitionReviewHistory",
                _db.QueryIgnoringFilters<AttendanceRecognitionReviewHistory>()
                    .Where(h => recognitionIds.Contains(h.RecognitionId)));
        }
        else
        {
            deletedCounts.Add(Empty("AttendanceRecognitionReviewHistory"));
        }

        totalDeleted += await WipeAsync("AttendanceDetail",
            _db.QueryIgnoringFilters<AttendanceDetail>().Where(d => d.TenantId == tenantId));
        totalDeleted += await WipeAsync("Attendance",
            _db.QueryIgnoringFilters<Attendance>().Where(a => a.TenantId == tenantId));
        totalDeleted += await WipeAsync("AttendanceRecognition",
            _db.QueryIgnoringFilters<AttendanceRecognition>().Where(r => r.TenantId == tenantId));
        totalDeleted += await WipeAsync("AttendanceSessionImage",
            _db.QueryIgnoringFilters<AttendanceSessionImage>().Where(i => i.TenantId == tenantId));
        totalDeleted += await WipeAsync("AttendanceRetryHistory",
            _db.QueryIgnoringFilters<AttendanceRetryHistory>().Where(h => h.TenantId == tenantId));
        totalDeleted += await WipeAsync("AttendanceSessionSection",
            _db.QueryIgnoringFilters<AttendanceSessionSection>().Where(s => s.TenantId == tenantId));
        totalDeleted += await WipeAsync("AttendanceSession",
            _db.QueryIgnoringFilters<AttendanceSession>().Where(s => s.TenantId == tenantId));
        totalDeleted += await WipeAsync("ClassSchedule",
            _db.QueryIgnoringFilters<ClassSchedule>().Where(c => c.TenantId == tenantId));
        totalDeleted += await WipeAsync("AttendanceBulkOperationHistory",
            _db.QueryIgnoringFilters<AttendanceBulkOperationHistory>().Where(h => h.TenantId == tenantId));

        // --- Conflict / TT / Optimization ---
        totalDeleted += await WipeAsync("ConflictFinding",
            _db.QueryIgnoringFilters<ConflictFinding>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("ConflictWorkspacePin",
            _db.QueryIgnoringFilters<ConflictWorkspacePin>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("ConflictWorkspaceBookmark",
            _db.QueryIgnoringFilters<ConflictWorkspaceBookmark>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("ConflictWorkspaceNote",
            _db.QueryIgnoringFilters<ConflictWorkspaceNote>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("ConflictDetectionRun",
            _db.QueryIgnoringFilters<ConflictDetectionRun>().Where(x => x.TenantId == tenantId));

        totalDeleted += await WipeAsync("TimetableApprovalHistory",
            _db.QueryIgnoringFilters<TimetableApprovalHistory>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("TimetableApprovalComment",
            _db.QueryIgnoringFilters<TimetableApprovalComment>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("TimetableDecisionHistory",
            _db.QueryIgnoringFilters<TimetableDecisionHistory>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("TimetableApprovalStep",
            _db.QueryIgnoringFilters<TimetableApprovalStep>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("TimetableApprovalRequest",
            _db.QueryIgnoringFilters<TimetableApprovalRequest>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("TimetableChangeHistory",
            _db.QueryIgnoringFilters<TimetableChangeHistory>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("TimetableWarningDismissal",
            _db.QueryIgnoringFilters<TimetableWarningDismissal>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("TimetableCloneJob",
            _db.QueryIgnoringFilters<TimetableCloneJob>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("TimetableSection",
            _db.QueryIgnoringFilters<TimetableSection>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("TimetableEntry",
            _db.QueryIgnoringFilters<TimetableEntry>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("Timetable",
            _db.QueryIgnoringFilters<Timetable>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("ScheduleVersion",
            _db.QueryIgnoringFilters<ScheduleVersion>().Where(x => x.TenantId == tenantId));

        totalDeleted += await WipeAsync("OptimizationScenarioComment",
            _db.QueryIgnoringFilters<OptimizationScenarioComment>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("OptimizationScenarioNote",
            _db.QueryIgnoringFilters<OptimizationScenarioNote>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("OptimizationScenarioFavorite",
            _db.QueryIgnoringFilters<OptimizationScenarioFavorite>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("OptimizationScenarioBookmark",
            _db.QueryIgnoringFilters<OptimizationScenarioBookmark>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("OptimizationScenarioApprovalRequest",
            _db.QueryIgnoringFilters<OptimizationScenarioApprovalRequest>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("OptimizationScenarioShare",
            _db.QueryIgnoringFilters<OptimizationScenarioShare>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("OptimizationScenarioHistory",
            _db.QueryIgnoringFilters<OptimizationScenarioHistory>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("OptimizationMetricSnapshot",
            _db.QueryIgnoringFilters<OptimizationMetricSnapshot>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("OptimizationTelemetryAggregate",
            _db.QueryIgnoringFilters<OptimizationTelemetryAggregate>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("OptimizationSnapshot",
            _db.QueryIgnoringFilters<OptimizationSnapshot>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("OptimizationSimulationRun",
            _db.QueryIgnoringFilters<OptimizationSimulationRun>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("OptimizationEngineRun",
            _db.QueryIgnoringFilters<OptimizationEngineRun>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("OptimizationScenario",
            _db.QueryIgnoringFilters<OptimizationScenario>().Where(x => x.TenantId == tenantId));

        // --- Teaching groups then SA ---
        totalDeleted += await WipeAsync("TeachingGroupMembership",
            _db.QueryIgnoringFilters<TeachingGroupMembership>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("TeachingGroupSection",
            _db.QueryIgnoringFilters<TeachingGroupSection>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("TeachingGroup",
            _db.QueryIgnoringFilters<TeachingGroup>().Where(x => x.TenantId == tenantId));
        totalDeleted += await WipeAsync("SubjectAllocation",
            _db.QueryIgnoringFilters<SubjectAllocation>().Where(x => x.TenantId == tenantId));

        // --- Student Semester reconciliation (UPDATE only; never delete) ---
        var studentsUpdated = 0;
        var students = await _db.QueryIgnoringFilters<Student>()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .ToListAsync(ct);
        var byId = reconciliation.ToDictionary(r => r.StudentId);
        foreach (var student in students)
        {
            if (!byId.TryGetValue(student.Id, out var row))
                throw new DomainException($"Student {student.Id} missing from reconciliation inventory.");
            if (row.ResolutionStatus == StudentSemesterResolutionStatuses.UpdateRequired)
            {
                if (row.ResolvedSemesterId is null)
                    throw new DomainException($"Student {student.Id} UPDATE_REQUIRED without ResolvedSemesterId.");
                student.SemesterId = row.ResolvedSemesterId.Value;
                student.UpdatedDate = DateTime.UtcNow;
                student.UpdatedBy = _currentUser.UserId;
                studentsUpdated++;
            }
        }

        var journalSemesterId = reconciliation
            .Select(r => r.ResolvedSemesterId)
            .FirstOrDefault(id => id is > 0)
            ?? await _db.Semesters.AsNoTracking()
                .Where(s => s.TenantId == tenantId && !s.IsDeleted)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct)
            ?? 0;

        if (journalSemesterId > 0)
        {
            await _db.AddAsync(new LegacySemesterDispositionJournal
            {
                TenantId = tenantId,
                SemesterId = journalSemesterId,
                DispositionCode = PreProductionTransactionalResetCodes.DispositionCode,
                Evidence = Truncate(
                    $"corr={correlationId}; deleted={totalDeleted}; studentsUpdated={studentsUpdated}; " +
                    $"reason={request.Reason ?? "pre-production reset"}; noStudentDelete=true; noMasterDelete=true; " +
                    "tenantWide=true; source=P1-4-3HC1",
                    2000),
                PromptCode = PromptCode,
                AssignedGroupId = null,
                SemesterRowMutated = false,
                FinalizedUtc = DateTime.UtcNow,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId,
            });
        }

        if (TestFailureHook is not null)
            await TestFailureHook(ct);

        await ConcurrencyExceptionHelper.SaveChangesAsync(_db, ct);

        var protectedAfter = await CountProtectedAsync(tenantId, ct);
        if (!ProtectedEqual(protectedBefore, protectedAfter))
            throw new DomainException("Protected entity counts changed; fail closed / rollback.");

        var residual = await CountAllowlistAsync(tenantId, ct);
        if (residual.Any(r => r.Count > 0))
            throw new DomainException("Transactional residual remains after wipe; fail closed / rollback.");

        var postStudents = await BuildStudentReconciliationAsync(tenantId, ct);
        if (postStudents.Any(r => r.ResolutionStatus != StudentSemesterResolutionStatuses.AlreadyCorrect))
            throw new DomainException("Post-cleanup Student Semester integrity failed; rollback.");

        notes.Add($"Deleted={totalDeleted}; StudentsUpdated={studentsUpdated}; PostIntegrity=PASS.");
        return new PreProductionTransactionalResetExecuteResultDto
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            PromptCode = PromptCode,
            CorrelationId = correlationId,
            IsSuccessful = true,
            ExecutionStatus = "Completed",
            RolledBack = false,
            TransactionCommitted = true,
            TransactionModel = "ALL_OR_NOTHING",
            ProtectedBefore = protectedBefore,
            ProtectedAfter = protectedAfter,
            DeletedCounts = deletedCounts,
            TotalDeleted = totalDeleted,
            StudentsUpdated = studentsUpdated,
            StudentsAlreadyCorrect = reconciliation.Count(r =>
                r.ResolutionStatus == StudentSemesterResolutionStatuses.AlreadyCorrect),
            StudentReconciliation = postStudents,
            IdempotentZeroMutation = false,
            PostIntegrityPassed = true,
            Notes = notes,
            SchemaHardeningDeferred = true,
            StudentsDeleted = false,
            MasterDataDeleted = false,
        };
    }

    private async Task<IReadOnlyList<StudentSemesterReconciliationRowDto>> BuildStudentReconciliationAsync(
        int tenantId,
        CancellationToken ct)
    {
        var students = await _db.Students.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new
            {
                s.Id,
                s.StudentNumber,
                s.CourseId,
                s.GroupId,
                s.SemesterId,
            })
            .ToListAsync(ct);

        var groups = await _db.Groups.AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted)
            .Select(g => new { g.Id, g.CourseId })
            .ToDictionaryAsync(g => g.Id, ct);

        var operational = await OperationalSemesterRules.WhereOperational(
                _db.Semesters.AsNoTracking().Where(s => s.TenantId == tenantId))
            .Select(s => new { s.Id, s.GroupId, s.Number, s.CourseId })
            .ToListAsync(ct);

        var byGroup = operational
            .Where(s => s.GroupId.HasValue)
            .GroupBy(s => s.GroupId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<StudentSemesterReconciliationRowDto>();
        foreach (var st in students.OrderBy(s => s.Id))
        {
            if (!groups.TryGetValue(st.GroupId, out var group))
            {
                rows.Add(Row(st.Id, st.StudentNumber, st.CourseId, st.GroupId, st.SemesterId, null,
                    StudentSemesterResolutionStatuses.InvalidGroup, "Group missing for tenant."));
                continue;
            }

            if (group.CourseId != st.CourseId)
            {
                rows.Add(Row(st.Id, st.StudentNumber, st.CourseId, st.GroupId, st.SemesterId, null,
                    StudentSemesterResolutionStatuses.CourseGroupMismatch,
                    $"Group.CourseId={group.CourseId} != Student.CourseId={st.CourseId}."));
                continue;
            }

            if (!byGroup.TryGetValue(st.GroupId, out var candidates) || candidates.Count == 0)
            {
                rows.Add(Row(st.Id, st.StudentNumber, st.CourseId, st.GroupId, st.SemesterId, null,
                    StudentSemesterResolutionStatuses.NoSemester,
                    "No operational Group-owned Semester for Student.GroupId."));
                continue;
            }

            int resolvedId;
            string evidence;
            if (candidates.Count == 1)
            {
                resolvedId = candidates[0].Id;
                evidence = $"Unique operational Semester for GroupId={st.GroupId} → {resolvedId}.";
            }
            else
            {
                var currentNumber = await _db.Semesters.AsNoTracking()
                    .Where(s => s.Id == st.SemesterId && s.TenantId == tenantId)
                    .Select(s => (int?)s.Number)
                    .FirstOrDefaultAsync(ct);
                var byNumber = currentNumber is null
                    ? []
                    : candidates.Where(c => c.Number == currentNumber.Value).ToList();
                if (byNumber.Count != 1)
                {
                    rows.Add(Row(st.Id, st.StudentNumber, st.CourseId, st.GroupId, st.SemesterId, null,
                        StudentSemesterResolutionStatuses.Ambiguous,
                        $"Multiple operational Semesters for GroupId={st.GroupId} ({candidates.Count}); cannot resolve uniquely."));
                    continue;
                }

                resolvedId = byNumber[0].Id;
                evidence =
                    $"Resolved by Semester.Number={currentNumber} among {candidates.Count} Group Semesters → {resolvedId}.";
            }

            var status = resolvedId == st.SemesterId
                ? StudentSemesterResolutionStatuses.AlreadyCorrect
                : StudentSemesterResolutionStatuses.UpdateRequired;
            rows.Add(Row(st.Id, st.StudentNumber, st.CourseId, st.GroupId, st.SemesterId, resolvedId, status, evidence));
        }

        return rows;
    }

    private async Task<ProtectedCountsDto> CountProtectedAsync(int tenantId, CancellationToken ct)
        => new()
        {
            Students = await _db.QueryIgnoringFilters<Student>()
                .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct),
            Courses = await _db.QueryIgnoringFilters<Course>()
                .CountAsync(c => c.TenantId == tenantId && !c.IsDeleted, ct),
            Groups = await _db.QueryIgnoringFilters<Group>()
                .CountAsync(g => g.TenantId == tenantId && !g.IsDeleted, ct),
            Semesters = await _db.QueryIgnoringFilters<Semester>()
                .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct),
            Departments = await _db.QueryIgnoringFilters<Department>()
                .CountAsync(d => d.TenantId == tenantId && !d.IsDeleted, ct),
            Programs = await _db.QueryIgnoringFilters<Program>()
                .CountAsync(p => p.TenantId == tenantId && !p.IsDeleted, ct),
            Colleges = await _db.QueryIgnoringFilters<College>()
                .CountAsync(c => c.TenantId == tenantId && !c.IsDeleted, ct),
            Subjects = await _db.QueryIgnoringFilters<Subject>()
                .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct),
            Users = await _db.QueryIgnoringFilters<User>()
                .CountAsync(u => u.TenantId == tenantId && !u.IsDeleted, ct),
            Permissions = await _db.Permissions.AsNoTracking().CountAsync(ct),
            ApplicationRoles = await _db.ApplicationRoles.AsNoTracking().CountAsync(ct),
            TenantAcademicConfigurations = await _db.QueryIgnoringFilters<TenantAcademicConfiguration>()
                .CountAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct),
        };

    private async Task<IReadOnlyList<PreProductionEntityCountDto>> CountAllowlistAsync(
        int tenantId,
        CancellationToken ct)
    {
        var list = new List<PreProductionEntityCountDto>
        {
            await C<AttendanceRecognitionReviewHistory>("AttendanceRecognitionReviewHistory", async () =>
            {
                var ids = await _db.QueryIgnoringFilters<AttendanceRecognition>()
                    .Where(r => r.TenantId == tenantId).Select(r => r.Id).ToListAsync(ct);
                return ids.Count == 0
                    ? 0
                    : await _db.QueryIgnoringFilters<AttendanceRecognitionReviewHistory>()
                        .CountAsync(h => ids.Contains(h.RecognitionId), ct);
            }),
            await C<AttendanceDetail>("AttendanceDetail",
                () => _db.QueryIgnoringFilters<AttendanceDetail>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<Attendance>("Attendance",
                () => _db.QueryIgnoringFilters<Attendance>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<AttendanceRecognition>("AttendanceRecognition",
                () => _db.QueryIgnoringFilters<AttendanceRecognition>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<AttendanceSessionImage>("AttendanceSessionImage",
                () => _db.QueryIgnoringFilters<AttendanceSessionImage>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<AttendanceRetryHistory>("AttendanceRetryHistory",
                () => _db.QueryIgnoringFilters<AttendanceRetryHistory>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<AttendanceSessionSection>("AttendanceSessionSection",
                () => _db.QueryIgnoringFilters<AttendanceSessionSection>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<AttendanceSession>("AttendanceSession",
                () => _db.QueryIgnoringFilters<AttendanceSession>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<ClassSchedule>("ClassSchedule",
                () => _db.QueryIgnoringFilters<ClassSchedule>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<AttendanceBulkOperationHistory>("AttendanceBulkOperationHistory",
                () => _db.QueryIgnoringFilters<AttendanceBulkOperationHistory>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<ConflictFinding>("ConflictFinding",
                () => _db.QueryIgnoringFilters<ConflictFinding>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<ConflictWorkspacePin>("ConflictWorkspacePin",
                () => _db.QueryIgnoringFilters<ConflictWorkspacePin>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<ConflictWorkspaceBookmark>("ConflictWorkspaceBookmark",
                () => _db.QueryIgnoringFilters<ConflictWorkspaceBookmark>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<ConflictWorkspaceNote>("ConflictWorkspaceNote",
                () => _db.QueryIgnoringFilters<ConflictWorkspaceNote>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<ConflictDetectionRun>("ConflictDetectionRun",
                () => _db.QueryIgnoringFilters<ConflictDetectionRun>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<TimetableApprovalHistory>("TimetableApprovalHistory",
                () => _db.QueryIgnoringFilters<TimetableApprovalHistory>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<TimetableApprovalComment>("TimetableApprovalComment",
                () => _db.QueryIgnoringFilters<TimetableApprovalComment>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<TimetableDecisionHistory>("TimetableDecisionHistory",
                () => _db.QueryIgnoringFilters<TimetableDecisionHistory>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<TimetableApprovalStep>("TimetableApprovalStep",
                () => _db.QueryIgnoringFilters<TimetableApprovalStep>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<TimetableApprovalRequest>("TimetableApprovalRequest",
                () => _db.QueryIgnoringFilters<TimetableApprovalRequest>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<TimetableChangeHistory>("TimetableChangeHistory",
                () => _db.QueryIgnoringFilters<TimetableChangeHistory>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<TimetableWarningDismissal>("TimetableWarningDismissal",
                () => _db.QueryIgnoringFilters<TimetableWarningDismissal>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<TimetableCloneJob>("TimetableCloneJob",
                () => _db.QueryIgnoringFilters<TimetableCloneJob>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<TimetableSection>("TimetableSection",
                () => _db.QueryIgnoringFilters<TimetableSection>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<TimetableEntry>("TimetableEntry",
                () => _db.QueryIgnoringFilters<TimetableEntry>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<Timetable>("Timetable",
                () => _db.QueryIgnoringFilters<Timetable>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<ScheduleVersion>("ScheduleVersion",
                () => _db.QueryIgnoringFilters<ScheduleVersion>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<OptimizationScenarioComment>("OptimizationScenarioComment",
                () => _db.QueryIgnoringFilters<OptimizationScenarioComment>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<OptimizationScenarioNote>("OptimizationScenarioNote",
                () => _db.QueryIgnoringFilters<OptimizationScenarioNote>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<OptimizationScenarioFavorite>("OptimizationScenarioFavorite",
                () => _db.QueryIgnoringFilters<OptimizationScenarioFavorite>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<OptimizationScenarioBookmark>("OptimizationScenarioBookmark",
                () => _db.QueryIgnoringFilters<OptimizationScenarioBookmark>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<OptimizationScenarioApprovalRequest>("OptimizationScenarioApprovalRequest",
                () => _db.QueryIgnoringFilters<OptimizationScenarioApprovalRequest>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<OptimizationScenarioShare>("OptimizationScenarioShare",
                () => _db.QueryIgnoringFilters<OptimizationScenarioShare>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<OptimizationScenarioHistory>("OptimizationScenarioHistory",
                () => _db.QueryIgnoringFilters<OptimizationScenarioHistory>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<OptimizationMetricSnapshot>("OptimizationMetricSnapshot",
                () => _db.QueryIgnoringFilters<OptimizationMetricSnapshot>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<OptimizationTelemetryAggregate>("OptimizationTelemetryAggregate",
                () => _db.QueryIgnoringFilters<OptimizationTelemetryAggregate>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<OptimizationSnapshot>("OptimizationSnapshot",
                () => _db.QueryIgnoringFilters<OptimizationSnapshot>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<OptimizationSimulationRun>("OptimizationSimulationRun",
                () => _db.QueryIgnoringFilters<OptimizationSimulationRun>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<OptimizationEngineRun>("OptimizationEngineRun",
                () => _db.QueryIgnoringFilters<OptimizationEngineRun>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<OptimizationScenario>("OptimizationScenario",
                () => _db.QueryIgnoringFilters<OptimizationScenario>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<TeachingGroupMembership>("TeachingGroupMembership",
                () => _db.QueryIgnoringFilters<TeachingGroupMembership>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<TeachingGroupSection>("TeachingGroupSection",
                () => _db.QueryIgnoringFilters<TeachingGroupSection>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<TeachingGroup>("TeachingGroup",
                () => _db.QueryIgnoringFilters<TeachingGroup>().CountAsync(x => x.TenantId == tenantId, ct)),
            await C<SubjectAllocation>("SubjectAllocation",
                () => _db.QueryIgnoringFilters<SubjectAllocation>().CountAsync(x => x.TenantId == tenantId, ct)),
        };
        return list;

        static async Task<PreProductionEntityCountDto> C<T>(string name, Func<Task<int>> count)
            => new()
            {
                Entity = name,
                Classification = PreProductionTransactionalResetAllowlist.Delete,
                Count = await count(),
                RecommendedAction = PreProductionTransactionalResetAllowlist.Delete,
            };
    }

    private static IReadOnlyList<PreProductionEntityCountDto> BuildDenylistCounts(ProtectedCountsDto p)
        =>
        [
            Denylist("Student", p.Students),
            Denylist("Course", p.Courses),
            Denylist("Group", p.Groups),
            Denylist("Semester", p.Semesters),
            Denylist("Department", p.Departments),
            Denylist("Program", p.Programs),
            Denylist("College", p.Colleges),
            Denylist("Subject", p.Subjects),
            Denylist("User", p.Users),
            Denylist("Permission", p.Permissions),
            Denylist("ApplicationRole", p.ApplicationRoles),
            Denylist("TenantAcademicConfiguration", p.TenantAcademicConfigurations),
        ];

    private static PreProductionEntityCountDto Denylist(string entity, int count)
        => new()
        {
            Entity = entity,
            Classification = PreProductionTransactionalResetAllowlist.Preserve,
            Count = count,
            RecommendedAction = PreProductionTransactionalResetAllowlist.Preserve,
        };

    private static PreProductionEntityCountDto Empty(string name)
        => new()
        {
            Entity = name,
            Classification = PreProductionTransactionalResetAllowlist.Delete,
            Count = 0,
            RecommendedAction = "DELETED",
        };

    private static bool ProtectedEqual(ProtectedCountsDto a, ProtectedCountsDto b)
        => a.Students == b.Students
           && a.Courses == b.Courses
           && a.Groups == b.Groups
           && a.Semesters == b.Semesters
           && a.Departments == b.Departments
           && a.Programs == b.Programs
           && a.Colleges == b.Colleges
           && a.Subjects == b.Subjects
           && a.Users == b.Users
           && a.Permissions == b.Permissions
           && a.ApplicationRoles == b.ApplicationRoles
           && a.TenantAcademicConfigurations == b.TenantAcademicConfigurations;

    private static StudentSemesterReconciliationRowDto Row(
        int id, string number, int courseId, int groupId, int current, int? resolved, string status, string evidence)
        => new()
        {
            StudentId = id,
            StudentNumber = number,
            CourseId = courseId,
            GroupId = groupId,
            CurrentSemesterId = current,
            ResolvedSemesterId = resolved,
            ResolutionStatus = status,
            Evidence = evidence,
        };

    private PreProductionTransactionalResetExecuteResultDto Fail(
        int tenantId,
        string correlationId,
        string reason,
        List<string> notes,
        PreProductionTransactionalResetPreviewDto? preview = null,
        IReadOnlyList<StudentSemesterReconciliationRowDto>? studentRows = null)
        => new()
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = tenantId,
            PromptCode = PromptCode,
            CorrelationId = correlationId,
            IsSuccessful = false,
            ExecutionStatus = "Aborted",
            AbortReason = reason,
            TransactionModel = "ALL_OR_NOTHING",
            ProtectedBefore = preview?.ProtectedBefore ?? new(),
            StudentReconciliation = studentRows ?? preview?.StudentReconciliation ?? [],
            Notes = notes,
            SchemaHardeningDeferred = true,
        };

    private PreProductionTransactionalResetExecuteResultDto Aborted(
        string correlationId,
        string reason,
        PreProductionTransactionalResetExecuteResultDto? partial)
        => new()
        {
            GeneratedUtc = DateTime.UtcNow,
            TenantId = _currentUser.TenantId,
            PromptCode = PromptCode,
            CorrelationId = correlationId,
            IsSuccessful = false,
            ExecutionStatus = "Aborted",
            RolledBack = true,
            TransactionCommitted = false,
            TransactionModel = "ALL_OR_NOTHING",
            AbortReason = reason,
            ProtectedBefore = partial?.ProtectedBefore ?? new(),
            ProtectedAfter = partial?.ProtectedAfter ?? new(),
            DeletedCounts = partial?.DeletedCounts ?? [],
            StudentReconciliation = partial?.StudentReconciliation ?? [],
            Notes = (partial?.Notes ?? []).Concat(["Rolled back."]).ToList(),
            SchemaHardeningDeferred = true,
        };

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
