using Abhyanvaya.API.Common;
using Abhyanvaya.API.Diagnostics;
using Abhyanvaya.API.ExceptionHandling;
using Abhyanvaya.API.Media;
using Abhyanvaya.API.Services;
using Abhyanvaya.API.Common.Auth.Handlers;
using Abhyanvaya.API.Common.Auth.Requirements;
using Abhyanvaya.Application;
using Abhyanvaya.API.SignalR;
using Abhyanvaya.API.Hubs;
using Abhyanvaya.API.Middleware;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Mappings;
using Abhyanvaya.Infrastructure;
using Abhyanvaya.Infrastructure.BackgroundWorkers;
using Abhyanvaya.Infrastructure.ProductionReadiness;
using Abhyanvaya.Infrastructure.Diagnostics;
using Abhyanvaya.Infrastructure.InsightFace;
using Abhyanvaya.Infrastructure.Persistence;
using Abhyanvaya.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Enums;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtIssuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is required.");
var jwtAudience = jwtSettings["Audience"] ?? throw new InvalidOperationException("Jwt:Audience is required.");
var jwtKey = jwtSettings["Key"] ?? throw new InvalidOperationException("Jwt:Key is required. Set via user-secrets or environment variable Jwt__Key.");

// Add services
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHostedService<EnrollmentStartupValidationHostedService>();
builder.Services.AddSingleton<IEnrollmentSignalRPublisher, EnrollmentSignalRPublisher>();
builder.Services.AddScoped<IEnrollmentActorPermissions, EnrollmentActorPermissions>();
builder.Services.Configure<EnrollmentProgressBroadcastOptions>(
    builder.Configuration.GetSection(EnrollmentProgressBroadcastOptions.SectionName));
if (builder.Configuration.GetValue<bool>($"{EnrollmentProgressBroadcastOptions.SectionName}:Enabled", true))
{
    builder.Services.AddHostedService<EnrollmentProgressBroadcastService>();
}
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token like: Bearer {your token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Background worker failure policy: Development favors developer productivity (keep the host
// alive so a single bad job doesn't kill the debug session); Production favors fail-fast so an
// orchestrator (IIS / Kubernetes / Docker / Azure App Service) can detect and restart the process.
builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = builder.Environment.IsDevelopment()
        ? BackgroundServiceExceptionBehavior.Ignore
        : BackgroundServiceExceptionBehavior.StopHost;
});

// Add Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddAutoMapper(typeof(StudentMappingProfile).Assembly);
builder.Services.AddMediaStorage();
builder.Services.AddStudentPhotoServices();
builder.Services.AddScoped<Abhyanvaya.Application.Common.Interfaces.IMediaStorageService, ApplicationMediaStorageService>();
builder.Services.AddScoped<Abhyanvaya.Application.Common.Interfaces.IObjectStorageProvider, ObjectStorageProviderAdapter>();
builder.Services.AddScoped<IMediaObjectReader, MediaObjectReader>();
builder.Services.AddScoped<CollegeBrandingService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        },
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(AuthorizationPolicies.TenantScopedUser, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new HasTenantRequirement());
    });

    options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
                return true;
            return string.Equals(role, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase);
        });
    });

    options.AddPolicy(AuthorizationPolicies.AdminOrFaculty, policy =>
        policy.RequireRole("Admin", "Faculty"));

    options.AddPolicy(AuthorizationPolicies.CanViewStudents, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.Equals(role, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(role, nameof(UserRole.Faculty), StringComparison.OrdinalIgnoreCase))
                return false;
            return int.TryParse(ctx.User.FindFirst("TenantId")?.Value, out var tid) && tid > 0;
        });
    });

    options.AddPolicy(AuthorizationPolicies.CanManageStudents, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
                return true;
            return string.Equals(role, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase)
                   && int.TryParse(ctx.User.FindFirst("TenantId")?.Value, out var tid)
                   && tid > 0;
        });
    });

    options.AddPolicy(AuthorizationPolicies.CanManageAttendance, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.Equals(role, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(role, nameof(UserRole.Faculty), StringComparison.OrdinalIgnoreCase))
                return false;
            return int.TryParse(ctx.User.FindFirst("TenantId")?.Value, out var tid) && tid > 0;
        });
    });

    options.AddPolicy(AuthorizationPolicies.CanViewReports, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.Equals(role, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(role, nameof(UserRole.Faculty), StringComparison.OrdinalIgnoreCase))
                return false;
            return int.TryParse(ctx.User.FindFirst("TenantId")?.Value, out var tid) && tid > 0;
        });
    });

    options.AddPolicy(AuthorizationPolicies.SuperAdminOnly, policy =>
        policy.RequireAuthenticatedUser().RequireRole(nameof(UserRole.SuperAdmin)));

    options.AddPolicy(AuthorizationPolicies.TenantScopedAdmin, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
                return true;
            return string.Equals(role, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase)
                   && int.TryParse(ctx.User.FindFirst("TenantId")?.Value, out var tid)
                   && tid > 0;
        });
    });

    options.AddPolicy(AuthorizationPolicies.UniversityListAccess, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
                return true;
            if (int.TryParse(ctx.User.FindFirst("TenantId")?.Value, out var tidOrg) && tidOrg > 0
                && ctx.User.HasClaim("permission", PermissionKeys.OrganizationManage))
                return true;
            return string.Equals(role, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase)
                   && int.TryParse(ctx.User.FindFirst("TenantId")?.Value, out var tid)
                   && tid > 0;
        });
    });

    options.AddPolicy(AuthorizationPolicies.DashboardOverviewAccess, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
                return true;
            return int.TryParse(ctx.User.FindFirst("TenantId")?.Value, out var tid)
                   && tid > 0
                   && (string.Equals(role, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase)
                       || string.Equals(role, nameof(UserRole.Faculty), StringComparison.OrdinalIgnoreCase));
        });
    });

    void AddSetupManagePolicy(string policyName, string permissionKey)
    {
        options.AddPolicy(policyName, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx =>
            {
                var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
                if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
                    return true;
                if (!int.TryParse(ctx.User.FindFirst("TenantId")?.Value, out var tid) || tid <= 0)
                    return false;
                return ctx.User.HasClaim("permission", permissionKey);
            });
        });
    }

    AddSetupManagePolicy(AuthorizationPolicies.CanManageCourses, PermissionKeys.SetupCoursesManage);
    AddSetupManagePolicy(AuthorizationPolicies.CanManageGroups, PermissionKeys.SetupGroupsManage);
    AddSetupManagePolicy(AuthorizationPolicies.CanManageSemesters, PermissionKeys.SetupSemestersManage);
    AddSetupManagePolicy(AuthorizationPolicies.CanManageOrganization, PermissionKeys.OrganizationManage);

    options.AddPolicy(AuthorizationPolicies.TenantCollegeAdminOnly, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
            return string.Equals(role, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase)
                   && int.TryParse(ctx.User.FindFirst("TenantId")?.Value, out var tid)
                   && tid > 0;
        });
    });

    options.AddPolicy(AuthorizationPolicies.CanViewEnrollment, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
                return true;
            return ctx.User.HasClaim("permission", PermissionKeys.EnrollmentView)
                   || ctx.User.HasClaim("permission", PermissionKeys.EnrollmentManage)
                   || ctx.User.HasClaim("permission", PermissionKeys.StudentsView);
        });
    });

    options.AddPolicy(AuthorizationPolicies.CanManageEnrollment, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
                return true;
            return ctx.User.HasClaim("permission", PermissionKeys.EnrollmentManage)
                   || ctx.User.HasClaim("permission", PermissionKeys.StudentsManage);
        });
    });
});
builder.Services.AddSingleton<IAuthorizationHandler, HasTenantHandler>();

builder.Services.AddMemoryCache();

var useRedis = builder.Configuration.GetValue<bool>("UseRedis");
if (useRedis)
{
    var redisConnection = builder.Configuration["Redis:Connection"] ?? builder.Configuration.GetConnectionString("Redis");
    if (string.IsNullOrWhiteSpace(redisConnection))
        throw new InvalidOperationException("Redis connection is required when UseRedis=true.");

    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "Abhyanvaya_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddScoped<MemoryCacheService>();
builder.Services.AddScoped<RedisCacheService>();
builder.Services.AddScoped<ICacheService, SmartCacheService>();

var corsOriginsRaw = builder.Configuration["Cors:ReactOrigin"] ?? "http://localhost:5173";
var corsAllowed = corsOriginsRaw
    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
var allowCloudflarePages = builder.Configuration.GetValue<bool>("Cors:AllowCloudflarePages");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrEmpty(origin))
                    return false;
                if (corsAllowed.Contains(origin))
                    return true;
                if (allowCloudflarePages
                    && Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    && uri.Scheme == Uri.UriSchemeHttps
                    && uri.Host.EndsWith(".pages.dev", StringComparison.OrdinalIgnoreCase))
                    return true;
                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var portEnv = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(portEnv))
    builder.WebHost.UseUrls($"http://+:{portEnv}");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var migrateScope = app.Services.CreateAsyncScope();
    var dbContext = migrateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();

app.UseCors("AllowReact");

var brandingProvider = BrandingSettingsResolver.Get(app.Configuration, "Branding:Provider") ?? "local";
var brandingPublicBaseUrl = BrandingSettingsResolver.Get(app.Configuration, "Branding:PublicBaseUrl");
app.Logger.LogInformation(
    "Branding configured with Provider={Provider}, PublicBaseUrl={PublicBaseUrl}",
    brandingProvider,
    string.IsNullOrWhiteSpace(brandingPublicBaseUrl) ? "<empty>" : brandingPublicBaseUrl);

var enableSwagger = app.Environment.IsDevelopment()
    || app.Configuration.GetValue<bool>("EnableSwagger");
if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

void AddPublicBrandingHeaders(StaticFileResponseContext ctx)
{
    ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=86400");
    ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
}

static string ResolveLocalMediaPhysicalRoot(IConfiguration configuration, IWebHostEnvironment env)
{
    var configured = configuration["Media:PhysicalRoot"]?.Trim();
    if (string.IsNullOrEmpty(configured))
        configured = configuration["Branding:PhysicalRoot"]?.Trim();
    if (!string.IsNullOrEmpty(configured))
        return configured;

    var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
    return Path.Combine(webRoot, "branding");
}

var brandingPhysical = app.Configuration["Branding:PhysicalRoot"]?.Trim();
if (!string.IsNullOrEmpty(brandingPhysical))
{
    Directory.CreateDirectory(brandingPhysical);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(brandingPhysical),
        RequestPath = "/branding",
        OnPrepareResponse = AddPublicBrandingHeaders,
    });
}

var mediaPhysical = ResolveLocalMediaPhysicalRoot(app.Configuration, app.Environment);
Directory.CreateDirectory(mediaPhysical);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaPhysical),
    RequestPath = "/media",
    OnPrepareResponse = AddPublicBrandingHeaders,
});

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Path.StartsWithSegments("/branding", StringComparison.OrdinalIgnoreCase))
            AddPublicBrandingHeaders(ctx);
    },
});
app.UseAuthentication();
app.UseTenantContext();
app.UseAuthorization();
app.MapControllers();
app.MapHub<EnrollmentHub>("/hubs/enrollment").RequireCors("AllowReact");

MapPlatformHealthEndpoints(app);

// AI12.OBS.1: hosted background workers are started by the Generic Host inside StartAsync(), which
// app.Run() would otherwise call internally right before blocking. Splitting StartAsync() out lets
// the startup summary (logged immediately after) inspect each worker's *actual* running state
// (BackgroundService.ExecuteTask) instead of a pre-start snapshot. This is not a behavior change —
// app.Run() itself performs the exact same StartAsync() + WaitForShutdownAsync() sequence internally.
await app.StartAsync();
LogStartupConfigurationSummary(app);
await app.WaitForShutdownAsync();

// AI11.HARDENING.1-3 / AI12.OBS.1-5: emits a single structured startup summary, executed once
// after all hosted services (including the recognition/embedding background workers) have started
// but before the summary log call returns control to WaitForShutdownAsync(). Values are pulled
// live from DI/configuration/the running host — nothing here is hardcoded except field labels.
static void LogStartupConfigurationSummary(WebApplication app)
{
    var logger = app.Logger;
    var configuredHostOptions = app.Services.GetRequiredService<IOptions<HostOptions>>().Value;
    var backgroundServiceExceptionBehavior = configuredHostOptions.BackgroundServiceExceptionBehavior switch
    {
        BackgroundServiceExceptionBehavior.StopHost => "StopHost",
        _ => "Ignore",
    };
    var backgroundServiceExceptionBehaviorReason = backgroundServiceExceptionBehavior == "StopHost"
        ? "Production Fail-Fast"
        : "Development Environment";

    // AI12.OBS.1: derived from the actual IHostedService singleton + its own BackgroundService.ExecuteTask —
    // no polling loop or separate tracking service, just a point-in-time read of the running host.
    var recognitionWorkerStatus = BackgroundWorkerInspector.Inspect(app.Services, typeof(ClassroomRecognitionBackgroundService));
    var embeddingWorkerStatus = BackgroundWorkerInspector.Inspect(app.Services, typeof(StudentFaceEmbeddingBackgroundService));

    using var scope = app.Services.CreateScope();

    // AI12.OBS.2: no switch statement — the active IStorageProvider exposes its own display metadata.
    var storageProvider = scope.ServiceProvider.GetRequiredService<IStorageProviderFactory>().GetActiveProvider();

    var recognitionEngineName = scope.ServiceProvider.GetRequiredService<IFaceDetectionService>().ProviderName;
    var insightFaceOptions = app.Services.GetRequiredService<IOptions<InsightFaceOptions>>().Value;
    var recognitionPipelineVersion = insightFaceOptions.PipelineVersion;

    // AI12.OBS.3: no hardcoded "Cosine Similarity" string — metadata comes from IFaceMatcher itself.
    var faceMatcher = scope.ServiceProvider.GetRequiredService<IFaceMatcher>();

    // AI11.HARDENING.3 / AI12.OBS.5 / AI12.OBS.10: read-only presence + size check
    // (File.Exists/FileInfo.Length only) against the ContentRootPath-resolved directory.
    // Models are never loaded here, so a missing model cannot affect or delay startup.
    var modelReport = ModelAvailabilityChecker.Check(insightFaceOptions, app.Environment);

    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var dbProviderDisplayName = StartupDiagnostics.DescribeDatabaseProvider(dbContext.Database.ProviderName);

    var applicationVersion = StartupDiagnostics.ResolveApplicationVersion();

    // AI12.OBS.4: SaaS deployment metadata, all read from actual configuration (no hardcoding).
    var tenancyModeRaw = app.Configuration["Tenancy:Mode"];
    var tenantModeDisplay = string.Equals(tenancyModeRaw, "SingleTenant", StringComparison.OrdinalIgnoreCase)
        ? "Single Tenant"
        : "Multi Tenant";
    var useRedis = app.Configuration.GetValue<bool>("UseRedis");
    var cacheProviderDisplay = useRedis ? "Redis" : "Memory";
    var recognitionQueueDisplay = StartupDiagnostics.DescribeQueueImplementation(
        scope.ServiceProvider.GetRequiredService<IClassroomPhotoQueue>());

    // AI14.RUNTIME.4: reuses the same IOptions<AttendanceSessionRecoveryOptions> instance the
    // background service itself binds — no separate/duplicate configuration read.
    var recoveryOptions = app.Services.GetRequiredService<IOptions<AttendanceSessionRecoveryOptions>>().Value;

    logger.LogInformation("==========================================================");
    logger.LogInformation("Abhyanvaya AI Attendance Startup");
    logger.LogInformation("==========================================================");
    logger.LogInformation("Application Environment             : {Environment}", app.Environment.EnvironmentName);
    logger.LogInformation("Application Version                 : {Version}", applicationVersion);
    logger.LogInformation("BackgroundServiceExceptionBehavior  : {Behavior}", backgroundServiceExceptionBehavior);
    logger.LogInformation("  Reason                             : {Reason}", backgroundServiceExceptionBehaviorReason);

    LogWorkerStatus(logger, "Recognition Worker", recognitionWorkerStatus);
    LogWorkerStatus(logger, "Embedding Worker", embeddingWorkerStatus);

    logger.LogInformation("Media Provider                      : {DisplayName}", storageProvider.DisplayName);
    logger.LogInformation("Recognition Engine                  : {RecognitionEngine}", recognitionEngineName);
    logger.LogInformation("Recognition Pipeline Version        : {PipelineVersion}", recognitionPipelineVersion);
    logger.LogInformation("Face Matching Engine                : {FaceMatchingEngine}", faceMatcher.Name);
    logger.LogInformation("  Algorithm                          : {Algorithm}", faceMatcher.Algorithm);
    logger.LogInformation("  Matcher Version                    : {MatcherVersion}", faceMatcher.Version);

    // AI12.OBS.10: log both the raw configured value and the ContentRootPath-resolved absolute
    // path so operators can see exactly which directory was actually checked on disk.
    logger.LogInformation("Configured Model Directory           : {ConfiguredModelDirectory}", modelReport.ConfiguredModelDirectory);
    if (modelReport.ModelDirectoryExists)
    {
        logger.LogInformation("Resolved Model Directory             : {ResolvedModelDirectory}", modelReport.ResolvedModelDirectory);
    }
    else
    {
        logger.LogWarning("Resolved Model Directory             : {ResolvedModelDirectory} (MISSING)", modelReport.ResolvedModelDirectory);
    }

    LogModelFileStatus(logger, "Detection Model", modelReport.Detection);
    LogModelFileStatus(logger, "Embedding Model", modelReport.Embedding);

    // AI14.RUNTIME.1: report the actual ONNX Runtime thread configuration that
    // InsightFaceOnnxModelHost passes into SessionOptions when it loads each model. Values are read
    // from the same IOptions<InsightFaceOptions> instance used everywhere else in this method
    // (no duplicate configuration reads) — never hardcoded, and this log call cannot change or
    // influence session/thread behavior, it only reports it.
    LogOnnxRuntimeThreadConfiguration(logger, app.Configuration, insightFaceOptions);

    logger.LogInformation("Tenant Mode                         : {TenantMode}", tenantModeDisplay);
    logger.LogInformation("Deployment                          : {Deployment}", app.Environment.EnvironmentName);
    logger.LogInformation("Database Provider                   : {DatabaseProvider}", dbProviderDisplayName);
    logger.LogInformation("Cache Provider                      : {CacheProvider}", cacheProviderDisplay);
    logger.LogInformation("Recognition Queue                   : {RecognitionQueue}", recognitionQueueDisplay);

    LogRecoveryServiceConfiguration(logger, recoveryOptions);

    logger.LogInformation("Started At UTC                      : {StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC", DateTime.UtcNow);
    logger.LogInformation("==========================================================");

    // AI12.OBS.7-9: soft, advisory configuration validation — every finding is only ever logged
    // (at a level derived from its severity), never thrown, and never blocks startup. See
    // ConfigurationValidator for the full rationale on why these checks are additive to (not a
    // replacement for) the existing hard fail-fast checks above.
    var configurationIssues = ConfigurationValidator.Validate(app, modelReport);
    LogConfigurationIssues(logger, configurationIssues);
}

static void LogConfigurationIssues(ILogger logger, IReadOnlyList<ConfigurationIssue> issues)
{
    logger.LogInformation("----------------------------------------------------------");
    logger.LogInformation("Startup Configuration Validation");
    logger.LogInformation("----------------------------------------------------------");

    if (issues.Count == 0)
    {
        logger.LogInformation("All startup configuration checks passed. No issues detected.");
    }
    else
    {
        foreach (var issue in issues)
        {
            // Severity only ever selects the *log level* used to report the finding — it never
            // throws and never influences whether startup continues (AI12.OBS.8).
            var logLevel = issue.Severity switch
            {
                ConfigurationSeverity.Critical => LogLevel.Error,
                ConfigurationSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Information,
            };

            logger.Log(logLevel, "[{Severity}]", issue.Severity);
            logger.Log(logLevel, "  Category           : {Category}", issue.Category);
            logger.Log(logLevel, "  Configuration Key  : {ConfigurationKey}", issue.ConfigurationKey);
            logger.Log(logLevel, "  Message            : {Message}", issue.Message);
            logger.Log(logLevel, "  Suggested Fix      : {SuggestedFix}", issue.SuggestedFix);
        }

        var criticalCount = issues.Count(i => i.Severity == ConfigurationSeverity.Critical);
        var warningCount = issues.Count(i => i.Severity == ConfigurationSeverity.Warning);
        var informationCount = issues.Count(i => i.Severity == ConfigurationSeverity.Information);
        logger.LogInformation(
            "{Count} configuration issue(s) detected ({Critical} Critical, {Warning} Warning, {Information} Information). Startup is continuing normally — these are advisory only.",
            issues.Count,
            criticalCount,
            warningCount,
            informationCount);
    }

    logger.LogInformation("----------------------------------------------------------");
}

static void LogWorkerStatus(ILogger logger, string workerLabel, BackgroundWorkerStatus status)
{
    logger.LogInformation("{WorkerLabel}", workerLabel);
    logger.LogInformation("  Registered                         : {Registered}", status.Registered ? "Yes" : "No");
    logger.LogInformation("  Running                             : {Running}", status.Running ? "Yes" : "No");
    logger.LogInformation("  Startup Status                      : {StartupStatus}", status.StartupStatus);

    if (status.Health == "Healthy")
    {
        logger.LogInformation("  Health                              : {Health}", status.Health);
    }
    else
    {
        logger.LogWarning("  Health                              : {Health}", status.Health);
    }
}

static void LogModelFileStatus(ILogger logger, string modelLabel, ModelFileStatus status)
{
    if (status.Found)
    {
        logger.LogInformation(
            "{ModelLabel} ({FileName})           : Found ({SizeMB} MB)",
            modelLabel,
            status.FileName,
            status.SizeMegabytes);
    }
    else
    {
        logger.LogWarning(
            "{ModelLabel} ({FileName})           : Missing (0 MB)",
            modelLabel,
            status.FileName);
    }
}

// AI14.RUNTIME.1: reports the ONNX Runtime thread configuration InsightFaceOnnxModelHost actually
// applies to SessionOptions when it loads the detection/embedding models. Values always come from
// the bound InsightFaceOptions instance (backed by its own C# default of 1/1 when a key is absent
// from configuration) — this method never hardcodes a thread count, it only formats what IOptions
// already resolved. When a key is missing from configuration, that is called out explicitly so
// operators know the value shown is the built-in default rather than something set in appsettings.
static void LogOnnxRuntimeThreadConfiguration(ILogger logger, IConfiguration configuration, InsightFaceOptions insightFaceOptions)
{
    var intraOpConfigured = !string.IsNullOrWhiteSpace(configuration[$"{InsightFaceOptions.SectionName}:IntraOpNumThreads"]);
    var interOpConfigured = !string.IsNullOrWhiteSpace(configuration[$"{InsightFaceOptions.SectionName}:InterOpNumThreads"]);

    logger.LogInformation("ONNX Runtime");
    LogOnnxThreadSetting(logger, "IntraOp Threads", insightFaceOptions.IntraOpNumThreads, intraOpConfigured);
    LogOnnxThreadSetting(logger, "InterOp Threads", insightFaceOptions.InterOpNumThreads, interOpConfigured);

    // AI16.RUNTIME.1: surfaces the two memory-allocator SessionOptions this milestone made
    // configurable (see docs/AI16_RUNTIME1_ONNX_MEMORY_OPTIMIZATION.md). Read only — never rebinds
    // InsightFaceOptions a second time.
    var cpuMemArenaConfigured = !string.IsNullOrWhiteSpace(configuration[$"{InsightFaceOptions.SectionName}:EnableCpuMemArena"]);
    var memPatternConfigured = !string.IsNullOrWhiteSpace(configuration[$"{InsightFaceOptions.SectionName}:EnableMemoryPattern"]);
    LogOnnxBoolSetting(logger, "CPU Mem Arena", insightFaceOptions.EnableCpuMemArena, cpuMemArenaConfigured);
    LogOnnxBoolSetting(logger, "Memory Pattern", insightFaceOptions.EnableMemoryPattern, memPatternConfigured);
}

static void LogOnnxThreadSetting(ILogger logger, string label, int threadCount, bool explicitlyConfigured)
{
    if (explicitlyConfigured)
    {
        logger.LogInformation("  {Label}                     : {ThreadCount}", label, threadCount);
    }
    else
    {
        logger.LogInformation("  {Label}                     : {ThreadCount} (default — not set in configuration)", label, threadCount);
    }
}

static void LogOnnxBoolSetting(ILogger logger, string label, bool value, bool explicitlyConfigured)
{
    if (explicitlyConfigured)
    {
        logger.LogInformation("  {Label}                    : {Value}", label, value);
    }
    else
    {
        logger.LogInformation("  {Label}                    : {Value} (default — not set in configuration)", label, value);
    }
}

// AI14.RUNTIME.4: startup visibility into the recovery sweep's own configuration — every value comes
// straight from the bound AttendanceSessionRecoveryOptions (AI14.RUNTIME.2), never hardcoded here.
static void LogRecoveryServiceConfiguration(ILogger logger, AttendanceSessionRecoveryOptions recoveryOptions)
{
    logger.LogInformation("Recovery Service");
    logger.LogInformation("  Enabled                             : {Enabled}", recoveryOptions.Enabled ? "Yes" : "No");
    logger.LogInformation("  Scan Interval                       : {ScanIntervalSeconds} seconds", recoveryOptions.ScanIntervalSeconds);
    logger.LogInformation("  Recovery Timeout                    : {TimeoutMinutes} minutes", recoveryOptions.TimeoutMinutes);
    logger.LogInformation("  Maximum Recoveries                  : {MaxRecoveriesPerRun}", recoveryOptions.MaxRecoveriesPerRun);
}

// AI12.OBS.6: lightweight, read-only platform health endpoints (minimal API — not controllers).
// None of these execute recognition or load ONNX models; they only perform File.Exists/FileInfo
// checks, a DB "SELECT 1"-equivalent (Database.CanConnectAsync), and the storage provider's own
// existing CheckHealthAsync probe (already used by MediaStorageService). Bodies are RFC7807
// ProblemDetails ("application/problem+json") with the diagnostic snapshot as an extension member.
static void MapPlatformHealthEndpoints(WebApplication app)
{
    app.MapGet("/health/live", () =>
    {
        var problem = new ProblemDetails
        {
            Type = "https://abhyanvaya.app/health/live",
            Title = "Live",
            Status = StatusCodes.Status200OK,
            Detail = "The Abhyanvaya API process is running.",
        };
        return Results.Json(problem, statusCode: problem.Status, contentType: "application/problem+json");
    });

    app.MapGet("/health/ready", async (HttpContext httpContext, CancellationToken cancellationToken) =>
    {
        var services = httpContext.RequestServices;
        using var scope = services.CreateScope();

        var databaseReachable = await IsDatabaseReachableAsync(scope.ServiceProvider, cancellationToken);
        var storageReachable = await IsStorageReachableAsync(scope.ServiceProvider, cancellationToken);

        var insightFaceOptions = services.GetRequiredService<IOptions<InsightFaceOptions>>().Value;
        var modelReport = ModelAvailabilityChecker.Check(insightFaceOptions, app.Environment);

        var recognitionWorker = BackgroundWorkerInspector.Inspect(services, typeof(ClassroomRecognitionBackgroundService));
        var embeddingWorker = BackgroundWorkerInspector.Inspect(services, typeof(StudentFaceEmbeddingBackgroundService));

        var queueAvailable = IsRecognitionQueueAvailable(scope.ServiceProvider);

        // AI14.RUNTIME.4: runtime-only recovery sweep counters — no persistence, no new endpoint;
        // read from the same singleton the background service itself writes to.
        var recoveryOptions = services.GetRequiredService<IOptions<AttendanceSessionRecoveryOptions>>().Value;
        var recoveryMetrics = scope.ServiceProvider.GetRequiredService<IAttendanceSessionRecoveryMetrics>().GetSnapshot();
        var recovery = BuildRecoverySnapshot(recoveryOptions, recoveryMetrics);

        // AI15.DIAGNOSTICS.1 Task 9: metadata-only recognition pipeline memory forensics.
        var recognitionDiagnosticsSummary = scope.ServiceProvider.GetRequiredService<IRecognitionDiagnosticsStore>().GetLast();
        var recognitionDiagnostics = BuildRecognitionDiagnosticsSnapshot(recognitionDiagnosticsSummary);

        var isReady = databaseReachable && storageReachable && modelReport.AllModelsPresent
            && recognitionWorker.Running && embeddingWorker.Running && queueAvailable;

        // AI12.OBS.8/9: configuration issues are exposed as metadata only — they do not factor
        // into `isReady` (readiness semantics are unchanged from AI12.OBS.6). Recovery sweep health
        // (AI14.RUNTIME.4) and recognition memory diagnostics (AI15.DIAGNOSTICS.1) are likewise
        // metadata-only and do not affect readiness.
        var configurationIssues = ConfigurationValidator.Validate(app, modelReport);

        var problem = new ProblemDetails
        {
            Type = "https://abhyanvaya.app/health/ready",
            Title = isReady ? "Ready" : "NotReady",
            Status = isReady ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable,
            Detail = isReady
                ? "All readiness checks passed."
                : "One or more readiness checks failed; see the 'checks' extension for details.",
        };
        problem.Extensions["checks"] = new Dictionary<string, object?>
        {
            ["database"] = databaseReachable ? "Reachable" : "Unreachable",
            ["storage"] = storageReachable ? "Reachable" : "Unreachable",
            ["modelsPresent"] = modelReport.AllModelsPresent,
            ["recognitionWorkerStarted"] = recognitionWorker.Running,
            ["embeddingWorkerStarted"] = embeddingWorker.Running,
            ["queueAvailable"] = queueAvailable,
            ["configurationIssues"] = ToConfigurationIssueSummaries(configurationIssues),
            ["recovery"] = recovery,
            ["recognitionDiagnostics"] = recognitionDiagnostics,
        };

        return Results.Json(problem, statusCode: problem.Status, contentType: "application/problem+json");
    });

    app.MapGet("/health", async (HttpContext httpContext, CancellationToken cancellationToken) =>
    {
        var services = httpContext.RequestServices;
        using var scope = services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var databaseReachable = await IsDatabaseReachableAsync(scope.ServiceProvider, cancellationToken);
        var dbProviderDisplayName = StartupDiagnostics.DescribeDatabaseProvider(dbContext.Database.ProviderName);

        var storageProvider = scope.ServiceProvider.GetRequiredService<IStorageProviderFactory>().GetActiveProvider();
        var storageReachable = await IsStorageReachableAsync(scope.ServiceProvider, cancellationToken);

        var recognitionEngineName = scope.ServiceProvider.GetRequiredService<IFaceDetectionService>().ProviderName;
        var insightFaceOptions = services.GetRequiredService<IOptions<InsightFaceOptions>>().Value;
        var modelReport = ModelAvailabilityChecker.Check(insightFaceOptions, app.Environment);

        var recognitionWorker = BackgroundWorkerInspector.Inspect(services, typeof(ClassroomRecognitionBackgroundService));
        var embeddingWorker = BackgroundWorkerInspector.Inspect(services, typeof(StudentFaceEmbeddingBackgroundService));

        var classroomQueue = scope.ServiceProvider.GetRequiredService<IClassroomPhotoQueue>();
        var embeddingQueue = scope.ServiceProvider.GetRequiredService<IStudentPhotoEmbeddingQueue>();

        var useRedis = app.Configuration.GetValue<bool>("UseRedis");
        var cacheProviderDisplay = useRedis ? "Redis" : "Memory";

        // AI14.RUNTIME.4: runtime-only recovery sweep counters — no persistence, no new endpoint;
        // read from the same singleton the background service itself writes to.
        var recoveryOptions = services.GetRequiredService<IOptions<AttendanceSessionRecoveryOptions>>().Value;
        var recoveryMetrics = scope.ServiceProvider.GetRequiredService<IAttendanceSessionRecoveryMetrics>().GetSnapshot();
        var recovery = BuildRecoverySnapshot(recoveryOptions, recoveryMetrics);

        // AI15.DIAGNOSTICS.1 Task 9: metadata-only recognition pipeline memory forensics.
        var recognitionDiagnosticsSummary = scope.ServiceProvider.GetRequiredService<IRecognitionDiagnosticsStore>().GetLast();
        var recognitionDiagnostics = BuildRecognitionDiagnosticsSnapshot(recognitionDiagnosticsSummary);

        var overallHealthy = databaseReachable && storageReachable && modelReport.AllModelsPresent
            && recognitionWorker.Health == "Healthy" && embeddingWorker.Health == "Healthy";

        // AI12.OBS.8/9: configuration issues are exposed as metadata only — they do not factor
        // into `overallStatus` (health semantics are unchanged from AI12.OBS.6).
        var configurationIssues = ConfigurationValidator.Validate(app, modelReport);

        var snapshot = new
        {
            environment = app.Environment.EnvironmentName,
            version = StartupDiagnostics.ResolveApplicationVersion(),
            database = new { provider = dbProviderDisplayName, status = databaseReachable ? "Healthy" : "Unhealthy" },
            storage = new { provider = storageProvider.DisplayName, status = storageReachable ? "Healthy" : "Unhealthy" },
            recognitionEngine = recognitionEngineName,
            modelDirectory = new
            {
                configured = modelReport.ConfiguredModelDirectory,
                resolved = modelReport.ResolvedModelDirectory,
                exists = modelReport.ModelDirectoryExists,
            },
            detectionModel = new
            {
                fileName = modelReport.Detection.FileName,
                status = modelReport.Detection.Found ? "Found" : "Missing",
                sizeMB = modelReport.Detection.SizeMegabytes,
            },
            embeddingModel = new
            {
                fileName = modelReport.Embedding.FileName,
                status = modelReport.Embedding.Found ? "Found" : "Missing",
                sizeMB = modelReport.Embedding.SizeMegabytes,
            },
            recognitionWorker = new { status = recognitionWorker.Health, running = recognitionWorker.Running },
            embeddingWorker = new { status = embeddingWorker.Health, running = embeddingWorker.Running },
            backgroundQueue = new
            {
                classroomPhotoQueueDepth = classroomQueue.Count,
                embeddingQueueDepth = embeddingQueue.Count,
            },
            cache = new { provider = cacheProviderDisplay },
            recovery,
            recognitionDiagnostics,
            overallStatus = overallHealthy ? "Healthy" : "Degraded",
            configurationIssues = ToConfigurationIssueSummaries(configurationIssues),
        };

        var problem = new ProblemDetails
        {
            Type = "https://abhyanvaya.app/health",
            Title = snapshot.overallStatus,
            Status = StatusCodes.Status200OK,
            Detail = "Abhyanvaya platform health snapshot.",
        };
        problem.Extensions["health"] = snapshot;

        return Results.Json(problem, statusCode: problem.Status, contentType: "application/problem+json");
    });
}

static async Task<bool> IsDatabaseReachableAsync(IServiceProvider services, CancellationToken cancellationToken)
{
    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        return await dbContext.Database.CanConnectAsync(cancellationToken);
    }
    catch
    {
        return false;
    }
}

static async Task<bool> IsStorageReachableAsync(IServiceProvider services, CancellationToken cancellationToken)
{
    try
    {
        var provider = services.GetRequiredService<IStorageProviderFactory>().GetActiveProvider();
        var result = await provider.CheckHealthAsync(cancellationToken);
        return result.Ok;
    }
    catch
    {
        return false;
    }
}

// AI14.RUNTIME.4: shared JSON projection of the recovery sweep's runtime counters for both /health
// and /health/ready, so both surfaces report identical values from the same
// IAttendanceSessionRecoveryMetrics singleton instead of duplicating the derivation logic.
// Status is metadata-only — it never factors into /health's overallStatus or /health/ready's isReady.
static object BuildRecoverySnapshot(
    AttendanceSessionRecoveryOptions recoveryOptions,
    AttendanceSessionRecoveryMetricsSnapshot metrics)
{
    var status = !recoveryOptions.Enabled
        ? "Disabled"
        : metrics.PendingRecoveries > 0
            ? "Degraded"
            : "Healthy";

    return new
    {
        status,
        runs = metrics.RecoveryRuns,
        recoveredSessions = metrics.RecoveredSessions,
        lastRecoveryUtc = metrics.LastRecoveryUtc,
        lastDurationMs = metrics.LastRecoveryDurationMs,
        pendingRecoveries = metrics.PendingRecoveries,
    };
}

// AI15.DIAGNOSTICS.1 Task 9: shared JSON projection of the last completed/failed classroom
// recognition job's memory forensics summary, for both /health and /health/ready. Metadata only —
// it never factors into /health's overallStatus or /health/ready's isReady, and reading it can never
// throw or fail health (IRecognitionDiagnosticsStore.GetLast() is a simple in-memory read, returning
// null until the first classroom recognition job completes since process start).
static object BuildRecognitionDiagnosticsSnapshot(RecognitionDiagnosticsSummary? summary)
{
    if (summary is null)
    {
        return new { status = "NoDataYet", lastRecognition = (object?)null };
    }

    return new
    {
        status = summary.Failed ? "LastRunFailed" : "Healthy",
        lastRecognition = new
        {
            attendanceSessionId = summary.AttendanceSessionId,
            startedUtc = summary.StartedUtc,
            completedUtc = summary.CompletedUtc,
            peakWorkingSetMB = Math.Round(summary.PeakWorkingSetBytes / (1024d * 1024d), 1),
            peakManagedHeapMB = Math.Round(summary.PeakManagedHeapBytes / (1024d * 1024d), 1),
            peakPrivateMemoryMB = Math.Round(summary.PeakPrivateBytes / (1024d * 1024d), 1),
            peakStage = summary.PeakStage,
            peakFace = summary.PeakFace,
            lastStage = summary.LastStage,
            lastFace = summary.LastFace,
            recognitionDurationMs = summary.DurationMs,
            completed = summary.Completed,
            failed = summary.Failed,
            // AI15.DIAGNOSTICS.2B/2C: sourced from InsightFaceOptions.PipelineVersion (via the
            // per-job RecognitionDiagnosticsSummary, never re-read/hardcoded here) and the scoped
            // IRecognitionExecutionContext that was active for that job.
            pipelineVersion = summary.PipelineVersion,
            executionTraceId = summary.ExecutionTraceId,
            recognitionAttempt = summary.RecognitionAttempt,
            // AI16.RUNTIME.4: native-memory estimate and largest single-step Working Set jump — see
            // RecognitionMemorySnapshot.NativeEstimateBytes for the estimation method. Metadata only.
            peakNativeEstimateMB = Math.Round(summary.PeakNativeEstimateBytes / (1024d * 1024d), 1),
            peakWorkingSetDeltaMB = Math.Round(summary.PeakWorkingSetDeltaBytes / (1024d * 1024d), 1),
        },
    };
}

// AI12.OBS.8/9: shared JSON projection of ConfigurationIssue for both /health and /health/ready,
// so severity + category are exposed identically wherever configuration issues are surfaced.
static object ToConfigurationIssueSummaries(IReadOnlyList<ConfigurationIssue> issues) => new
{
    critical = issues.Count(i => i.Severity == ConfigurationSeverity.Critical),
    warning = issues.Count(i => i.Severity == ConfigurationSeverity.Warning),
    information = issues.Count(i => i.Severity == ConfigurationSeverity.Information),
    items = issues.Select(i => new
    {
        severity = i.Severity.ToString(),
        category = i.Category.ToString(),
        configurationKey = i.ConfigurationKey,
        message = i.Message,
        suggestedFix = i.SuggestedFix,
    }),
};

static bool IsRecognitionQueueAvailable(IServiceProvider services)
{
    try
    {
        _ = services.GetRequiredService<IClassroomPhotoQueue>().Count;
        _ = services.GetRequiredService<IStudentPhotoEmbeddingQueue>().Count;
        return true;
    }
    catch
    {
        return false;
    }
}
