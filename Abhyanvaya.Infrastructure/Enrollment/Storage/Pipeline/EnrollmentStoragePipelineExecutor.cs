using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage.Pipeline;

public sealed class EnrollmentStoragePipelineExecutor : IEnrollmentStoragePipelineExecutor
{
    private readonly IReadOnlyList<IEnrollmentStorageStep> _steps;
    private readonly RollbackStep _rollbackStep;
    private readonly IStorageMetricsCollector _metrics;

    public EnrollmentStoragePipelineExecutor(
        IEnumerable<IEnrollmentStorageStep> steps,
        RollbackStep rollbackStep,
        IStorageMetricsCollector metrics)
    {
        _steps = steps.OrderBy(s => s.Order).ThenBy(s => s.Name, StringComparer.Ordinal).ToList();
        _rollbackStep = rollbackStep;
        _metrics = metrics;
    }

    public IReadOnlyList<StorageStepMetadata> DescribePipeline()
    {
        var metadata = _steps.Select(s => s.ToMetadata()).ToList();
        metadata.Add(RollbackStep.Metadata);
        return metadata;
    }

    public async Task<EnrollmentStoragePipelineContext> ExecuteAsync(
        EnrollmentStoragePipelineContext context,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            foreach (var step in _steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (context.Failed)
                {
                    break;
                }

                await step.ExecuteAsync(context, cancellationToken);
            }

            if (context.Failed || context.PrimaryRecord is null || context.Manifest is null)
            {
                await _rollbackStep.ExecuteAsync(context, cancellationToken);
                context.FailureReason ??= "Primary aligned face artifact could not be stored.";
            }
        }
        catch (Exception)
        {
            await _rollbackStep.ExecuteAsync(context, cancellationToken);
            throw;
        }
        finally
        {
            sw.Stop();
            _metrics.RecordPipelineTime(sw.ElapsedMilliseconds, "EnrollmentStorage");
        }

        return context;
    }
}
