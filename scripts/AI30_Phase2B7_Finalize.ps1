$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$targets = @(
  "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI30 Phase 2B.7",
  "C:\Users\Rupesh Reddy\Desktop\Saviter\Abhyanvaya\AI Attandance\AI30 Phase 2B.7"
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
  "2B.7.1_Domain" = @(
    "Abhyanvaya.Domain\Entities\Scheduling\OptimizationSandboxEntities.cs",
    "Abhyanvaya.Domain\Enums\Scheduling\OptimizationEnums.cs",
    "Abhyanvaya.Application\Scheduling\Optimization\Sandbox\OptimizationSandboxService.cs",
    "Abhyanvaya.Application\Common\Interfaces\Scheduling\IOptimizationScenarioRepository.cs",
    "Abhyanvaya.Infrastructure\Persistence\Repositories\Scheduling\OptimizationScenarioRepository.cs",
    "docs\AI30_PHASE2B7_OPTIMIZATION_SANDBOX.md"
  )
  "2B.7.2_Replay" = @("Abhyanvaya.Application\Scheduling\Optimization\Sandbox\SandboxSupportServices.cs")
  "2B.7.3_Comparison" = @("Abhyanvaya.Application\Scheduling\Optimization\Sandbox\SandboxSupportServices.cs")
  "2B.7.4_Favorites" = @("Abhyanvaya.Application\Scheduling\Optimization\Sandbox\OptimizationSandboxService.cs")
  "2B.7.5_WorkspaceUI" = @(
    "abhyanvaya-ui\src\pages\setup\scheduling\optimization\OptimizationWorkspacePage.tsx",
    "abhyanvaya-ui\src\routes\AppRoutes.tsx",
    "abhyanvaya-ui\src\pages\setup\scheduling\SchedulingHub.tsx",
    "abhyanvaya-ui\src\services\schedulingService.ts"
  )
  "2B.7.6_Collaboration" = @("Abhyanvaya.Application\Scheduling\Optimization\Sandbox\SandboxSupportServices.cs")
  "2B.7.7_History" = @("Abhyanvaya.Application\Scheduling\Optimization\Sandbox\SandboxSupportServices.cs")
  "2B.7.8_MetricsEvolution" = @("Abhyanvaya.Application\Scheduling\Optimization\Sandbox\SandboxSupportServices.cs")
  "2B.7.9_Tests" = @("Abhyanvaya.Application.UnitTests\Scheduling\Phase2B7")
  "2B.7.10_Docs" = @(
    "docs\AI30_PHASE2B7_OPTIMIZATION_SANDBOX.md",
    "docs\AI30_PHASE2B7_ARCHITECTURE_REVIEW.md",
    "docs\AI30_PHASE2B7_IMPLEMENTATION_SUMMARY.md"
  )
  "_FULL" = @(
    "Abhyanvaya.Application\Scheduling\Optimization\Sandbox",
    "Abhyanvaya.Application\DTOs\Scheduling\OptimizationSandboxDtos.cs",
    "Abhyanvaya.API\Controllers\Scheduling\Phase2B7Controllers.cs",
    "Abhyanvaya.Domain\Entities\Scheduling\OptimizationSandboxEntities.cs",
    "Abhyanvaya.Infrastructure\Persistence\Configurations\Scheduling\OptimizationSandboxConfiguration.cs",
    "Abhyanvaya.Infrastructure\Persistence\Repositories\Scheduling\OptimizationScenarioRepository.cs",
    "abhyanvaya-ui\src\pages\setup\scheduling\optimization",
    "docs\AI30_PHASE2B7_OPTIMIZATION_SANDBOX.md",
    "docs\AI30_PHASE2B7_ARCHITECTURE_REVIEW.md",
    "docs\AI30_PHASE2B7_IMPLEMENTATION_SUMMARY.md",
    "Abhyanvaya.Application.UnitTests\Scheduling\Phase2B7"
  )
}

foreach ($destRoot in $targets) {
  Ensure-Dir $destRoot
  foreach ($k in $prompts.Keys) {
    Ensure-Dir (Join-Path $destRoot $k)
    foreach ($rel in $prompts[$k]) { Copy-Rel $rel $destRoot $k }
  }
  Get-ChildItem (Join-Path $root "Abhyanvaya.Infrastructure\Persistence\Migrations\*Phase2B7*") -ErrorAction SilentlyContinue |
    ForEach-Object {
      Copy-Rel ("Abhyanvaya.Infrastructure\Persistence\Migrations\" + $_.Name) $destRoot "_FULL"
      Copy-Rel ("Abhyanvaya.Infrastructure\Persistence\Migrations\" + $_.Name) $destRoot "2B.7.10_Docs"
    }
  Write-Host "Done -> $destRoot"
}
