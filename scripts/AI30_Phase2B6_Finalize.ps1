$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$destRoot = "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI30 Phase 2B.6"

function Ensure-Dir([string]$path) { if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force -Path $path | Out-Null } }
function Copy-Rel([string]$rel, [string]$promptFolder) {
    $src = Join-Path $root $rel
    if (-not (Test-Path $src)) { Write-Warning "Missing: $rel"; return }
    $dest = Join-Path $destRoot (Join-Path $promptFolder $rel)
    Ensure-Dir (Split-Path $dest -Parent)
    if ((Get-Item $src).PSIsContainer) { Copy-Item -Recurse -Force $src $dest } else { Copy-Item -Force $src $dest }
}

Ensure-Dir $destRoot
$prompts = [ordered]@{
    "2B.6.1_Framework" = @(
        "Abhyanvaya.Application\Scheduling\Optimization\OptimizationContracts.cs",
        "Abhyanvaya.Application\Scheduling\Optimization\NoOpOptimizationStrategy.cs",
        "Abhyanvaya.Application\Scheduling\Optimization\OptimizationReadinessRegistration.cs",
        "docs\AI30_PHASE2B6_OPTIMIZATION_FRAMEWORK.md"
    )
    "2B.6.2_Scoring" = @("Abhyanvaya.Application\Scheduling\Optimization\Scoring")
    "2B.6.3_Simulation" = @("Abhyanvaya.Application\Scheduling\Optimization\Simulation")
    "2B.6.4_PreviewUI" = @(
        "abhyanvaya-ui\src\pages\setup\scheduling\optimization",
        "abhyanvaya-ui\src\routes\AppRoutes.tsx",
        "abhyanvaya-ui\src\pages\setup\scheduling\SchedulingHub.tsx",
        "abhyanvaya-ui\src\services\schedulingService.ts"
    )
    "2B.6.5_Metrics" = @("Abhyanvaya.Application\Scheduling\Optimization\Metrics")
    "2B.6.6_Telemetry" = @("Abhyanvaya.Application\Scheduling\Optimization\Telemetry")
    "2B.6.7_Plugins" = @("Abhyanvaya.Application\Scheduling\Optimization\Plugins")
    "2B.6.8_Attendance" = @("docs\AI30_PHASE2B6_ATTENDANCE_COMPATIBILITY.md")
    "2B.6.9_Tests" = @("Abhyanvaya.Application.UnitTests\Scheduling\Phase2B6")
    "2B.6.10_Review" = @(
        "docs\AI30_PHASE2B6_ARCHITECTURE_REVIEW.md",
        "docs\AI30_PHASE2B6_IMPLEMENTATION_SUMMARY.md"
    )
    "_FULL" = @(
        "Abhyanvaya.Application\Scheduling\Optimization",
        "Abhyanvaya.Application\DTOs\Scheduling\OptimizationReadinessDtos.cs",
        "Abhyanvaya.API\Controllers\Scheduling\Phase2B6Controllers.cs",
        "Abhyanvaya.Domain\Enums\Scheduling\OptimizationEnums.cs",
        "Abhyanvaya.Domain\Entities\Scheduling\OptimizationReadinessEntities.cs",
        "Abhyanvaya.Infrastructure\Persistence\Configurations\Scheduling\OptimizationReadinessConfiguration.cs",
        "abhyanvaya-ui\src\pages\setup\scheduling\optimization",
        "docs\AI30_PHASE2B6_OPTIMIZATION_FRAMEWORK.md",
        "docs\AI30_PHASE2B6_ATTENDANCE_COMPATIBILITY.md",
        "docs\AI30_PHASE2B6_ARCHITECTURE_REVIEW.md",
        "docs\AI30_PHASE2B6_IMPLEMENTATION_SUMMARY.md",
        "Abhyanvaya.Application.UnitTests\Scheduling\Phase2B6"
    )
}

foreach ($k in $prompts.Keys) {
    Ensure-Dir (Join-Path $destRoot $k)
    foreach ($rel in $prompts[$k]) { Copy-Rel $rel $k }
    Write-Host "Copied $k"
}

# Copy migration if present
Get-ChildItem (Join-Path $root "Abhyanvaya.Infrastructure\Persistence\Migrations\*Phase2B6*") -ErrorAction SilentlyContinue |
    ForEach-Object {
        Copy-Rel ("Abhyanvaya.Infrastructure\Persistence\Migrations\" + $_.Name) "_FULL"
        Copy-Rel ("Abhyanvaya.Infrastructure\Persistence\Migrations\" + $_.Name) "2B.6.10_Review"
    }

Write-Host "Done -> $destRoot"
