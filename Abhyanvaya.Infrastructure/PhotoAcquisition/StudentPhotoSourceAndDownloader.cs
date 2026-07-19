using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.PhotoAcquisition;
using Abhyanvaya.Domain.Constants;

namespace Abhyanvaya.Infrastructure.PhotoAcquisition;

public sealed class StudentPhotoSourceAdapter : IStudentPhotoSource
{
    private readonly IStudentPhotoProviderFactory _providerFactory;

    public StudentPhotoSourceAdapter(IStudentPhotoProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public Task<PhotoSourceResolution> ResolveAsync(
        PhotoAcquisitionStudentMaster student,
        CancellationToken cancellationToken = default)
    {
        var providerName = string.IsNullOrWhiteSpace(student.PreferredProviderName)
            ? _providerFactory.GetDefaultProvider().ProviderName
            : student.PreferredProviderName;

        _providerFactory.GetProvider(providerName);

        var sourceReference = BuildSourceReference(providerName, student);
        return Task.FromResult(new PhotoSourceResolution
        {
            ProviderName = providerName,
            Student = student,
            SourceReference = sourceReference,
        });
    }

    private static string BuildSourceReference(string providerName, PhotoAcquisitionStudentMaster student)
    {
        return providerName switch
        {
            StudentPhotoProviders.ExamBranch =>
                $"exambranch://{student.CollegeCode}/{student.AcademicYear}/{student.StudentNumber}",
            _ => $"{providerName}://tenant/{student.TenantId}/student/{student.StudentId}",
        };
    }
}

public sealed class StudentPhotoDownloader : IStudentPhotoDownloader
{
    private readonly IStudentPhotoProviderFactory _providerFactory;
    private readonly PhotoAcquisitionOptions _options;

    public StudentPhotoDownloader(
        IStudentPhotoProviderFactory providerFactory,
        Microsoft.Extensions.Options.IOptions<PhotoAcquisitionOptions> options)
    {
        _providerFactory = providerFactory;
        _options = options.Value;
    }

    public async Task<PhotoDownloadResult> DownloadAsync(
        PhotoSourceResolution source,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.DownloadTimeoutSeconds)));

        try
        {
            var provider = _providerFactory.GetProvider(source.ProviderName);
            var fetchResult = await provider.FetchPhotoAsync(
                new StudentPhotoFetchRequest(
                    source.Student.TenantId,
                    source.Student.StudentId,
                    source.Student.StudentNumber,
                    source.Student.CollegeCode,
                    source.Student.AcademicYear),
                timeoutCts.Token);

            if (!fetchResult.Success || fetchResult.PhotoBytes is not { Length: > 0 })
            {
                return new PhotoDownloadResult
                {
                    Success = false,
                    SourceReference = fetchResult.SourceReference,
                    FailureReason = fetchResult.FailureReason ?? "Download failed.",
                    IsRetryable = fetchResult.FailureCategory is null,
                };
            }

            return new PhotoDownloadResult
            {
                Success = true,
                PhotoBytes = fetchResult.PhotoBytes,
                ContentType = fetchResult.ContentType,
                SourceReference = fetchResult.SourceReference,
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new PhotoDownloadResult
            {
                Success = false,
                SourceReference = source.SourceReference,
                FailureReason = "Download timed out.",
                IsRetryable = true,
            };
        }
    }
}
