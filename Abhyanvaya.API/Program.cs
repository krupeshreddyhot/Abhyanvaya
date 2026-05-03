using Abhyanvaya.API.Common;
using Abhyanvaya.API.Services;
using Abhyanvaya.API.Common.Auth.Handlers;
using Abhyanvaya.API.Common.Auth.Requirements;
using Abhyanvaya.Application;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Infrastructure;
using Abhyanvaya.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
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
builder.Services.AddControllers();
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

// Add Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<IStudentService, StudentService>();
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
            .AllowAnyMethod();
    });
});

var portEnv = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(portEnv))
    builder.WebHost.UseUrls($"http://+:{portEnv}");

var app = builder.Build();

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

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Path.StartsWithSegments("/branding", StringComparison.OrdinalIgnoreCase))
            AddPublicBrandingHeaders(ctx);
    },
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
