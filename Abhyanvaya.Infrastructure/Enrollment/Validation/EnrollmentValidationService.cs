using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation;

public sealed class EnrollmentValidationService : IEnrollmentValidationService
{
    private readonly IEnrollmentValidationPolicy _policy;
    private readonly IEnrollmentValidationRuleRegistry _ruleRegistry;
    private readonly EnrollmentValidationPipelineExecutor _pipelineExecutor;
    private readonly IEnrollmentFaceAnalysisService _faceAnalysisService;
    private readonly TimeProvider _clock;
    private readonly ILogger<EnrollmentValidationService> _logger;

    public EnrollmentValidationService(
        IEnrollmentValidationPolicy policy,
        IEnrollmentValidationRuleRegistry ruleRegistry,
        IEnrollmentFaceAnalysisService faceAnalysisService,
        TimeProvider clock,
        ILogger<EnrollmentValidationService> logger)
    {
        _policy = policy;
        _ruleRegistry = ruleRegistry;
        _faceAnalysisService = faceAnalysisService;
        _clock = clock;
        _logger = logger;
        _pipelineExecutor = new EnrollmentValidationPipelineExecutor(ruleRegistry);
    }

    public async Task<EnrollmentValidationResult> ValidateAsync(
        EnrollmentValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var startedUtc = _clock.GetUtcNow();
        var ctx = request.ExecutionContext;

        _logger.LogInformation(
            "Enrollment validation started. StudentId={StudentId} BatchId={BatchId} CorrelationId={CorrelationId} ExecutionTraceId={ExecutionTraceId} PipelineVersion={PipelineVersion}",
            request.StudentId,
            request.BatchId,
            ctx.CorrelationId,
            ctx.ExecutionTraceId,
            ctx.PipelineVersion);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var policyDecision = await _policy.ResolveAsync(new EnrollmentValidationPolicyRequest
            {
                TenantId = ctx.TenantId,
                RequestedProfile = request.ValidationProfile,
            }, cancellationToken);

            var accessor = new EnrollmentFaceAnalysisAccessor(
                request,
                policyDecision.Thresholds,
                _faceAnalysisService);

            var ruleContext = new EnrollmentValidationRuleContext
            {
                Request = request,
                Policy = policyDecision,
                Thresholds = policyDecision.Thresholds,
                AnalysisAccessor = accessor,
            };

            var ruleResults = await _pipelineExecutor.ExecuteAsync(ruleContext, cancellationToken);
            var (failureCategory, failureReason, diagnosticCode, validationPassed) =
                EnrollmentValidationPipelineExecutor.ResolveEligibility(ruleResults);

            stopwatch.Stop();

            var telemetry = BuildTelemetry(stopwatch.ElapsedMilliseconds, request, ruleResults, accessor);
            var report = EnrollmentValidationReportAggregator.BuildReport(
                ruleResults,
                ruleContext,
                validationPassed,
                telemetry);

            var diagnosticImages = await ValidationDiagnosticImageBuilder.BuildOptionalAsync(
                accessor,
                cancellationToken);

            var artifact = ValidationDiagnosticImageBuilder.BuildArtifact(
                report,
                request,
                accessor,
                startedUtc,
                telemetry,
                diagnosticImages);

            if (validationPassed)
            {
                _logger.LogInformation(
                    "Enrollment validation completed. StudentId={StudentId} DurationMs={DurationMs} CorrelationId={CorrelationId} ExecutionTraceId={ExecutionTraceId} PipelineVersion={PipelineVersion} CompositeScore={CompositeScore}",
                    request.StudentId,
                    stopwatch.ElapsedMilliseconds,
                    ctx.CorrelationId,
                    ctx.ExecutionTraceId,
                    ctx.PipelineVersion,
                    report.CompositeScore);
            }
            else
            {
                _logger.LogWarning(
                    "Enrollment validation failed. StudentId={StudentId} DurationMs={DurationMs} CorrelationId={CorrelationId} ExecutionTraceId={ExecutionTraceId} PipelineVersion={PipelineVersion} FailureCategory={FailureCategory} DiagnosticCode={DiagnosticCode}",
                    request.StudentId,
                    stopwatch.ElapsedMilliseconds,
                    ctx.CorrelationId,
                    ctx.ExecutionTraceId,
                    ctx.PipelineVersion,
                    failureCategory,
                    diagnosticCode);
            }

            return new EnrollmentValidationResult
            {
                ValidationPassed = validationPassed,
                Report = report,
                FailureCategory = failureCategory,
                FailureReason = failureReason,
                DiagnosticCode = diagnosticCode,
                Telemetry = telemetry,
                Duration = stopwatch.Elapsed,
                AlignedFaceBytes = validationPassed ? artifact.AlignedFaceImage : null,
                Artifact = artifact,
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "Enrollment validation cancelled. StudentId={StudentId} DurationMs={DurationMs} CorrelationId={CorrelationId} ExecutionTraceId={ExecutionTraceId}",
                request.StudentId,
                stopwatch.ElapsedMilliseconds,
                ctx.CorrelationId,
                ctx.ExecutionTraceId);
            throw;
        }
    }

    private EnrollmentValidationTelemetry BuildTelemetry(
        long elapsedMs,
        EnrollmentValidationRequest request,
        IReadOnlyList<EnrollmentValidationRuleResult> ruleResults,
        EnrollmentFaceAnalysisAccessor accessor) =>
        new()
        {
            ElapsedMilliseconds = elapsedMs,
            Engine = _faceAnalysisService.ProviderName,
            Model = _faceAnalysisService.ModelName,
            ImageSizeBytes = request.ImageMetadata.ByteSize,
            RulesExecuted = ruleResults.Count(r => r.Severity is ValidationRuleOutcome.Pass or ValidationRuleOutcome.Fail or ValidationRuleOutcome.Warning or ValidationRuleOutcome.Information),
            RulesPassed = ruleResults.Count(r => r.Severity == ValidationRuleOutcome.Pass),
            RulesFailed = ruleResults.Count(r => r.Severity == ValidationRuleOutcome.Fail),
            RulesSkipped = ruleResults.Count(r => r.Severity is ValidationRuleOutcome.Skipped or ValidationRuleOutcome.NotApplicable),
            CorrelationId = request.ExecutionContext.CorrelationId,
            ExecutionTraceId = request.ExecutionContext.ExecutionTraceId,
            PipelineVersion = request.ExecutionContext.PipelineVersion,
        };
}
