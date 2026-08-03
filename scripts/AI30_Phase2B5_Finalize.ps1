# AI30 Phase 2B.5 — copy created/modified deliverables into CursonModifiedFiles per-prompt folders.
$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$destRoot = "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\Phase 2B.5"

function Ensure-Dir([string]$path) {
    if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force -Path $path | Out-Null }
}

function Copy-Rel([string]$rel, [string]$promptFolder) {
    $src = Join-Path $root $rel
    if (-not (Test-Path $src)) { Write-Warning "Missing: $rel"; return }
    $targetDir = Join-Path $destRoot (Join-Path $promptFolder (Split-Path $rel -Parent))
    Ensure-Dir $targetDir
    if ((Get-Item $src).PSIsContainer) {
        Copy-Item -Recurse -Force $src (Join-Path $destRoot (Join-Path $promptFolder $rel))
    } else {
        Ensure-Dir (Split-Path (Join-Path $destRoot (Join-Path $promptFolder $rel)) -Parent)
        Copy-Item -Force $src (Join-Path $destRoot (Join-Path $promptFolder $rel))
    }
}

Ensure-Dir $destRoot

$prompts = @{
    "2B.5.1_ResolutionGuidance" = @(
        "Abhyanvaya.Application\Scheduling\Conflicts\Intelligence\ConflictResolutionModels.cs",
        "Abhyanvaya.Application\Scheduling\Conflicts\Intelligence\ConflictResolutionAdvisor.cs",
        "Abhyanvaya.Application\Scheduling\Conflicts\Intelligence\Providers",
        "Abhyanvaya.Application\DTOs\Scheduling\ConflictIntelligenceDtos.cs",
        "docs\AI30_PHASE2B5_CONFLICT_RESOLUTION_GUIDANCE.md"
    )
    "2B.5.2_ImpactAnalysis" = @(
        "Abhyanvaya.Application\Scheduling\Conflicts\Intelligence\ImpactModels.cs",
        "Abhyanvaya.Application\Scheduling\Conflicts\Intelligence\ImpactAnalyzer.cs",
        "Abhyanvaya.Domain\Enums\Scheduling\ImpactCategory.cs",
        "docs\AI30_PHASE2B5_IMPACT_ANALYSIS.md"
    )
    "2B.5.3_DependencyGraph" = @(
        "Abhyanvaya.Application\Scheduling\Conflicts\Intelligence\DependencyModels.cs",
        "Abhyanvaya.Application\Scheduling\Conflicts\Intelligence\ConflictDependencyAnalyzer.cs",
        "docs\AI30_PHASE2B5_DEPENDENCY_GRAPH.md"
    )
    "2B.5.4_RuleThresholds" = @(
        "Abhyanvaya.Application\Scheduling\Conflicts\Intelligence\ConflictRuleThresholds.cs",
        "Abhyanvaya.Application\Scheduling\Conflicts\Intelligence\ConflictRuleConfigurationService.cs",
        "Abhyanvaya.Domain\Entities\Scheduling\ConflictRuleThresholdSetting.cs",
        "Abhyanvaya.Infrastructure\Persistence\Configurations\Scheduling\ConflictIntelligenceConfiguration.cs",
        "abhyanvaya-ui\src\pages\setup\scheduling\conflicts\ConflictRuleThresholdsPage.tsx",
        "docs\AI30_PHASE2B5_RULE_THRESHOLDS.md",
        "Abhyanvaya.API\appsettings.json"
    )
    "2B.5.5_Analytics" = @(
        "Abhyanvaya.Application\Scheduling\Conflicts\Intelligence\ConflictAnalyticsService.cs",
        "abhyanvaya-ui\src\pages\setup\scheduling\conflicts\ConflictAnalyticsPage.tsx"
    )
    "2B.5.6_Explainability" = @(
        "Abhyanvaya.Application\Scheduling\Conflicts\Intelligence\ConflictExplainabilityService.cs"
    )
    "2B.5.7_Workspace" = @(
        "Abhyanvaya.Application\Scheduling\Conflicts\Intelligence\ConflictIntelligenceService.cs",
        "Abhyanvaya.API\Controllers\Scheduling\Phase2B5Controllers.cs",
        "abhyanvaya-ui\src\pages\setup\scheduling\conflicts\ConflictWorkspacePage.tsx",
        "abhyanvaya-ui\src\services\schedulingService.ts",
        "abhyanvaya-ui\src\routes\AppRoutes.tsx",
        "abhyanvaya-ui\src\pages\setup\scheduling\SchedulingHub.tsx"
    )
    "2B.5.8_AttendanceCompatibility" = @(
        "docs\AI30_PHASE2B5_ATTENDANCE_COMPATIBILITY.md",
        "Abhyanvaya.Application\Scheduling\Conflicts\AttendanceSessionResolver.cs"
    )
    "2B.5.9_Tests" = @(
        "Abhyanvaya.Application.UnitTests\Scheduling\Phase2B5"
    )
    "2B.5.10_ArchitectureReview" = @(
        "docs\AI30_PHASE2B5_ARCHITECTURE_REVIEW.md",
        "docs\AI30_PHASE2B5_IMPLEMENTATION_SUMMARY.md"
    )
    "_FULL" = @(
        "Abhyanvaya.Application\Scheduling\Conflicts",
        "Abhyanvaya.Application\DTOs\Scheduling\ConflictIntelligenceDtos.cs",
        "Abhyanvaya.API\Controllers\Scheduling\Phase2B5Controllers.cs",
        "Abhyanvaya.Domain\Entities\Scheduling\ConflictRuleThresholdSetting.cs",
        "Abhyanvaya.Domain\Enums\Scheduling\ImpactCategory.cs",
        "Abhyanvaya.Infrastructure\Persistence\Configurations\Scheduling\ConflictIntelligenceConfiguration.cs",
        "Abhyanvaya.Infrastructure\Persistence\Migrations\20260802092301_AI30_Phase2B5_ConflictIntelligence.cs",
        "Abhyanvaya.Infrastructure\Persistence\Migrations\20260802092301_AI30_Phase2B5_ConflictIntelligence.Designer.cs",
        "abhyanvaya-ui\src\pages\setup\scheduling\conflicts",
        "docs\AI30_PHASE2B5_CONFLICT_RESOLUTION_GUIDANCE.md",
        "docs\AI30_PHASE2B5_IMPACT_ANALYSIS.md",
        "docs\AI30_PHASE2B5_DEPENDENCY_GRAPH.md",
        "docs\AI30_PHASE2B5_RULE_THRESHOLDS.md",
        "docs\AI30_PHASE2B5_ATTENDANCE_COMPATIBILITY.md",
        "docs\AI30_PHASE2B5_ARCHITECTURE_REVIEW.md",
        "docs\AI30_PHASE2B5_IMPLEMENTATION_SUMMARY.md",
        "Abhyanvaya.Application.UnitTests\Scheduling\Phase2B5"
    )
}

foreach ($prompt in $prompts.Keys) {
    Ensure-Dir (Join-Path $destRoot $prompt)
    foreach ($rel in $prompts[$prompt]) {
        Copy-Rel $rel $prompt
    }
    Write-Host "Copied $prompt"
}

Write-Host "Done -> $destRoot"
