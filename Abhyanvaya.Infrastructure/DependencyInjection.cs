using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Infrastructure.BackgroundWorkers;
using Abhyanvaya.Infrastructure.Diagnostics;
using Abhyanvaya.Infrastructure.Diagnostics.MemoryAudit;
using Abhyanvaya.Infrastructure.Embedding;
using Abhyanvaya.Infrastructure.InsightFace;
using Abhyanvaya.Infrastructure.Recognition;
using Abhyanvaya.Infrastructure.Recognition.Orchestration;
using Abhyanvaya.Infrastructure.Recognition.Orchestration.Stages;
using Abhyanvaya.Infrastructure.Recognition.Engine;
using Abhyanvaya.Infrastructure.Recognition.Persistence;
using Abhyanvaya.Infrastructure.ClassroomAttendance;
using Abhyanvaya.Infrastructure.ClassroomAttendance.Persistence;
using Abhyanvaya.Infrastructure.ModelLifecycle;
using Abhyanvaya.Infrastructure.ModelLifecycle.Persistence;
using Abhyanvaya.Infrastructure.Operations;
using Abhyanvaya.Infrastructure.ArtifactStorage;
using Abhyanvaya.Infrastructure.TenantContext;
using Abhyanvaya.Infrastructure.EnrollmentApi;
using Abhyanvaya.Infrastructure.ProductionReadiness;
using Abhyanvaya.Infrastructure.FaceEnrollment;
using Abhyanvaya.Infrastructure.PhotoAcquisition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Abhyanvaya.Infrastructure.Audit;
using Abhyanvaya.Infrastructure.DomainEvents;
using Abhyanvaya.Infrastructure.DomainEvents.Handlers;
using Abhyanvaya.Domain.Events;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Abhyanvaya.Infrastructure.Persistence.Repositories;
using Abhyanvaya.Infrastructure.Services;
using Abhyanvaya.Infrastructure.Enrollment;
using Abhyanvaya.Infrastructure.Enrollment.Configuration;
using Abhyanvaya.Infrastructure.Enrollment.Pipeline;
using Abhyanvaya.Infrastructure.Enrollment.Queue;
using Abhyanvaya.Infrastructure.Enrollment.Queries;
using Abhyanvaya.Infrastructure.Enrollment.Validation;
using Abhyanvaya.Infrastructure.Enrollment.Validation.Rules;
using Abhyanvaya.Infrastructure.Enrollment.Storage;
using Abhyanvaya.Infrastructure.Enrollment.Storage.ArtifactTypes;
using Abhyanvaya.Infrastructure.Enrollment.Storage.Pipeline;
using Abhyanvaya.Infrastructure.Enrollment.Embedding;
using Abhyanvaya.Infrastructure.Enrollment.Persistence;
using Abhyanvaya.Infrastructure.Enrollment.Orchestration;
using Abhyanvaya.Infrastructure.Enrollment.Orchestration.Stages;
using Abhyanvaya.Infrastructure.Enrollment.Background;
using Abhyanvaya.Infrastructure.Enrollment.Versioning;
using Abhyanvaya.Infrastructure.Enrollment.PhotoProviders;
using Abhyanvaya.Infrastructure.Resilience;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IApplicationDbContext>(provider =>
                provider.GetRequiredService<ApplicationDbContext>());

            services.AddScoped<IUnitOfWork>(provider =>
                provider.GetRequiredService<ApplicationDbContext>());

            services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
            services.AddScoped<IDomainEventHandler<AttendanceMarkedEvent>, AttendanceMarkedEventHandler>();
            services.AddScoped<IDomainEventHandler<AttendanceGeneratedFromAIEvent>, AttendanceGeneratedFromAIEventHandler>();
            services.AddScoped<IDomainEventHandler<AttendanceFinalizedEvent>, AttendanceFinalizedEventHandler>();
            services.AddScoped<IDomainEventHandler<AttendanceLockedEvent>, AttendanceLockedEventHandler>();
            services.AddScoped<IDomainEventHandler<AttendanceUnlockedEvent>, AttendanceUnlockedEventHandler>();
            services.AddScoped<IAuditService, AuditService>();
            services.AddSingleton<IAttendanceCalendar, AttendanceCalendar>();

            services.AddOptions<InsightFaceOptions>()
                .Bind(configuration.GetSection(InsightFaceOptions.SectionName))
                .PostConfigure<IHostEnvironment>((options, environment) =>
                {
                    // Anchor relative ModelDirectory to ContentRootPath so model loading works under IIS,
                    // Docker, Windows Services, and cloud hosts — not only under `dotnet run` (AI12.OBS.10).
                    if (string.IsNullOrWhiteSpace(options.ModelDirectory) || Path.IsPathRooted(options.ModelDirectory))
                    {
                        return;
                    }

                    options.ModelDirectory = Path.Combine(environment.ContentRootPath, options.ModelDirectory);
                });

            services.AddSingleton<InsightFaceOnnxModelHost>();
            services.AddSingleton<IEmbeddingGenerationMetrics, EmbeddingGenerationMetrics>();
            services.AddScoped<IEmbeddingProviderFactory, EmbeddingProviderFactory>();
            services.AddSingleton<IStudentPhotoEmbeddingQueue, InMemoryStudentPhotoEmbeddingQueue>();
            services.AddSingleton<IClassroomPhotoQueue, InMemoryClassroomPhotoQueue>();

            services.AddOptions<AttendanceSessionRecoveryOptions>()
                .Bind(configuration.GetSection(AttendanceSessionRecoveryOptions.SectionName));
            services.AddSingleton<IAttendanceSessionRecoveryMetrics, AttendanceSessionRecoveryMetrics>();

            services.AddOptions<RecognitionDiagnosticsOptions>()
                .Bind(configuration.GetSection(RecognitionDiagnosticsOptions.SectionName));
            services.AddSingleton<IRecognitionDiagnosticsStore, RecognitionDiagnosticsStore>();
            services.AddScoped<IRecognitionPipelineDiagnostics, RecognitionPipelineDiagnostics>();

            // AI15.DIAGNOSTICS.2A: Scoped (not AsyncLocal/ThreadStatic/Singleton) per-job execution
            // context — one instance per DI scope, exactly like ITenantContextAccessor below.
            services.AddScoped<IRecognitionExecutionContext, RecognitionExecutionContext>();

            // AI17.RUNTIME: a separate Scoped forensics service from IRecognitionPipelineDiagnostics
            // above — see IRecognitionForensicsAudit's remarks for why. Same lifetime/pattern.
            services.AddScoped<IRecognitionForensicsAudit, RecognitionForensicsAudit>();

            // AI18.MEMORY.1: a separate Scoped memory-forensics service from IRecognitionForensicsAudit
            // above — see IRecognitionMemoryAudit's remarks for why. Same lifetime/pattern; gated by the
            // same RecognitionDiagnosticsOptions.Enabled flag and inert until its own Begin() call.
            services.AddScoped<IRecognitionMemoryAudit, RecognitionMemoryAudit>();

            services.AddScoped<InsightFaceEngine>();
            services.AddScoped<IFaceDetectionService, InsightFaceDetectionService>();
            services.AddScoped<IEmbeddingGenerator, InsightFaceEmbeddingGenerator>();
            services.AddScoped<IFaceMatcher, FaceMatcher>();
            services.AddScoped<IEmbeddingValidator, EmbeddingValidator>();
            services.AddScoped<IEmbeddingNormalizer, EmbeddingNormalizer>();
            services.AddScoped<IEmbeddingStorage, EmbeddingStorage>();
            services.AddScoped<IEmbeddingPipeline, EmbeddingPipeline>();
            services.AddScoped<IClassroomImageValidator, Validation.ClassroomImageValidator>();

            // AI18.REVIEW.2: dedicated recognition-thumbnail persistence seam between the pipeline
            // and the existing IMediaStorageService abstraction (implemented in the API layer as
            // ApplicationMediaStorageService, registered in Program.cs). The AI engine
            // (InsightFaceEngine) never sees this interface.
            services.AddScoped<IRecognitionMediaService, RecognitionMediaService>();
            services.AddScoped<IClassroomRecognitionPipeline, ClassroomRecognitionPipeline>();

            // AI20.PHASE2.3: recognition engine and vector search framework.
            services.AddOptions<RecognitionEngineOptions>()
                .Bind(configuration.GetSection(RecognitionEngineOptions.SectionName));
            services.AddSingleton<IRecognitionPipelineMetrics, NoOpRecognitionPipelineMetrics>();
            services.AddScoped<IRecognitionPolicy, ConfigurableRecognitionPolicy>();
            services.AddScoped<ISimilarityProvider, CosineSimilarityProvider>();
            services.AddScoped<ISimilarityProvider, EuclideanSimilarityProvider>();
            services.AddScoped<ISimilarityProvider, InnerProductSimilarityProvider>();
            services.AddScoped<SimilarityEngine>();
            services.AddScoped<ISimilarityEngine>(sp => sp.GetRequiredService<SimilarityEngine>());
            services.AddScoped<IVectorDatabaseProvider, PostgreSqlVectorDatabaseProvider>();
            services.AddScoped<IVectorSearchEngine, VectorSearchEngine>();
            services.AddScoped<IRecognitionDecisionEngine, RecognitionDecisionEngine>();
            services.AddScoped<IRecognitionRepository, RecognitionRepository>();
            services.AddScoped<IRecognitionResultWriter, RecognitionResultWriter>();
            services.AddScoped<IRecognitionCandidateStrategy, AttendanceSessionCandidateStrategy>();
            services.AddScoped<IRecognitionCandidateStrategy, CourseCandidateStrategy>();
            services.AddScoped<IRecognitionCandidateStrategy, TenantCandidateStrategy>();
            services.AddScoped<IRecognitionCandidateProvider, RecognitionCandidateProvider>();
            services.AddScoped<IRecognitionPipelineStage, EmbeddingRecognitionPipelineStage>();
            services.AddScoped<IRecognitionPipelineStage, CandidateRetrievalRecognitionPipelineStage>();
            services.AddScoped<IRecognitionPipelineStage, VectorSearchRecognitionPipelineStage>();
            services.AddScoped<IRecognitionPipelineStage, SimilarityRecognitionPipelineStage>();
            services.AddScoped<IRecognitionPipelineStage, DecisionRecognitionPipelineStage>();
            services.AddScoped<IRecognitionPipelineStage, PersistenceRecognitionPipelineStage>();
            services.AddScoped<IRecognitionPipelineRegistry, RecognitionPipelineRegistry>();
            services.AddScoped<IRecognitionPipelineExecutor, RecognitionPipelineExecutor>();
            services.AddScoped<IRecognitionOrchestrator, RecognitionOrchestrator>();

            // AI20.PHASE2.4: classroom recognition orchestration and attendance decision framework.
            services.AddOptions<ClassroomAttendanceOptions>()
                .Bind(configuration.GetSection(ClassroomAttendanceOptions.SectionName));
            services.AddScoped<IAttendancePolicy, ConfigurableAttendancePolicy>();
            services.AddScoped<IAttendanceSessionManager, AttendanceSessionManager>();
            services.AddScoped<IAttendanceValidationService, AttendanceValidationService>();
            services.AddScoped<IAttendanceConflictStrategy, HighestConfidenceConflictStrategy>();
            services.AddScoped<IAttendanceConflictStrategy, ManualReviewConflictStrategy>();
            services.AddScoped<IAttendanceConflictResolver, AttendanceConflictResolver>();
            services.AddScoped<IAttendanceDecisionEngine, AttendanceDecisionEngine>();
            services.AddScoped<IAttendanceRecognitionRepository, AttendanceRecognitionRepository>();
            services.AddScoped<IAttendanceResultWriter, AttendanceResultWriter>();
            services.AddScoped<IMultiFaceRecognitionCoordinator, MultiFaceRecognitionCoordinator>();
            services.AddScoped<IManualReviewService, ManualReviewService>();
            services.AddScoped<IAttendanceAnalyticsService, AttendanceAnalyticsService>();
            services.AddScoped<IClassroomRecognitionOrchestrator, ClassroomRecognitionOrchestrator>();

            // AI20.PHASE2.5: AI model lifecycle, quality, and governance framework.
            services.AddOptions<ModelLifecycleOptions>()
                .Bind(configuration.GetSection(ModelLifecycleOptions.SectionName));
            services.AddScoped<IModelLifecycleRepository, ModelLifecycleRepository>();
            services.AddScoped<IModelRegistry, ModelRegistry>();
            services.AddScoped<IModelVersionManager, ModelVersionManager>();
            services.AddScoped<IActiveModelProvider, ActiveModelProvider>();
            services.AddScoped<IEmbeddingCompatibilityService, EmbeddingCompatibilityService>();
            services.AddScoped<IModelCompatibilityService, ModelCompatibilityService>();
            services.AddScoped<IGoldenDatasetManager, GoldenDatasetManager>();
            services.AddScoped<IRecognitionRegressionRunner, RecognitionRegressionRunner>();
            services.AddScoped<IRecognitionBenchmarkService, RecognitionBenchmarkService>();
            services.AddScoped<IDriftDetectionService, DriftDetectionService>();
            services.AddScoped<IModelRolloutPolicy, TenantRolloutPolicy>();
            services.AddScoped<IModelRolloutPolicy, PercentageRolloutPolicy>();
            services.AddScoped<IModelRolloutPolicy, CanaryRolloutPolicy>();
            services.AddScoped<IModelRolloutManager, ModelRolloutManager>();
            services.AddScoped<IModelRollbackManager, ModelRollbackManager>();
            services.AddScoped<IRecognitionMetricsService, RecognitionMetricsService>();
            services.AddScoped<IRecognitionQualityEngine, RecognitionQualityEngine>();
            services.AddScoped<IContinuousLearningCoordinator, ContinuousLearningCoordinator>();

            // AI20.PHASE2.6: enterprise AI operations, observability, and production readiness.
            services.AddAIOperationsPlatform();

            services.AddHostedService<StudentFaceEmbeddingBackgroundService>();
            services.AddHostedService<ClassroomRecognitionBackgroundService>();
            services.AddHostedService<StuckAttendanceSessionRecoveryService>();

            services.AddScoped<IJwtService, JwtService>();
            services.AddHttpContextAccessor();
            services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IStudentRepository, StudentRepository>();

            // AI20.PHASE2.1.1: enrollment batch/item persistence repositories (thin; no orchestration).
            services.AddScoped<IStudentEnrollmentBatchRepository, StudentEnrollmentBatchRepository>();
            services.AddScoped<IStudentEnrollmentItemRepository, StudentEnrollmentItemRepository>();

            // AI20.IMPLEMENT.4/5: external photo provider framework. Only ExamBranchPhotoProvider is
            // registered today; future providers (OU/CSV/GoogleDrive/AzureBlob/OneDrive/ManualUpload —
            // see Domain.Constants.StudentPhotoProviders) plug in by adding another
            // AddScoped<IStudentPhotoProvider, TProvider>() line here — no factory or caller changes.
            services.AddOptions<ExamBranchPhotoProviderOptions>()
                .Bind(configuration.GetSection(ExamBranchPhotoProviderOptions.SectionName));

            services.AddHttpClient(ExamBranchPhotoProvider.HttpClientName, (provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ExamBranchPhotoProviderOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Abhyanvaya-AI-Enrollment/1.0");
            })
            .AddPolicyHandler(ExternalPhotoImportPolicies.RetryPolicy);

            services.AddScoped<IStudentPhotoProvider, ExamBranchPhotoProvider>();
            services.AddScoped<IStudentPhotoProviderFactory, StudentPhotoProviderFactory>();

            // AI21.PHASE1: enterprise student photo acquisition framework.
            services.AddPhotoAcquisitionPlatform(configuration);

            // AI21.PHASE2: enterprise face enrollment pipeline (consumes ReadyForEnrollment photos).
            services.AddFaceEnrollmentPipeline(configuration);

            // AI21.PHASE3: enterprise artifact storage platform (consumes artifact upload queue).
            services.AddArtifactStoragePlatform(configuration);

            // AI21.PHASE4: enterprise production deployment and go-live readiness.
            services.AddProductionReadinessPlatform(configuration);

            // AI22.PHASE1: enterprise enrollment API platform.
            services.AddEnrollmentApiPlatform();

            // AI22.5: enterprise operational tenant context platform.
            services.AddTenantContextPlatform();

            // AI20.PHASE2.1.2: enrollment batch creation service (create-only scope).
            services.AddOptions<EnrollmentPipelineOptions>()
                .Bind(configuration.GetSection(EnrollmentPipelineOptions.SectionName));
            services.AddSingleton<TimeProvider>(TimeProvider.System);
            services.AddScoped<IPipelineVersionProvider, ConfigurationPipelineVersionProvider>();
            services.AddSingleton<IPipelineManifestProvider, ConfigurationPipelineManifestProvider>();
            services.AddScoped<IEnrollmentConfigurationSnapshotCapture, EnrollmentConfigurationSnapshotCapture>();
            services.AddScoped<IEnrollmentEligibleStudentQuery, EnrollmentEligibleStudentQuery>();
            services.AddScoped<IEnrollmentReferenceValidator, EnrollmentReferenceValidator>();
            services.AddScoped<IEnrollmentBatchService, EnrollmentBatchService>();
            services.AddSingleton<InMemoryEnrollmentWakeSignal>();
            services.AddSingleton<IEnrollmentJobQueue>(sp => sp.GetRequiredService<InMemoryEnrollmentWakeSignal>());

            // AI20.PHASE2.1.3: enrollment progress reporter (sole progress/counter writer).
            services.AddScoped<IEnrollmentProgressSnapshotRepository, EnrollmentProgressSnapshotRepository>();
            services.AddScoped<IEnrollmentProgressReporter, EnrollmentProgressReporter>();

            // AI20.PHASE2.1.4: enrollment validation service (pure evaluation — no storage/DB).
            services.AddOptions<EnrollmentValidationOptions>()
                .Bind(configuration.GetSection(EnrollmentValidationOptions.SectionName));
            services.AddScoped<IEnrollmentFaceAnalysisService, InsightFaceEnrollmentFaceAnalysisService>();
            services.AddScoped<IEnrollmentValidationPolicy, DefaultEnrollmentValidationPolicy>();
            services.AddSingleton<IValidationCache, NoOpValidationCache>();
            RegisterEnrollmentValidationRules(services);
            services.AddScoped<IEnrollmentValidationRuleRegistry, EnrollmentValidationRuleRegistry>();
            services.AddScoped<IEnrollmentValidationService, EnrollmentValidationService>();

            // AI20.PHASE2.1.5: enrollment storage service (sole artifact persistence owner).
            services.AddScoped<IChecksumService, Sha256ChecksumService>();
            services.AddSingleton<IEnrollmentArtifactTypeDefinition, AlignedFaceArtifactTypeDefinition>();
            services.AddSingleton<IEnrollmentArtifactTypeDefinition, ValidationReportArtifactTypeDefinition>();
            services.AddSingleton<IEnrollmentArtifactTypeRegistry, EnrollmentArtifactTypeRegistry>();
            services.AddScoped<IEnrollmentStoragePolicy, DefaultEnrollmentStoragePolicy>();
            services.AddScoped<IEnrollmentStorageRecordRepository, EnrollmentStorageRecordRepository>();
            services.AddSingleton<IStorageMetricsCollector, NoOpStorageMetricsCollector>();
            services.AddSingleton<IEnrollmentArtifactCache, NoOpEnrollmentArtifactCache>();
            RegisterEnrollmentStoragePipeline(services);
            services.AddScoped<IEnrollmentStorageService, EnrollmentStorageService>();

            // AI20.PHASE2.1.5A: enrollment artifact resolver (sole artifact read owner).
            services.AddScoped<IEnrollmentArtifactResolver, EnrollmentArtifactResolver>();

            // AI20.PHASE2.1.6: enrollment embedding service (sole embedding generation owner).
            services.AddScoped<IEmbeddingEngine, InsightFaceEmbeddingEngine>();
            services.AddScoped<IEmbeddingQualityAnalyzer, EmbeddingQualityAnalyzer>();
            services.AddScoped<IEnrollmentEmbeddingService, EnrollmentEmbeddingService>();

            // AI20.PHASE2.1.7: enrollment result writer (sole embedding persistence owner).
            services.AddSingleton<IEnrollmentPersistenceMetrics, NoOpEnrollmentPersistenceMetrics>();
            services.AddScoped<IEnrollmentPersistencePolicy, DefaultEnrollmentPersistencePolicy>();
            services.AddScoped<IEnrollmentPersistenceRepository, EnrollmentPersistenceRepository>();
            services.AddScoped<IEnrollmentDuplicateDetector, EnrollmentDuplicateDetector>();
            services.AddScoped<IEnrollmentResultWriter, EnrollmentResultWriter>();

            // AI20.PHASE2.1.8: enrollment orchestrator and pipeline workflow engine.
            services.AddSingleton<IEnrollmentPipelineMetrics, NoOpEnrollmentPipelineMetrics>();
            services.AddScoped<IEnrollmentRetryPolicy, DefaultEnrollmentRetryPolicy>();
            services.AddScoped<IEnrollmentPipelineStage, DownloadEnrollmentPipelineStage>();
            services.AddScoped<IEnrollmentPipelineStage, ValidationEnrollmentPipelineStage>();
            services.AddScoped<IEnrollmentPipelineStage, StorageEnrollmentPipelineStage>();
            services.AddScoped<IEnrollmentPipelineStage, EmbeddingEnrollmentPipelineStage>();
            services.AddScoped<IEnrollmentPipelineStage, PersistenceEnrollmentPipelineStage>();
            services.AddScoped<IEnrollmentPipelineStage, ProgressEnrollmentPipelineStage>();
            services.AddScoped<IEnrollmentPipelineRegistry, EnrollmentPipelineRegistry>();
            services.AddScoped<IEnrollmentPipelineExecutor, EnrollmentPipelineExecutor>();
            services.AddScoped<IEnrollmentOrchestrator, EnrollmentOrchestrator>();

            // AI20.PHASE2.2: enrollment background processing and distributed worker framework.
            services.AddOptions<EnrollmentBackgroundOptions>()
                .Bind(configuration.GetSection(EnrollmentBackgroundOptions.SectionName));
            services.AddOptions<EnrollmentRecoveryOptions>()
                .Bind(configuration.GetSection(EnrollmentRecoveryOptions.SectionName));
            services.AddSingleton<IEnrollmentWorkerMetrics, NoOpEnrollmentWorkerMetrics>();
            services.AddScoped<IEnrollmentWorkRepository, EnrollmentWorkRepository>();
            services.AddScoped<IEnrollmentSchedulingPolicy, DefaultEnrollmentSchedulingPolicy>();
            services.AddScoped<IDistributedLockProvider, PostgreSqlDistributedLockProvider>();
            services.AddScoped<IEnrollmentDeadLetterService, EnrollmentDeadLetterService>();
            services.AddScoped<IEnrollmentLeaseManager, EnrollmentLeaseManager>();
            services.AddScoped<IEnrollmentHeartbeatService, EnrollmentHeartbeatService>();
            services.AddScoped<IEnrollmentWorkScheduler, EnrollmentWorkScheduler>();
            services.AddScoped<IEnrollmentWorkQueue, DatabaseEnrollmentWorkQueue>();
            services.AddScoped<EnrollmentProcessingWorker>();
            services.AddScoped<IEnrollmentWorker, EnrollmentProcessingWorker>();
            services.AddScoped<IEnrollmentWorkerHost, EnrollmentWorkerHost>();
            services.AddScoped<IEnrollmentRecoveryService, EnrollmentRecoveryService>();
            if (configuration.GetValue<bool>($"{EnrollmentBackgroundOptions.SectionName}:Enabled", true))
            {
                services.AddHostedService<EnrollmentBackgroundService>();
            }

            if (configuration.GetValue<bool>($"{EnrollmentRecoveryOptions.SectionName}:Enabled", true))
            {
                services.AddHostedService<EnrollmentRecoveryBackgroundService>();
            }

            return services;
        }

        private static void RegisterEnrollmentStoragePipeline(IServiceCollection services)
        {
            services.AddScoped<IEnrollmentStorageStep, ValidateInputStep>();
            services.AddScoped<IEnrollmentStorageStep, ResolvePolicyStep>();
            services.AddScoped<IEnrollmentStorageStep, PrepareArtifactsStep>();
            services.AddScoped<IEnrollmentStorageStep, ChecksumStep>();
            services.AddScoped<IEnrollmentStorageStep, CompressionStep>();
            services.AddScoped<IEnrollmentStorageStep, EncryptionStep>();
            services.AddScoped<IEnrollmentStorageStep, DuplicateDetectionStep>();
            services.AddScoped<IEnrollmentStorageStep, UploadStep>();
            services.AddScoped<IEnrollmentStorageStep, MetadataStep>();
            services.AddScoped<IEnrollmentStorageStep, ManifestStep>();
            services.AddScoped<RollbackStep>();
            services.AddScoped<EnrollmentStoragePipelineExecutor>();
            services.AddScoped<IEnrollmentStoragePipelineExecutor>(sp =>
                sp.GetRequiredService<EnrollmentStoragePipelineExecutor>());
        }

        private static void RegisterEnrollmentValidationRules(IServiceCollection services)
        {
            services.AddScoped<IEnrollmentValidationRule, ImageFormatRule>();
            services.AddScoped<IEnrollmentValidationRule, CorruptImageRule>();
            services.AddScoped<IEnrollmentValidationRule, MinimumResolutionRule>();
            services.AddScoped<IEnrollmentValidationRule, MaximumResolutionRule>();
            services.AddScoped<IEnrollmentValidationRule, ExactlyOneFaceRule>();
            services.AddScoped<IEnrollmentValidationRule, FaceConfidenceRule>();
            services.AddScoped<IEnrollmentValidationRule, MinimumFaceCropResolutionRule>();
            services.AddScoped<IEnrollmentValidationRule, FaceCoverageRule>();
            services.AddScoped<IEnrollmentValidationRule, BlurRule>();
            services.AddScoped<IEnrollmentValidationRule, PoseRule>();
            services.AddScoped<IEnrollmentValidationRule, BrightnessRule>();
            services.AddScoped<IEnrollmentValidationRule, ContrastRule>();

            foreach (var futureRule in FutureValidationRuleFactory.CreateRules())
            {
                services.AddSingleton<IEnrollmentValidationRule>(futureRule);
            }
        }
    }
}
