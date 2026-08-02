$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$targets = @(
  "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI30 Phase 3",
  "C:\Users\Rupesh Reddy\Desktop\Saviter\Abhyanvaya\AI Attandance\AI30 Phase 3"
)

function Ensure-Dir([string]$path) { if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force -Path $path | Out-Null } }
function Copy-Rel([string]$rel, [string]$destRoot, [string]$promptFolder) {
  $src = Join-Path $root $rel
  if (-not (Test-Path $src)) { Write-Warning "Missing: $rel"; return }
  $dest = Join-Path $destRoot (Join-Path $promptFolder $rel)
  Ensure-Dir (Split-Path $dest -Parent)
  if ((Get-Item $src).PSIsContainer) { Copy-Item -Recurse -Force $src $dest } else { Copy-Item -Force $src $dest }
}

$prompts = [ordered]@{
  "3.1_Engine" = @(
    "Abhyanvaya.Application\Scheduling\Optimization\Engine",
    "Abhyanvaya.Domain\Entities\Scheduling\OptimizationEngineEntities.cs",
    "Abhyanvaya.Domain\Enums\Scheduling\OptimizationEnums.cs"
  )
  "3.2_Greedy" = @("Abhyanvaya.Application\Scheduling\Optimization\Strategies\GreedyOptimizationStrategy.cs")
  "3.3_Workload" = @("Abhyanvaya.Application\Scheduling\Optimization\Strategies\FacultyWorkloadOptimizationStrategy.cs")
  "3.4_Room" = @("Abhyanvaya.Application\Scheduling\Optimization\Strategies\RoomOptimizationStrategy.cs")
  "3.5_Preference" = @("Abhyanvaya.Application\Scheduling\Optimization\Strategies\PreferenceOptimizationStrategy.cs")
  "3.6_Pipeline" = @(
    "Abhyanvaya.Application\Scheduling\Optimization\Pipeline",
    "Abhyanvaya.Application\Scheduling\Optimization\OptimizationReadinessRegistration.cs"
  )
  "3.7_Progress" = @(
    "Abhyanvaya.Application\Scheduling\Optimization\Progress",
    "Abhyanvaya.API\Hubs\OptimizationHub.cs",
    "Abhyanvaya.API\SignalR\OptimizationSignalRPublisher.cs",
    "Abhyanvaya.API\Program.cs"
  )
  "3.8_Comparison" = @("Abhyanvaya.Application\Scheduling\Optimization\Pipeline\OptimizationPipeline.cs")
  "3.9_Approval" = @("Abhyanvaya.Application\Scheduling\Optimization\Approval")
  "3.10_Dashboard" = @(
    "abhyanvaya-ui\src\pages\setup\scheduling\optimization\OptimizationDashboardPage.tsx",
    "abhyanvaya-ui\src\routes\AppRoutes.tsx",
    "abhyanvaya-ui\src\pages\setup\scheduling\SchedulingHub.tsx",
    "abhyanvaya-ui\src\services\schedulingService.ts",
    "Abhyanvaya.Application\Scheduling\Optimization\Dashboard",
    "Abhyanvaya.API\Controllers\Scheduling\Phase3Controllers.cs"
  )
  "3.11_Tests" = @("Abhyanvaya.Application.UnitTests\Scheduling\Phase3")
  "3.12_Docs" = @(
    "docs\AI30_PHASE3_ENTERPRISE_OPTIMIZATION_ENGINE.md",
    "docs\AI30_PHASE3_ARCHITECTURE_REVIEW.md",
    "docs\AI30_PHASE3_IMPLEMENTATION_SUMMARY.md"
  )
  "_FULL" = @(
    "Abhyanvaya.Application\Scheduling\Optimization\Engine",
    "Abhyanvaya.Application\Scheduling\Optimization\Strategies",
    "Abhyanvaya.Application\Scheduling\Optimization\Pipeline",
    "Abhyanvaya.Application\Scheduling\Optimization\Approval",
    "Abhyanvaya.Application\Scheduling\Optimization\Dashboard",
    "Abhyanvaya.Application\Scheduling\Optimization\Progress",
    "Abhyanvaya.Application\Scheduling\Optimization\OptimizationContracts.cs",
    "Abhyanvaya.Application\Scheduling\Optimization\OptimizationReadinessRegistration.cs",
    "Abhyanvaya.Application\Scheduling\Optimization\Sandbox\OptimizationSandboxService.cs",
    "Abhyanvaya.Application\DTOs\Scheduling\OptimizationSandboxDtos.cs",
    "Abhyanvaya.Application\Common\Interfaces\IApplicationDbContext.cs",
    "Abhyanvaya.Domain\Entities\Scheduling\OptimizationEngineEntities.cs",
    "Abhyanvaya.Domain\Enums\Scheduling\OptimizationEnums.cs",
    "Abhyanvaya.Infrastructure\Persistence\Configurations\Scheduling\OptimizationEngineConfiguration.cs",
    "Abhyanvaya.Infrastructure\Persistence\ApplicationDbContext.cs",
    "Abhyanvaya.API\Controllers\Scheduling\Phase3Controllers.cs",
    "Abhyanvaya.API\Hubs\OptimizationHub.cs",
    "Abhyanvaya.API\SignalR\OptimizationSignalRPublisher.cs",
    "abhyanvaya-ui\src\pages\setup\scheduling\optimization",
    "docs\AI30_PHASE3_ENTERPRISE_OPTIMIZATION_ENGINE.md",
    "docs\AI30_PHASE3_ARCHITECTURE_REVIEW.md",
    "docs\AI30_PHASE3_IMPLEMENTATION_SUMMARY.md",
    "Abhyanvaya.Application.UnitTests\Scheduling\Phase3"
  )
}

foreach ($destRoot in $targets) {
  Ensure-Dir $destRoot
  foreach ($k in $prompts.Keys) {
    Ensure-Dir (Join-Path $destRoot $k)
    foreach ($rel in $prompts[$k]) { Copy-Rel $rel $destRoot $k }
  }
  Get-ChildItem (Join-Path $root "Abhyanvaya.Infrastructure\Persistence\Migrations\*Phase3*") -ErrorAction SilentlyContinue |
    ForEach-Object {
      Copy-Rel ("Abhyanvaya.Infrastructure\Persistence\Migrations\" + $_.Name) $destRoot "_FULL"
      Copy-Rel ("Abhyanvaya.Infrastructure\Persistence\Migrations\" + $_.Name) $destRoot "3.12_Docs"
    }
  Write-Host "Done -> $destRoot"
}
