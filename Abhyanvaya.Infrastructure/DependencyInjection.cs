using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Infrastructure.BackgroundWorkers;
using Abhyanvaya.Infrastructure.Embedding;
using Abhyanvaya.Infrastructure.InsightFace;
using Abhyanvaya.Infrastructure.Recognition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Abhyanvaya.Infrastructure.Audit;
using Abhyanvaya.Infrastructure.DomainEvents;
using Abhyanvaya.Infrastructure.DomainEvents.Handlers;
using Abhyanvaya.Domain.Events;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Abhyanvaya.Infrastructure.Persistence.Repositories;
using Abhyanvaya.Infrastructure.Services;

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
                .Bind(configuration.GetSection(InsightFaceOptions.SectionName));

            services.AddSingleton<InsightFaceOnnxModelHost>();
            services.AddSingleton<IEmbeddingGenerationMetrics, EmbeddingGenerationMetrics>();
            services.AddScoped<IEmbeddingProviderFactory, EmbeddingProviderFactory>();
            services.AddSingleton<IStudentPhotoEmbeddingQueue, InMemoryStudentPhotoEmbeddingQueue>();
            services.AddSingleton<IClassroomPhotoQueue, InMemoryClassroomPhotoQueue>();

            services.AddScoped<InsightFaceEngine>();
            services.AddScoped<IFaceDetectionService, InsightFaceDetectionService>();
            services.AddScoped<IEmbeddingGenerator, InsightFaceEmbeddingGenerator>();
            services.AddScoped<IFaceMatcher, FaceMatcher>();
            services.AddScoped<IEmbeddingValidator, EmbeddingValidator>();
            services.AddScoped<IEmbeddingNormalizer, EmbeddingNormalizer>();
            services.AddScoped<IEmbeddingStorage, EmbeddingStorage>();
            services.AddScoped<IEmbeddingPipeline, EmbeddingPipeline>();
            services.AddScoped<IClassroomImageValidator, Validation.ClassroomImageValidator>();
            services.AddScoped<IClassroomRecognitionPipeline, ClassroomRecognitionPipeline>();

            services.AddHostedService<StudentFaceEmbeddingBackgroundService>();
            services.AddHostedService<ClassroomRecognitionBackgroundService>();

            services.AddScoped<IJwtService, JwtService>();
            services.AddHttpContextAccessor();
            services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IStudentRepository, StudentRepository>();

            return services;
        }
    }
}
