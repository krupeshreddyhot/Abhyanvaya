using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic.Allocation;

public sealed class SectionAllocationContextBuilder : ISectionAllocationContextBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISectionCapacityEngine _capacity;
    private readonly ISectionPolicyService _policies;
    private readonly ISectionReadinessService _readiness;
    private readonly ISectionHealthService _health;
    private readonly ISectionCapacityRecommendationService _recommendations;
    private readonly ISectionCapacityHistoryService _capacityHistory;
    private readonly ISectionVersioningService _versions;
    private readonly ISectionMergeService _merge;
    private readonly ISectionSplitService _split;
    private readonly IAllocationSnapshotService _snapshots;
    private readonly ISectionAllocationContextValidator _validator;
    private readonly IAcademicTelemetryService _telemetry;

    private AllocationContextCompositionReport? _lastComposition;

    public SectionAllocationContextBuilder(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISectionCapacityEngine capacity,
        ISectionPolicyService policies,
        ISectionReadinessService readiness,
        ISectionHealthService health,
        ISectionCapacityRecommendationService recommendations,
        ISectionCapacityHistoryService capacityHistory,
        ISectionVersioningService versions,
        ISectionMergeService merge,
        ISectionSplitService split,
        IAllocationSnapshotService snapshots,
        ISectionAllocationContextValidator validator,
        IAcademicTelemetryService telemetry)
    {
        _db = db;
        _currentUser = currentUser;
        _capacity = capacity;
        _policies = policies;
        _readiness = readiness;
        _health = health;
        _recommendations = recommendations;
        _capacityHistory = capacityHistory;
        _versions = versions;
        _merge = merge;
        _split = split;
        _snapshots = snapshots;
        _validator = validator;
        _telemetry = telemetry;
    }

    public Task<SectionAllocationContext> BuildAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.AllocationContextBuild,
            "AllocationContext.Build",
            ct => BuildCoreAsync(scope, ct),
            cancellationToken);

    public Task<SectionAllocationContext> RefreshAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.AllocationContextRefresh,
            "AllocationContext.Refresh",
            ct => BuildCoreAsync(scope, ct),
            cancellationToken);

    public async Task<AllocationSnapshotDto> SnapshotAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default)
        => await _telemetry.TrackAsync(
            AcademicOperations.AllocationSnapshot,
            "AllocationContext.Snapshot",
            async ct =>
            {
                var ctx = await BuildCoreAsync(scope, ct);
                return await _snapshots.CreateAsync(ctx, scope, ct);
            },
            cancellationToken);

    public async Task<AllocationValidationReport> ValidateAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default)
        => await _telemetry.TrackAsync(
            AcademicOperations.AllocationValidation,
            "AllocationContext.Validate",
            async ct =>
            {
                var ctx = await BuildCoreAsync(scope, ct);
                return await _validator.ValidateAsync(ctx, ct);
            },
            cancellationToken);

    public Task<SectionAllocationAnalysisContext> BuildAnalysisContextAsync(
        AllocationScopeRequest scope,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.AllocationContextBuild,
            "AllocationContext.BuildAnalysis",
            ct => BuildAnalysisCoreAsync(scope, ct),
            cancellationToken);

    public Task<AllocationContextCompositionReport?> GetLastCompositionReportAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_lastComposition);

    private async Task<SectionAllocationContext> BuildCoreAsync(AllocationScopeRequest scope, CancellationToken ct)
    {
        EnsureScope(scope);
        var steps = new List<AllocationCompositionStep>();
        var warnings = new List<string>();
        var total = Stopwatch.StartNew();

        var hierarchy = await StepAsync(steps, "Hierarchy", async () =>
        {
            var year = await _db.SchedulingAcademicYears.AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == scope.AcademicYearId && y.TenantId == _currentUser.TenantId, ct);
            var course = await _db.Courses.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == scope.CourseId && c.TenantId == _currentUser.TenantId, ct)
                ?? throw new InvalidOperationException("Course not found for allocation scope.");
            var group = await _db.Groups.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == scope.GroupId && g.TenantId == _currentUser.TenantId, ct);
            var semester = await _db.Semesters.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == scope.SemesterId && s.TenantId == _currentUser.TenantId, ct);
            string? programName = null;
            if (course.ProgramId is > 0)
            {
                programName = await _db.Programs.AsNoTracking()
                    .Where(p => p.Id == course.ProgramId)
                    .Select(p => p.ProgramName)
                    .FirstOrDefaultAsync(ct);
            }
            return new AllocationHierarchyProjection
            {
                AcademicYearId = scope.AcademicYearId,
                AcademicYearName = year?.Name,
                ProgramId = course.ProgramId,
                ProgramName = programName,
                CourseId = course.Id,
                CourseName = course.Name,
                GroupId = scope.GroupId,
                GroupName = group?.Name,
                SemesterId = scope.SemesterId,
                SemesterName = semester?.Name,
            };
        });

        var sectionEntities = await StepAsync(steps, "Sections", () =>
            _db.Sections.AsNoTracking()
                .Where(s => s.TenantId == _currentUser.TenantId
                            && s.AcademicYearId == scope.AcademicYearId
                            && s.CourseId == scope.CourseId
                            && s.GroupId == scope.GroupId
                            && s.SemesterId == scope.SemesterId)
                .OrderBy(s => s.DisplayOrder).ThenBy(s => s.SectionCode)
                .ToListAsync(ct));

        if (sectionEntities.Count == 0)
            warnings.Add("No sections found for allocation scope.");

        var capacities = await StepAsync(steps, "Capacity", async () =>
        {
            var ids = sectionEntities.Select(s => s.Id).ToList();
            return ids.Count == 0
                ? []
                : (await _capacity.GetOccupancyAsync(ids, cancellationToken: ct)).ToList();
        });

        var capacityProjections = capacities.Select(c => new AllocationCapacityProjection
        {
            SectionId = c.SectionId,
            MaximumCapacity = c.MaximumCapacity,
            MinimumCapacity = c.MinimumCapacity,
            RecommendedCapacity = c.RecommendedCapacity,
            CurrentStrength = c.CurrentStrength,
            AvailableCapacity = c.AvailableSeats,
            ReservedSeats = c.ReservedSeats,
            WaitingList = c.WaitingList,
            OccupancyPercent = c.OccupancyPercent,
            CapacityStatus = c.CapacityStatus,
        }).ToList();

        var sectionProjections = new List<AllocationSectionProjection>();
        await StepAsync(steps, "Readiness+Health+Policies", async () =>
        {
            foreach (var s in sectionEntities)
            {
                var ready = await _readiness.EvaluateAsync(s.Id, ct);
                var health = await _health.EvaluateAsync(s.Id, ct);
                var policy = await _policies.ResolveForSectionAsync(s.Id, ct);
                if (policy.Warnings.Count > 0) warnings.AddRange(policy.Warnings.Select(w => $"{s.SectionCode}: {w}"));
                sectionProjections.Add(new AllocationSectionProjection
                {
                    SectionId = s.Id,
                    SectionCode = s.SectionCode,
                    SectionName = s.SectionName,
                    SectionType = string.IsNullOrWhiteSpace(s.SectionTypeCode) ? SectionTypeCodes.Regular : s.SectionTypeCode,
                    Lifecycle = SectionLifecycleStates.Normalize(s.Status),
                    Health = health.OverallStatus,
                    Readiness = ready.OverallStatus,
                });
            }
            return true;
        });

        var students = await StepAsync(steps, "Students", async () =>
        {
            var sectionIds = sectionEntities.Select(s => s.Id).ToList();
            var studentRows = await _db.Students.AsNoTracking()
                .Where(st => st.TenantId == _currentUser.TenantId
                             && st.CourseId == scope.CourseId
                             && st.GroupId == scope.GroupId
                             && st.SemesterId == scope.SemesterId)
                .Select(st => new { st.Id, st.StudentNumber, st.Name })
                .ToListAsync(ct);
            var current = sectionIds.Count == 0
                ? new Dictionary<int, (int SectionId, string Code)>()
                : await (
                    from ss in _db.StudentSections.AsNoTracking()
                    join sec in _db.Sections.AsNoTracking() on ss.SectionId equals sec.Id
                    where ss.TenantId == _currentUser.TenantId && ss.IsCurrent && sectionIds.Contains(ss.SectionId)
                    select new { ss.StudentId, ss.SectionId, sec.SectionCode }
                ).ToDictionaryAsync(x => x.StudentId, x => (SectionId: x.SectionId, Code: x.SectionCode), ct);

            return studentRows.Select(st =>
            {
                current.TryGetValue(st.Id, out var cur);
                return new AllocationStudentProjection
                {
                    StudentId = st.Id,
                    StudentNumber = st.StudentNumber,
                    StudentName = st.Name,
                    CurrentSectionId = cur.SectionId == 0 ? null : cur.SectionId,
                    CurrentSectionCode = cur.Code,
                };
            }).ToList();
        });

        var faculty = await StepAsync(steps, "Faculty", async () =>
        {
            var sectionIds = sectionEntities.Select(s => s.Id).ToList();
            if (sectionIds.Count == 0) return new List<AllocationFacultyProjection>();
            var rows = await _db.FacultySectionAssignments.AsNoTracking()
                .Where(f => f.TenantId == _currentUser.TenantId && f.IsCurrent && sectionIds.Contains(f.SectionId))
                .ToListAsync(ct);
            var names = await _db.StaffMembers.AsNoTracking()
                .Where(st => rows.Select(r => r.FacultyId).Contains(st.Id))
                .ToDictionaryAsync(st => st.Id, st => $"{st.FirstName} {st.LastName}".Trim(), ct);
            return rows.Select(r => new AllocationFacultyProjection
            {
                FacultyId = r.FacultyId,
                FacultyName = names.GetValueOrDefault(r.FacultyId),
                SectionId = r.SectionId,
                Role = r.Role,
            }).ToList();
        });

        var subjects = await StepAsync(steps, "Subjects", async () =>
        {
            var rows = await (
                from s in _db.Subjects.AsNoTracking()
                join ts in _db.TenantSubjects.AsNoTracking() on s.TenantSubjectId equals ts.Id into tsg
                from ts in tsg.DefaultIfEmpty()
                where s.TenantId == _currentUser.TenantId
                      && s.CourseId == scope.CourseId
                      && s.SemesterId == scope.SemesterId
                select new AllocationSubjectProjection
                {
                    SubjectId = s.Id,
                    SubjectCode = ts != null ? ts.Code : null,
                    SubjectName = ts != null ? ts.Name : null,
                    CourseId = s.CourseId,
                    SemesterId = s.SemesterId,
                }).ToListAsync(ct);
            return rows;
        });

        var (rooms, timetableStatus) = await StepAsync(steps, "Timetable/Rooms", async () =>
        {
            var sectionIds = sectionEntities.Select(s => s.Id).ToList();
            var mapCount = sectionIds.Count == 0
                ? 0
                : await _db.TimetableSections.CountAsync(
                    t => t.TenantId == _currentUser.TenantId && sectionIds.Contains(t.SectionId), ct);
            var status = mapCount > 0 ? "Mapped" : "Unmapped";
            IReadOnlyList<AllocationRoomProjection> roomList =
            [
                new AllocationRoomProjection
                {
                    TimetableMappingCount = mapCount,
                    Status = status,
                    RoomCode = mapCount > 0 ? "ViaTimetable" : null,
                }
            ];
            return (roomList, status);
        });

        var recommendations = await StepAsync(steps, "Recommendations", async () =>
        {
            var recs = await _recommendations.RecommendAsync(scope.AcademicYearId, scope.SemesterId, ct);
            return recs.Where(r => sectionEntities.Any(s => s.Id == r.SectionId))
                .Select(r => $"{r.SectionCode}: {r.Recommendation} — {r.Rationale}")
                .ToList();
        });

        var policyLines = await StepAsync(steps, "PolicySummary", async () =>
        {
            var lines = new List<string>();
            foreach (var s in sectionEntities.Take(20))
            {
                var p = await _policies.ResolveForSectionAsync(s.Id, ct);
                lines.Add($"{s.SectionCode}: AllowMerge={p.AllowMerge}, AllowSplit={p.AllowSplit}, Max={p.MaximumCapacity?.ToString() ?? "-"}");
            }
            return lines;
        });

        var overallHealth = sectionProjections.Any(s => s.Health == "Critical") ? "Critical"
            : sectionProjections.Any(s => s.Health == "Warning") ? "Warning"
            : "Healthy";
        var overallReadiness = sectionProjections.Any(s => s.Readiness == "Blocked") ? "Blocked"
            : sectionProjections.Any(s => s.Readiness == "Warning") ? "Warning"
            : "Ready";

        var contextId = Guid.NewGuid();
        var generatedAt = DateTime.UtcNow;
        var draft = new SectionAllocationContext
        {
            ContextId = contextId,
            ContextVersion = "1",
            SchemaVersion = SectionAllocationContext.CurrentSchemaVersion,
            GeneratedAt = generatedAt,
            Checksum = "",
            Hierarchy = hierarchy,
            Sections = sectionProjections,
            Capacities = capacityProjections,
            Students = students,
            FacultyAssignments = faculty,
            SubjectAssignments = subjects,
            RoomAvailability = rooms,
            Policies = policyLines,
            Recommendations = recommendations,
            Metadata = new Dictionary<string, string>
            {
                ["TenantId"] = _currentUser.TenantId.ToString(),
                ["SectionCount"] = sectionProjections.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["StudentCount"] = students.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["ConstraintRegistry"] = string.Join(",", AllocationConstraintRegistry.All.Select(c => c.Code)),
            },
            OverallHealth = overallHealth,
            OverallReadiness = overallReadiness,
            TimetableStatus = timetableStatus,
        };

        var checksum = ComputeChecksum(draft);
        var context = new SectionAllocationContext
        {
            ContextId = draft.ContextId,
            ContextVersion = draft.ContextVersion,
            SchemaVersion = draft.SchemaVersion,
            GeneratedAt = draft.GeneratedAt,
            Checksum = checksum,
            Hierarchy = draft.Hierarchy,
            Sections = draft.Sections,
            Capacities = draft.Capacities,
            Students = draft.Students,
            FacultyAssignments = draft.FacultyAssignments,
            SubjectAssignments = draft.SubjectAssignments,
            RoomAvailability = draft.RoomAvailability,
            Policies = draft.Policies,
            Recommendations = draft.Recommendations,
            Metadata = draft.Metadata,
            OverallHealth = draft.OverallHealth,
            OverallReadiness = draft.OverallReadiness,
            TimetableStatus = draft.TimetableStatus,
        };

        total.Stop();
        _lastComposition = new AllocationContextCompositionReport
        {
            ContextId = contextId,
            GeneratedAt = generatedAt,
            Steps = steps,
            Warnings = warnings,
            TotalDurationMs = total.Elapsed.TotalMilliseconds,
        };
        return context;
    }

    private async Task<SectionAllocationAnalysisContext> BuildAnalysisCoreAsync(AllocationScopeRequest scope, CancellationToken ct)
    {
        var context = await BuildCoreAsync(scope, ct);
        var capacityHist = new List<AllocationHistoryPoint>();
        var versionHist = new List<AllocationHistoryPoint>();
        foreach (var s in context.Sections)
        {
            var ch = await _capacityHistory.GetCapacityHistoryAsync(s.SectionId, ct);
            capacityHist.AddRange(ch.Take(10).Select(h => new AllocationHistoryPoint
            {
                At = h.RecordedDate,
                Kind = "Capacity",
                Summary = $"{s.SectionCode}: {h.CurrentStrength}/{h.MaximumCapacity} ({h.OccupancyPercent}%)",
            }));
            var vh = await _versions.GetVersionsAsync(s.SectionId, ct);
            versionHist.AddRange(vh.Take(10).Select(v => new AllocationHistoryPoint
            {
                At = v.VersionDate,
                Kind = "Version",
                Summary = $"{s.SectionCode} v{v.VersionNumber} {v.Operation}",
            }));
        }

        var merges = (await _merge.GetHistoryAsync(ct))
            .Select(m => new AllocationHistoryPoint
            {
                At = DateTime.SpecifyKind(m.EffectiveDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                Kind = "Merge",
                Summary = $"Merge {string.Join("+", m.SourceSectionIds)}→{m.TargetSectionId} ({m.Status})",
            }).ToList();
        var splits = (await _split.GetHistoryAsync(ct))
            .Select(sp => new AllocationHistoryPoint
            {
                At = DateTime.SpecifyKind(sp.EffectiveDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                Kind = "Split",
                Summary = $"Split {sp.SourceSectionId}→[{string.Join(",", sp.ChildSectionIds)}] ({sp.Status})",
            }).ToList();

        var analytics = await _capacity.GetAnalyticsAsync(ct);
        var trend = analytics.CapacityTrend.Select(t => new AllocationOccupancyTrendPoint
        {
            Date = t.Date,
            AverageOccupancyPercent = t.AverageOccupancyPercent,
            TotalCurrentStrength = t.TotalCurrentStrength,
        }).ToList();

        return new SectionAllocationAnalysisContext
        {
            Context = context,
            CapacityHistory = capacityHist,
            LifecycleHistory = versionHist.Where(v => v.Summary.Contains("Lifecycle", StringComparison.OrdinalIgnoreCase)).ToList(),
            MergeHistory = merges,
            SplitHistory = splits,
            VersionHistory = versionHist,
            OccupancyTrend = trend,
            Forecast = ["Forecast deferred to AI29.1C / analytics consumers."],
            Recommendations = context.Recommendations,
            Analytics = new Dictionary<string, string>
            {
                ["UtilizationPercent"] = analytics.UtilizationPercent.ToString("0.##"),
                ["AverageOccupancyPercent"] = analytics.AverageOccupancyPercent.ToString("0.##"),
                ["MergeCandidates"] = analytics.MergeCandidates.Count.ToString(),
                ["SplitCandidates"] = analytics.SplitCandidates.Count.ToString(),
            },
        };
    }

    private static void EnsureScope(AllocationScopeRequest scope)
    {
        if (scope.AcademicYearId <= 0 || scope.CourseId <= 0 || scope.GroupId <= 0 || scope.SemesterId <= 0)
            throw new ArgumentException("AcademicYearId, CourseId, GroupId and SemesterId are required.");
    }

    private static async Task<T> StepAsync<T>(List<AllocationCompositionStep> steps, string name, Func<Task<T>> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await action();
            sw.Stop();
            steps.Add(new AllocationCompositionStep
            {
                Service = name,
                DurationMs = sw.Elapsed.TotalMilliseconds,
                Outcome = "Ok",
            });
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            steps.Add(new AllocationCompositionStep
            {
                Service = name,
                DurationMs = sw.Elapsed.TotalMilliseconds,
                Outcome = "Error",
                Detail = ex.Message,
            });
            throw;
        }
    }

    private static string ComputeChecksum(SectionAllocationContext ctx)
    {
        var payload = JsonSerializer.Serialize(new
        {
            ctx.ContextId,
            ctx.SchemaVersion,
            ctx.Hierarchy,
            Sections = ctx.Sections.Select(s => s.SectionId),
            Capacities = ctx.Capacities.Select(c => new { c.SectionId, c.CurrentStrength, c.MaximumCapacity }),
            StudentCount = ctx.Students.Count,
            FacultyCount = ctx.FacultyAssignments.Count,
        }, JsonOpts);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}
