using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.StudentFaceEmbedding;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application;

/// <summary>
/// CRUD and queue orchestration for <see cref="StudentFaceEmbedding"/> rows.
/// </summary>
public sealed class StudentFaceEmbeddingService : IStudentFaceEmbeddingService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IStudentPhotoEmbeddingQueue _queue;
    private readonly ILogger<StudentFaceEmbeddingService> _logger;

    public StudentFaceEmbeddingService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IStudentPhotoEmbeddingQueue queue,
        ILogger<StudentFaceEmbeddingService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _queue = queue;
        _logger = logger;
    }

    public async Task<StudentFaceEmbeddingStatusDto> GetStatusAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var student = await GetStudentOrThrowAsync(studentId, cancellationToken);
        return await BuildStatusAsync(student, cancellationToken);
    }

    public async Task<IReadOnlyList<StudentFaceEmbeddingDto>> ListAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        await GetStudentOrThrowAsync(studentId, cancellationToken);

        var rows = await _context.StudentFaceEmbeddings
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.GeneratedUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(EmbeddingStorageMapper.MapToDto).ToList();
    }

    public async Task<StudentFaceEmbeddingStatusDto> RequestGenerateAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var student = await GetStudentOrThrowAsync(studentId, cancellationToken);
        EnsurePhotoAvailable(student);

        await EnqueueAsync(student, regenerate: false, cancellationToken);
        return await BuildStatusAsync(student, cancellationToken);
    }

    public async Task<StudentFaceEmbeddingStatusDto> RequestRegenerateAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var student = await GetStudentOrThrowAsync(studentId, cancellationToken);
        EnsurePhotoAvailable(student);

        await EnqueueAsync(student, regenerate: true, cancellationToken);
        return await BuildStatusAsync(student, cancellationToken);
    }

    public async Task<StudentFaceEmbeddingDto> DeactivateAsync(
        int studentId,
        Guid embeddingId,
        CancellationToken cancellationToken = default)
    {
        await GetStudentOrThrowAsync(studentId, cancellationToken);

        var embedding = await _context.StudentFaceEmbeddings
            .FirstOrDefaultAsync(e => e.Id == embeddingId && e.StudentId == studentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Embedding '{embeddingId}' was not found for this student.");

        embedding.IsActive = false;
        embedding.EmbeddingStatus = EmbeddingStatus.Inactive;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_context, cancellationToken);

        _logger.LogInformation(
            "Face embedding deactivated. StudentId={StudentId} EmbeddingId={EmbeddingId} TenantId={TenantId}",
            studentId,
            embeddingId,
            embedding.TenantId);

        return EmbeddingStorageMapper.MapToDto(embedding);
    }

    private async Task EnqueueAsync(Student student, bool regenerate, CancellationToken cancellationToken)
    {
        var message = new StudentPhotoUploadedMessage(
            student.TenantId,
            student.Id,
            student.PhotoKey!,
            _currentUser.UserId > 0 ? _currentUser.UserId : null,
            DateTime.UtcNow,
            regenerate);

        await _queue.EnqueueAsync(message, cancellationToken);

        _logger.LogInformation(
            "Face embedding job enqueued. StudentId={StudentId} TenantId={TenantId} Regenerate={Regenerate} QueueDepth={QueueDepth}",
            student.Id,
            student.TenantId,
            regenerate,
            _queue.Count);
    }

    private async Task<StudentFaceEmbeddingStatusDto> BuildStatusAsync(
        Student student,
        CancellationToken cancellationToken)
    {
        var embeddings = await _context.StudentFaceEmbeddings
            .AsNoTracking()
            .Where(e => e.StudentId == student.Id)
            .OrderByDescending(e => e.GeneratedUtc)
            .ToListAsync(cancellationToken);

        var active = embeddings.FirstOrDefault(e => e.IsActive);
        var currentPhotoVersion = student.PhotoUploadedUtc?.Ticks ?? 0L;

        return new StudentFaceEmbeddingStatusDto
        {
            StudentId = student.Id,
            HasPhoto = !string.IsNullOrWhiteSpace(student.PhotoKey),
            HasActiveEmbedding = active != null,
            ActiveStatus = active?.EmbeddingStatus,
            ActiveQuality = active?.EmbeddingQuality,
            ActiveModel = active?.EmbeddingModel,
            ActiveVersion = active?.EmbeddingVersion,
            ActiveDimension = active?.EmbeddingDimension,
            ActivePhotoVersion = active?.PhotoVersion,
            CurrentPhotoVersion = currentPhotoVersion,
            IsPhotoVersionStale = active != null && active.PhotoVersion != currentPhotoVersion,
            GeneratedUtc = active?.GeneratedUtc,
            GenerationPending = _queue.IsPending(student.Id),
            TotalEmbeddings = embeddings.Count,
            RetryCount = active?.RetryCount ?? embeddings.FirstOrDefault()?.RetryCount ?? 0,
            ActiveEmbeddingId = active?.Id
        };
    }

    private async Task<Student> GetStudentOrThrowAsync(int studentId, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Student '{studentId}' was not found.");

        TenantAccessGuard.EnsureTenantAccess(_currentUser, student.TenantId);
        return student;
    }

    private static void EnsurePhotoAvailable(Student student)
    {
        if (string.IsNullOrWhiteSpace(student.PhotoKey))
        {
            throw new InvalidOperationException("Student photo is required before generating a face embedding.");
        }
    }
}
