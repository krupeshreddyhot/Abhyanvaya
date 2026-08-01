using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

public sealed class TimetableApprovalService : ITimetableApprovalService
{
    private const string CoordinatorRole = "Coordinator";
    private const string AdministratorRole = "Administrator";

    private readonly ITimetableApprovalRepository _repository;
    private readonly ITimetableApprovalCommentRepository _commentRepository;
    private readonly ITimetableDecisionHistoryRepository _decisionRepository;
    private readonly ITimetableRepository _timetableRepository;
    private readonly IScheduleVersionRepository _versionRepository;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<SubmitForReviewRequest> _submitValidator;
    private readonly IValidator<DecideApprovalStepRequest> _decideValidator;
    private readonly IValidator<AddApprovalCommentRequest> _commentValidator;

    public TimetableApprovalService(
        ITimetableApprovalRepository repository,
        ITimetableApprovalCommentRepository commentRepository,
        ITimetableDecisionHistoryRepository decisionRepository,
        ITimetableRepository timetableRepository,
        IScheduleVersionRepository versionRepository,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IValidator<SubmitForReviewRequest> submitValidator,
        IValidator<DecideApprovalStepRequest> decideValidator,
        IValidator<AddApprovalCommentRequest> commentValidator)
    {
        _repository = repository;
        _commentRepository = commentRepository;
        _decisionRepository = decisionRepository;
        _timetableRepository = timetableRepository;
        _versionRepository = versionRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _submitValidator = submitValidator;
        _decideValidator = decideValidator;
        _commentValidator = commentValidator;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<TimetableApprovalRequestDto> SubmitForReviewAsync(SubmitForReviewRequest request, CancellationToken cancellationToken = default)
    {
        await _submitValidator.ValidateAndThrowAsync(request, cancellationToken);
        var timetable = await _timetableRepository.GetByIdAsync(TenantId, request.TimetableId, cancellationToken)
            ?? throw new KeyNotFoundException($"Timetable {request.TimetableId} not found.");
        if (timetable.Status != TimetableStatus.Locked && timetable.Status != TimetableStatus.Draft)
            throw new DomainException("Only draft or locked timetables can be submitted for review.");
        if (!timetable.ScheduleVersionId.HasValue)
            throw new DomainException("Timetable must be linked to a schedule version before review.");

        if (await _repository.GetPendingByTimetableAsync(TenantId, timetable.Id, cancellationToken) is not null)
            throw new DomainException("An active approval request already exists for this timetable.");

        var version = await _versionRepository.GetByIdAsync(TenantId, timetable.ScheduleVersionId.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Schedule version {timetable.ScheduleVersionId} not found.");

        var approvalRequest = new TimetableApprovalRequest
        {
            TenantId = TenantId,
            ScheduleVersionId = version.Id,
            TimetableId = timetable.Id,
            Status = TimetableApprovalRequestStatus.InReview,
            SubmittedBy = _currentUser.UserId,
            SubmittedUtc = DateTime.UtcNow,
            CurrentStepOrder = 1
        };
        await _repository.AddAsync(approvalRequest, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        await _repository.AddStepAsync(new TimetableApprovalStep
        {
            TenantId = TenantId,
            RequestId = approvalRequest.Id,
            StepOrder = 1,
            RoleKey = CoordinatorRole,
            Status = TimetableApprovalRequestStatus.InReview
        }, cancellationToken);
        await _repository.AddStepAsync(new TimetableApprovalStep
        {
            TenantId = TenantId,
            RequestId = approvalRequest.Id,
            StepOrder = 2,
            RoleKey = AdministratorRole,
            Status = TimetableApprovalRequestStatus.Pending
        }, cancellationToken);
        await _repository.AddHistoryAsync(new TimetableApprovalHistory
        {
            TenantId = TenantId,
            RequestId = approvalRequest.Id,
            StepOrder = 0,
            ActorUserId = _currentUser.UserId,
            Comments = request.Comments?.Trim(),
            OldStatus = TimetableApprovalRequestStatus.Pending,
            NewStatus = TimetableApprovalRequestStatus.InReview,
            OccurredUtc = DateTime.UtcNow
        }, cancellationToken);

        await _decisionRepository.AddAsync(new TimetableDecisionHistory
        {
            TenantId = TenantId,
            RequestId = approvalRequest.Id,
            StepOrder = 0,
            ActorUserId = _currentUser.UserId,
            Action = "SubmitForReview",
            Comment = request.Comments?.Trim(),
            OldStatus = TimetableApprovalRequestStatus.Pending,
            NewStatus = TimetableApprovalRequestStatus.InReview,
            OccurredUtc = DateTime.UtcNow
        }, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Comments))
        {
            await _commentRepository.AddAsync(new TimetableApprovalComment
            {
                TenantId = TenantId,
                RequestId = approvalRequest.Id,
                ActorUserId = _currentUser.UserId,
                Comment = request.Comments.Trim(),
                OccurredUtc = DateTime.UtcNow,
                IsDecisionNote = false
            }, cancellationToken);
        }

        version.Status = ScheduleVersionStatus.UnderReview;
        timetable.Status = TimetableStatus.Locked;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        return await MapRequestAsync(approvalRequest.Id, cancellationToken);
    }

    public async Task<TimetableApprovalRequestDto> DecideStepAsync(DecideApprovalStepRequest request, CancellationToken cancellationToken = default)
    {
        await _decideValidator.ValidateAndThrowAsync(request, cancellationToken);
        var approvalRequest = await _repository.GetByIdAsync(TenantId, request.RequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Approval request {request.RequestId} not found.");
        if (approvalRequest.Status is TimetableApprovalRequestStatus.Approved or TimetableApprovalRequestStatus.Rejected or TimetableApprovalRequestStatus.Returned or TimetableApprovalRequestStatus.Cancelled)
            throw new DomainException("Approval request is already closed.");

        var step = approvalRequest.Steps.FirstOrDefault(s => s.StepOrder == request.StepOrder)
            ?? throw new DomainException($"Approval step {request.StepOrder} not found.");
        if (step.Status != TimetableApprovalRequestStatus.InReview && step.Status != TimetableApprovalRequestStatus.Pending)
            throw new DomainException("Approval step is not awaiting decision.");
        if (approvalRequest.CurrentStepOrder != request.StepOrder)
            throw new DomainException("Decision must be made on the current workflow step.");

        var commentText = request.Comments?.Trim() ?? request.DecisionNotes?.Trim();
        if (request.Decision is ApprovalDecision.Rejected or ApprovalDecision.Returned
            && string.IsNullOrWhiteSpace(commentText))
            throw new DomainException("Comment is required when rejecting or returning for changes.");

        var oldStatus = approvalRequest.Status;
        step.Decision = request.Decision;
        step.DecidedBy = _currentUser.UserId;
        step.DecidedUtc = DateTime.UtcNow;
        step.Comments = commentText;

        var version = await _versionRepository.GetByIdAsync(TenantId, approvalRequest.ScheduleVersionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Schedule version {approvalRequest.ScheduleVersionId} not found.");
        var timetable = await _timetableRepository.GetByIdAsync(TenantId, approvalRequest.TimetableId, cancellationToken)
            ?? throw new KeyNotFoundException($"Timetable {approvalRequest.TimetableId} not found.");

        switch (request.Decision)
        {
            case ApprovalDecision.Approved:
                step.Status = TimetableApprovalRequestStatus.Approved;
                var nextStep = approvalRequest.Steps.OrderBy(s => s.StepOrder).FirstOrDefault(s => s.StepOrder > request.StepOrder);
                if (nextStep is null)
                {
                    approvalRequest.Status = TimetableApprovalRequestStatus.Approved;
                    version.Status = ScheduleVersionStatus.Approved;
                }
                else
                {
                    approvalRequest.CurrentStepOrder = nextStep.StepOrder;
                    nextStep.Status = TimetableApprovalRequestStatus.InReview;
                    approvalRequest.Status = TimetableApprovalRequestStatus.InReview;
                }
                break;
            case ApprovalDecision.Rejected:
                step.Status = TimetableApprovalRequestStatus.Rejected;
                approvalRequest.Status = TimetableApprovalRequestStatus.Rejected;
                version.Status = ScheduleVersionStatus.Draft;
                timetable.Status = TimetableStatus.Draft;
                break;
            case ApprovalDecision.Returned:
                step.Status = TimetableApprovalRequestStatus.Returned;
                approvalRequest.Status = TimetableApprovalRequestStatus.Returned;
                version.Status = ScheduleVersionStatus.Draft;
                timetable.Status = TimetableStatus.Draft;
                break;
            default:
                throw new DomainException("Unsupported approval decision.");
        }

        await _repository.AddHistoryAsync(new TimetableApprovalHistory
        {
            TenantId = TenantId,
            RequestId = approvalRequest.Id,
            StepOrder = request.StepOrder,
            ActorUserId = _currentUser.UserId,
            Decision = request.Decision,
            Comments = commentText,
            OldStatus = oldStatus,
            NewStatus = approvalRequest.Status,
            OccurredUtc = DateTime.UtcNow
        }, cancellationToken);

        await _decisionRepository.AddAsync(new TimetableDecisionHistory
        {
            TenantId = TenantId,
            RequestId = approvalRequest.Id,
            StepOrder = request.StepOrder,
            ActorUserId = _currentUser.UserId,
            Decision = request.Decision,
            Action = request.Decision.ToString(),
            Comment = commentText,
            DecisionNotes = request.DecisionNotes?.Trim(),
            ReviewerRemarks = request.ReviewerRemarks?.Trim(),
            OldStatus = oldStatus,
            NewStatus = approvalRequest.Status,
            OccurredUtc = DateTime.UtcNow
        }, cancellationToken);

        if (!string.IsNullOrWhiteSpace(commentText) || !string.IsNullOrWhiteSpace(request.ReviewerRemarks))
        {
            await _commentRepository.AddAsync(new TimetableApprovalComment
            {
                TenantId = TenantId,
                RequestId = approvalRequest.Id,
                ActorUserId = _currentUser.UserId,
                Comment = string.Join(" | ", new[] { commentText, request.ReviewerRemarks?.Trim() }.Where(x => !string.IsNullOrWhiteSpace(x))),
                OccurredUtc = DateTime.UtcNow,
                IsDecisionNote = true
            }, cancellationToken);
        }

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return await MapRequestAsync(approvalRequest.Id, cancellationToken);
    }

    public async Task<ApprovalCommentDto> AddCommentAsync(AddApprovalCommentRequest request, CancellationToken cancellationToken = default)
    {
        await _commentValidator.ValidateAndThrowAsync(request, cancellationToken);
        _ = await _repository.GetByIdAsync(TenantId, request.RequestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Approval request {request.RequestId} not found.");

        var entity = new TimetableApprovalComment
        {
            TenantId = TenantId,
            RequestId = request.RequestId,
            ActorUserId = _currentUser.UserId,
            Comment = request.Comment.Trim(),
            OccurredUtc = DateTime.UtcNow,
            IsDecisionNote = request.IsDecisionNote
        };
        await _commentRepository.AddAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return new ApprovalCommentDto
        {
            Id = entity.Id,
            RequestId = entity.RequestId,
            ActorUserId = entity.ActorUserId,
            Comment = entity.Comment,
            OccurredUtc = entity.OccurredUtc,
            IsDecisionNote = entity.IsDecisionNote
        };
    }

    public async Task<IReadOnlyList<TimetableApprovalRequestDto>> ListQueueAsync(TimetableApprovalRequestStatus? status, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListQueueAsync(TenantId, status, cancellationToken);
        var dtos = new List<TimetableApprovalRequestDto>();
        foreach (var item in items)
            dtos.Add(await MapRequestAsync(item.Id, cancellationToken));
        return dtos;
    }

    public async Task<TimetableApprovalTimelineDto?> GetTimelineAsync(int requestId, CancellationToken cancellationToken = default)
    {
        var request = await _repository.GetByIdAsync(TenantId, requestId, cancellationToken);
        if (request is null) return null;
        var history = await _repository.ListHistoryAsync(TenantId, requestId, cancellationToken);
        var comments = await _commentRepository.ListByRequestAsync(TenantId, requestId, cancellationToken);
        var decisions = await _decisionRepository.ListByRequestAsync(TenantId, requestId, cancellationToken);
        return new TimetableApprovalTimelineDto
        {
            RequestId = requestId,
            Status = request.Status,
            Events = history.Select(h => new TimetableApprovalHistoryDto
            {
                StepOrder = h.StepOrder,
                ActorUserId = h.ActorUserId,
                Decision = h.Decision,
                Comments = h.Comments,
                OccurredUtc = h.OccurredUtc,
                OldStatus = h.OldStatus,
                NewStatus = h.NewStatus
            }).ToList(),
            Comments = comments.Select(c => new ApprovalCommentDto
            {
                Id = c.Id,
                RequestId = c.RequestId,
                ActorUserId = c.ActorUserId,
                Comment = c.Comment,
                OccurredUtc = c.OccurredUtc,
                IsDecisionNote = c.IsDecisionNote
            }).ToList(),
            Decisions = decisions.Select(d => new DecisionHistoryDto
            {
                Id = d.Id,
                RequestId = d.RequestId,
                StepOrder = d.StepOrder,
                ActorUserId = d.ActorUserId,
                Decision = d.Decision,
                Action = d.Action,
                Comment = d.Comment,
                DecisionNotes = d.DecisionNotes,
                ReviewerRemarks = d.ReviewerRemarks,
                OldStatus = d.OldStatus,
                NewStatus = d.NewStatus,
                OccurredUtc = d.OccurredUtc
            }).ToList()
        };
    }

    private async Task<TimetableApprovalRequestDto> MapRequestAsync(int requestId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(TenantId, requestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Approval request {requestId} not found.");
        var versionName = await _context.SchedulingScheduleVersions.Where(x => x.Id == entity.ScheduleVersionId).Select(x => x.VersionName).FirstOrDefaultAsync(cancellationToken);
        var timetableName = await _context.SchedulingTimetables.Where(x => x.Id == entity.TimetableId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
        return new TimetableApprovalRequestDto
        {
            Id = entity.Id,
            ScheduleVersionId = entity.ScheduleVersionId,
            VersionName = versionName,
            TimetableId = entity.TimetableId,
            TimetableName = timetableName,
            Status = entity.Status,
            SubmittedBy = entity.SubmittedBy,
            SubmittedUtc = entity.SubmittedUtc,
            CurrentStepOrder = entity.CurrentStepOrder,
            Steps = entity.Steps.OrderBy(s => s.StepOrder).Select(s => new TimetableApprovalStepDto
            {
                Id = s.Id,
                StepOrder = s.StepOrder,
                RoleKey = s.RoleKey,
                Status = s.Status,
                AssignedTo = s.AssignedTo,
                DecidedBy = s.DecidedBy,
                DecidedUtc = s.DecidedUtc,
                Decision = s.Decision,
                Comments = s.Comments
            }).ToList()
        };
    }
}
