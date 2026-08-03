using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;
using DetectionRecommendation = Abhyanvaya.Application.Scheduling.Conflicts.ConflictRecommendation;
using AdvisoryRecommendation = Abhyanvaya.Application.Scheduling.Conflicts.Intelligence.ConflictRecommendation;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;

public interface IConflictIntelligenceService
{
    Task<ConflictGuidanceDto> GetGuidanceAsync(int? timetableEntryId, string ruleCode, int? academicYearId, int? timetableId, CancellationToken cancellationToken = default);
    Task<DependencyGraphDto> GetDependencyGraphAsync(int? academicYearId, int? timetableId, CancellationToken cancellationToken = default);
    Task<EnhancedConflictWorkspaceDto> GetEnhancedWorkspaceAsync(ConflictWorkspaceQuery query, CancellationToken cancellationToken = default);
    Task<ConflictWorkspacePinDto> PinAsync(UpsertConflictPinRequest request, CancellationToken cancellationToken = default);
    Task<ConflictWorkspaceNoteDto> AddNoteAsync(UpsertConflictNoteRequest request, CancellationToken cancellationToken = default);
    Task<ConflictWorkspaceBookmarkDto> SaveBookmarkAsync(UpsertConflictBookmarkRequest request, CancellationToken cancellationToken = default);
}

public sealed class ConflictIntelligenceService : IConflictIntelligenceService
{
    private readonly IConflictDetectionService _detection;
    private readonly ConflictAnalyzer _analyzer;
    private readonly IConflictResolutionAdvisor _advisor;
    private readonly IImpactAnalyzer _impactAnalyzer;
    private readonly IConflictDependencyAnalyzer _dependencyAnalyzer;
    private readonly IConflictExplainabilityService _explainability;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public ConflictIntelligenceService(
        IConflictDetectionService detection,
        ConflictAnalyzer analyzer,
        IConflictResolutionAdvisor advisor,
        IImpactAnalyzer impactAnalyzer,
        IConflictDependencyAnalyzer dependencyAnalyzer,
        IConflictExplainabilityService explainability,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IUnitOfWork uow)
    {
        _detection = detection;
        _analyzer = analyzer;
        _advisor = advisor;
        _impactAnalyzer = impactAnalyzer;
        _dependencyAnalyzer = dependencyAnalyzer;
        _explainability = explainability;
        _db = db;
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<ConflictGuidanceDto> GetGuidanceAsync(
        int? timetableEntryId,
        string ruleCode,
        int? academicYearId,
        int? timetableId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _detection.GetWorkspaceAsync(new ConflictWorkspaceQuery
        {
            AcademicYearId = academicYearId,
            TimetableId = timetableId,
            UseLatestRun = true
        }, cancellationToken);

        var conflictDto = workspace.Conflicts.FirstOrDefault(c =>
            c.RuleCode.Equals(ruleCode, StringComparison.OrdinalIgnoreCase) &&
            (!timetableEntryId.HasValue || c.TimetableEntryId == timetableEntryId));

        if (conflictDto is null)
            throw new KeyNotFoundException("Conflict not found in latest detection run.");

        var ay = academicYearId ?? workspace.Summary.AcademicYearId;
        var (context, _) = await _analyzer.AnalyzeAsync(_currentUser.TenantId, ay, timetableId ?? workspace.Summary.TimetableId, workspace.Summary.DepartmentId, cancellationToken);
        var conflict = ToDomain(conflictDto);
        var advice = await _advisor.AdviseAsync(conflict, context, cancellationToken);
        var impact = await _impactAnalyzer.AnalyzeAsync(conflict, context, cancellationToken);
        var explanation = _explainability.Explain(conflict, impact);

        return new ConflictGuidanceDto
        {
            Conflict = conflictDto,
            SuggestedResolutions = advice.Recommendations.Select(MapResolution).ToList(),
            Explanation = MapExplanation(explanation),
            Impact = MapImpact(impact)
        };
    }

    public async Task<DependencyGraphDto> GetDependencyGraphAsync(int? academicYearId, int? timetableId, CancellationToken cancellationToken = default)
    {
        var workspace = await _detection.GetWorkspaceAsync(new ConflictWorkspaceQuery
        {
            AcademicYearId = academicYearId,
            TimetableId = timetableId,
            UseLatestRun = true
        }, cancellationToken);

        var graph = _dependencyAnalyzer.Analyze(workspace.Conflicts.Select(ToDomain).ToList());
        return MapDependency(graph);
    }

    public async Task<EnhancedConflictWorkspaceDto> GetEnhancedWorkspaceAsync(ConflictWorkspaceQuery query, CancellationToken cancellationToken = default)
    {
        var workspace = await _detection.GetWorkspaceAsync(query, cancellationToken);
        var conflicts = workspace.Conflicts;
        var graph = _dependencyAnalyzer.Analyze(conflicts.Select(ToDomain).ToList());

        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.UserId;
        var pins = await _db.SchedulingConflictWorkspacePins
            .Where(p => p.TenantId == tenantId && p.UserId == userId && !p.IsDeleted)
            .AsNoTracking()
            .Select(p => new ConflictWorkspacePinDto
            {
                Id = p.Id,
                ConflictDetectionRunId = p.ConflictDetectionRunId,
                RuleCode = p.RuleCode,
                TimetableEntryId = p.TimetableEntryId
            }).ToListAsync(cancellationToken);

        var bookmarks = await _db.SchedulingConflictWorkspaceBookmarks
            .Where(b => b.TenantId == tenantId && b.UserId == userId && !b.IsDeleted)
            .AsNoTracking()
            .Select(b => new ConflictWorkspaceBookmarkDto { Id = b.Id, Name = b.Name, FilterJson = b.FilterJson })
            .ToListAsync(cancellationToken);

        var notes = await _db.SchedulingConflictWorkspaceNotes
            .Where(n => n.TenantId == tenantId && n.UserId == userId && !n.IsDeleted)
            .AsNoTracking()
            .Select(n => new ConflictWorkspaceNoteDto
            {
                Id = n.Id,
                ConflictDetectionRunId = n.ConflictDetectionRunId,
                RuleCode = n.RuleCode,
                TimetableEntryId = n.TimetableEntryId,
                NoteText = n.NoteText,
                UserId = n.UserId
            }).ToListAsync(cancellationToken);

        return new EnhancedConflictWorkspaceDto
        {
            Workspace = workspace,
            GroupedByRule = conflicts.GroupBy(c => c.RuleName).ToDictionary(g => g.Key, g => (IReadOnlyList<ConflictResultDto>)g.ToList()),
            GroupedByDepartment = conflicts.GroupBy(c => c.DepartmentId?.ToString() ?? "None").ToDictionary(g => g.Key, g => (IReadOnlyList<ConflictResultDto>)g.ToList()),
            GroupedByFaculty = conflicts.GroupBy(c => c.StaffName ?? c.StaffId?.ToString() ?? "None").ToDictionary(g => g.Key, g => (IReadOnlyList<ConflictResultDto>)g.ToList()),
            GroupedBySeverity = conflicts.GroupBy(c => c.Severity.ToString()).ToDictionary(g => g.Key, g => (IReadOnlyList<ConflictResultDto>)g.ToList()),
            GroupedByRoom = conflicts.GroupBy(c => c.RoomName ?? c.RoomId?.ToString() ?? "None").ToDictionary(g => g.Key, g => (IReadOnlyList<ConflictResultDto>)g.ToList()),
            Pins = pins,
            Bookmarks = bookmarks,
            Notes = notes,
            DependencyGraph = MapDependency(graph)
        };
    }

    public async Task<ConflictWorkspacePinDto> PinAsync(UpsertConflictPinRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Domain.Entities.Scheduling.ConflictWorkspacePin
        {
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            ConflictDetectionRunId = request.ConflictDetectionRunId,
            RuleCode = request.RuleCode,
            TimetableEntryId = request.TimetableEntryId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };
        await _db.AddAsync(entity);
        await _uow.SaveChangesAsync(cancellationToken);
        return new ConflictWorkspacePinDto
        {
            Id = entity.Id,
            ConflictDetectionRunId = entity.ConflictDetectionRunId,
            RuleCode = entity.RuleCode,
            TimetableEntryId = entity.TimetableEntryId
        };
    }

    public async Task<ConflictWorkspaceNoteDto> AddNoteAsync(UpsertConflictNoteRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Domain.Entities.Scheduling.ConflictWorkspaceNote
        {
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            ConflictDetectionRunId = request.ConflictDetectionRunId,
            RuleCode = request.RuleCode,
            TimetableEntryId = request.TimetableEntryId,
            NoteText = request.NoteText,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };
        await _db.AddAsync(entity);
        await _uow.SaveChangesAsync(cancellationToken);
        return new ConflictWorkspaceNoteDto
        {
            Id = entity.Id,
            ConflictDetectionRunId = entity.ConflictDetectionRunId,
            RuleCode = entity.RuleCode,
            TimetableEntryId = entity.TimetableEntryId,
            NoteText = entity.NoteText,
            UserId = entity.UserId
        };
    }

    public async Task<ConflictWorkspaceBookmarkDto> SaveBookmarkAsync(UpsertConflictBookmarkRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Domain.Entities.Scheduling.ConflictWorkspaceBookmark
        {
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            Name = request.Name,
            FilterJson = request.FilterJson,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };
        await _db.AddAsync(entity);
        await _uow.SaveChangesAsync(cancellationToken);
        return new ConflictWorkspaceBookmarkDto { Id = entity.Id, Name = entity.Name, FilterJson = entity.FilterJson };
    }

    private static ConflictResult ToDomain(ConflictResultDto dto) => new()
    {
        RuleCode = dto.RuleCode,
        RuleName = dto.RuleName,
        Category = dto.Category,
        Severity = dto.Severity,
        Description = dto.Description,
        WhyOccurred = dto.WhyOccurred,
        Recommendation = new DetectionRecommendation
        {
            SuggestedResolution = dto.Recommendation.SuggestedResolution,
            NavigationPath = dto.Recommendation.NavigationPath,
            TimetableId = dto.Recommendation.TimetableId ?? dto.TimetableId,
            TimetableEntryId = dto.Recommendation.TimetableEntryId ?? dto.TimetableEntryId,
            DayOfWeek = dto.Recommendation.DayOfWeek ?? dto.DayOfWeek,
            TimeSlotId = dto.Recommendation.TimeSlotId ?? dto.TimeSlotId
        },
        TimetableId = dto.TimetableId,
        TimetableEntryId = dto.TimetableEntryId,
        RelatedEntryId = dto.RelatedEntryId,
        DayOfWeek = dto.DayOfWeek,
        TimeSlotId = dto.TimeSlotId,
        StaffId = dto.StaffId,
        RoomId = dto.RoomId,
        DepartmentId = dto.DepartmentId,
        CourseId = dto.CourseId,
        GroupId = dto.GroupId,
        SemesterId = dto.SemesterId,
        SubjectId = dto.SubjectId
    };

    private static ConflictResolutionDto MapResolution(AdvisoryRecommendation r) => new()
    {
        RecommendationId = r.RecommendationId,
        Title = r.Title,
        Summary = r.Summary,
        ProviderCode = r.ProviderCode,
        Options = r.Options.Select(o => new ResolutionOptionDto
        {
            OptionCode = o.OptionCode,
            Label = o.Label,
            Description = o.Description,
            ActionHint = o.ActionHint,
            SuggestedRoomId = o.SuggestedRoomId,
            SuggestedStaffId = o.SuggestedStaffId,
            SuggestedTimeSlotId = o.SuggestedTimeSlotId,
            SuggestedDayOfWeek = o.SuggestedDayOfWeek,
            NavigationPath = o.NavigationPath
        }).ToList(),
        Score = new ResolutionScoreDto
        {
            Confidence = r.Score.Confidence,
            Impact = r.Score.Impact,
            Difficulty = r.Score.Difficulty,
            Rank = r.Score.Rank
        },
        Reasons = r.Reasons.Select(x => new ResolutionReasonDto { Code = x.Code, Message = x.Message }).ToList(),
        EstimatedResolution = r.EstimatedResolution,
        NavigationPath = r.NavigationPath,
        IsAdvisoryOnly = true,
        ModifiesTimetable = false
    };

    private static ConflictExplanationDto MapExplanation(ConflictExplanation e) => new()
    {
        RuleCode = e.RuleCode,
        RuleName = e.RuleName,
        RuleCategory = e.RuleCategory,
        RuleDescription = e.RuleDescription,
        BusinessReason = e.BusinessReason,
        Severity = e.Severity,
        Priority = e.Priority,
        WhyTriggered = e.WhyTriggered,
        SuggestedAction = e.SuggestedAction,
        Impact = e.Impact,
        References = e.References,
        NavigationPath = e.NavigationPath,
        TimetableId = e.TimetableId,
        TimetableEntryId = e.TimetableEntryId
    };

    private static ImpactGraphDto MapImpact(ImpactGraph g) => new()
    {
        Summary = new ImpactSummaryDto
        {
            FacultyAffected = g.Summary.FacultyAffected,
            StudentsAffected = g.Summary.StudentsAffected,
            RoomsAffected = g.Summary.RoomsAffected,
            DepartmentsAffected = g.Summary.DepartmentsAffected,
            PublishedVersionsAffected = g.Summary.PublishedVersionsAffected,
            WorkloadSignals = g.Summary.WorkloadSignals,
            AvailabilitySignals = g.Summary.AvailabilitySignals,
            AttendanceSignals = g.Summary.AttendanceSignals,
            MaxSeverity = g.Summary.MaxSeverity,
            RiskLevel = g.Summary.RiskLevel
        },
        Nodes = g.Nodes.Select(n => new ImpactNodeDto
        {
            NodeId = n.NodeId,
            Category = n.Category,
            Label = n.Label,
            EntityId = n.EntityId,
            Severity = n.Severity,
            Detail = n.Detail
        }).ToList(),
        Edges = g.Edges.Select(e => new ImpactEdgeDto
        {
            FromNodeId = e.FromNodeId,
            ToNodeId = e.ToNodeId,
            Relation = e.Relation
        }).ToList(),
        NavigationPath = g.NavigationPath,
        IsAdvisoryOnly = true
    };

    private static DependencyGraphDto MapDependency(DependencyGraph g) => new()
    {
        NodeCount = g.Summary.NodeCount,
        EdgeCount = g.Summary.EdgeCount,
        ClusterCount = g.Summary.ClusterCount,
        RootConflictCount = g.Summary.RootConflictCount,
        Nodes = g.Nodes.Select(n => new DependencyNodeDto
        {
            NodeId = n.NodeId,
            RuleCode = n.RuleCode,
            Label = n.Label,
            Severity = n.Severity,
            TimetableEntryId = n.TimetableEntryId,
            RelatedEntryId = n.RelatedEntryId,
            NavigationPath = n.NavigationPath,
            ClusterKey = n.ClusterKey
        }).ToList(),
        Edges = g.Edges.Select(e => new DependencyEdgeDto
        {
            FromNodeId = e.FromNodeId,
            ToNodeId = e.ToNodeId,
            Relation = e.Relation,
            Reason = e.Reason
        }).ToList(),
        Mermaid = g.Mermaid,
        Clusters = g.Clusters
    };
}
