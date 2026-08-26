using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Authorization;

/// <summary>
/// AI29.1D.24B.3A — Idempotent Allocation.* <b>permission catalog</b> provisioning only.
/// Does <b>not</b> assign permissions to ADMIN (or any role). Role assignment is seed/SQL/RBAC API.
/// </summary>
public sealed class AllocationPermissionCatalogReconciler : IHostedService
{
    private static readonly (int Id, string Key, string Action)[] AllocationPermissions =
    [
        (227, PermissionKeys.AllocationRun, "Run"),
        (228, PermissionKeys.AllocationApprove, "Approve"),
        (229, PermissionKeys.AllocationOperationsView, "OperationsView"),
        (230, PermissionKeys.AllocationScenarioView, "ScenarioView"),
        (231, PermissionKeys.AllocationScenarioCreate, "ScenarioCreate"),
        (232, PermissionKeys.AllocationScenarioCompare, "ScenarioCompare"),
        (233, PermissionKeys.AllocationScenarioReplay, "ScenarioReplay"),
        (234, PermissionKeys.AllocationScenarioReview, "ScenarioReview"),
        (235, PermissionKeys.AllocationReject, "Reject"),
        (236, PermissionKeys.AllocationExport, "Export"),
        (237, PermissionKeys.AllocationScenarioArchive, "ScenarioArchive"),
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AllocationPermissionCatalogReconciler> _logger;

    public AllocationPermissionCatalogReconciler(
        IServiceScopeFactory scopeFactory,
        ILogger<AllocationPermissionCatalogReconciler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var insertedPermissions = 0;

            var existingKeys = await db.Set<Permission>().AsNoTracking()
                .Select(p => p.Key)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var keySet = existingKeys.ToHashSet(StringComparer.Ordinal);

            foreach (var (id, key, action) in AllocationPermissions)
            {
                if (keySet.Contains(key))
                    continue;

                var idTaken = await db.Set<Permission>().AsNoTracking()
                    .AnyAsync(p => p.Id == id, cancellationToken)
                    .ConfigureAwait(false);

                if (!idTaken)
                {
                    db.Set<Permission>().Add(new Permission
                    {
                        Id = id,
                        Key = key,
                        Resource = "Allocation",
                        Action = action
                    });
                }
                else
                {
                    db.Set<Permission>().Add(new Permission
                    {
                        Key = key,
                        Resource = "Allocation",
                        Action = action
                    });
                }

                insertedPermissions++;
                keySet.Add(key);
            }

            if (insertedPermissions > 0)
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "AI29.1D.24B.3A Allocation permission catalog: inserted {PermissionCount} catalog rows (no role grants).",
                    insertedPermissions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI29.1D.24B.3A Allocation permission catalog reconciliation failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
