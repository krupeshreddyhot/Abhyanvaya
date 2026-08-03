using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Faculty;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Faculty;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using ClosedXML.Excel;

namespace Abhyanvaya.Application.Scheduling;

public sealed class TimetableChangeHistoryService : ITimetableChangeHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly ITimetableChangeHistoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IFacultyScheduleNotifier _facultyNotifier;

    public TimetableChangeHistoryService(
        ITimetableChangeHistoryRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IFacultyScheduleNotifier facultyNotifier)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _facultyNotifier = facultyNotifier;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task RecordAsync(int timetableId, TimetableChangeOperation operation, int? entryId, object? oldValue, object? newValue, string? reason, CancellationToken cancellationToken = default)
    {
        var entity = new TimetableChangeHistory
        {
            TenantId = TenantId,
            TimetableId = timetableId,
            EntryId = entryId,
            UserId = _currentUser.UserId,
            OccurredUtc = DateTime.UtcNow,
            Operation = operation,
            OldValueJson = oldValue is null ? null : JsonSerializer.Serialize(oldValue, JsonOptions),
            NewValueJson = newValue is null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
            Reason = reason?.Trim()
        };
        await _repository.AddAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        // AI31 — push schedule change to faculty workspace (SignalR, no polling)
        await _facultyNotifier.PublishAsync(
            TenantId,
            _currentUser.StaffId > 0 ? _currentUser.StaffId : null,
            new FacultyScheduleNotificationDto
            {
                NotificationId = $"chg-{entity.Id}",
                Kind = operation switch
                {
                    TimetableChangeOperation.Delete => "Cancelled",
                    TimetableChangeOperation.Move => "Rescheduled",
                    TimetableChangeOperation.Update => "Rescheduled",
                    _ => "ScheduleChange"
                },
                Title = operation.ToString(),
                Message = string.IsNullOrWhiteSpace(reason) ? $"Timetable {operation} recorded." : reason!,
                OccurredUtc = entity.OccurredUtc,
                TimetableId = timetableId,
                EntryId = entryId
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<TimetableChangeHistoryDto>> ListAsync(TimetableChangeHistoryFilter filter, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListAsync(TenantId, filter, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<byte[]> ExportExcelAsync(TimetableChangeHistoryFilter filter, CancellationToken cancellationToken = default)
    {
        var items = await ListAsync(filter, cancellationToken);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ChangeHistory");
        sheet.Cell(1, 1).Value = "Id";
        sheet.Cell(1, 2).Value = "TimetableId";
        sheet.Cell(1, 3).Value = "EntryId";
        sheet.Cell(1, 4).Value = "UserId";
        sheet.Cell(1, 5).Value = "OccurredUtc";
        sheet.Cell(1, 6).Value = "Operation";
        sheet.Cell(1, 7).Value = "Reason";

        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.Id;
            sheet.Cell(row, 2).Value = item.TimetableId;
            sheet.Cell(row, 3).Value = item.EntryId;
            sheet.Cell(row, 4).Value = item.UserId;
            sheet.Cell(row, 5).Value = item.OccurredUtc;
            sheet.Cell(row, 6).Value = item.Operation.ToString();
            sheet.Cell(row, 7).Value = item.Reason;
            row++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static TimetableChangeHistoryDto Map(TimetableChangeHistory entity) => new()
    {
        Id = entity.Id,
        TimetableId = entity.TimetableId,
        EntryId = entity.EntryId,
        UserId = entity.UserId,
        OccurredUtc = entity.OccurredUtc,
        Operation = entity.Operation,
        OldValueJson = entity.OldValueJson,
        NewValueJson = entity.NewValueJson,
        Reason = entity.Reason
    };
}
