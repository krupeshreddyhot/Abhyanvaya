$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$targets = @("D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI31.5")

function Ensure-Dir([string]$path) { if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force -Path $path | Out-Null } }
function Copy-Rel([string]$rel, [string]$destRoot, [string]$promptFolder) {
  $src = Join-Path $root $rel
  if (-not (Test-Path $src)) { Write-Warning "Missing: $rel"; return }
  $dest = Join-Path $destRoot (Join-Path $promptFolder $rel)
  Ensure-Dir (Split-Path $dest -Parent)
  if ((Get-Item $src).PSIsContainer) { Copy-Item -Recurse -Force $src $dest } else { Copy-Item -Force $src $dest }
}

$prompts = [ordered]@{
  "31.5.1_Calendar" = @(
    "Abhyanvaya.Application\Faculty\FacultyEnhancementServices.cs",
    "Abhyanvaya.API\Controllers\FacultyWorkspaceEnhancementController.cs",
    "docs\AI31_5_CALENDAR_INTEGRATION.md",
    "abhyanvaya-ui\src\pages\faculty\FacultyWorkspaceEnhancements.tsx",
    "abhyanvaya-ui\src\services\facultyWorkspaceService.ts"
  )
  "31.5.2_Timeline" = @(
    "Abhyanvaya.Application\Faculty\FacultyEnhancementServices.cs",
    "abhyanvaya-ui\src\pages\faculty\FacultyWorkspaceEnhancements.tsx",
    "abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx"
  )
  "31.5.3_Navigation" = @(
    "Abhyanvaya.Application\Faculty\FacultyEnhancementServices.cs",
    "docs\AI31_5_CLASSROOM_NAVIGATION.md",
    "abhyanvaya-ui\src\pages\faculty\FacultyWorkspaceEnhancements.tsx"
  )
  "31.5.4_Preferences" = @(
    "Abhyanvaya.Domain\Entities\Scheduling\WorkspacePreference.cs",
    "Abhyanvaya.Application\Faculty\WorkspacePreferenceService.cs",
    "Abhyanvaya.Application\DTOs\Faculty\FacultyEnhancementDtos.cs",
    "Abhyanvaya.Infrastructure\Persistence\Configurations\Scheduling\WorkspacePreferenceConfiguration.cs"
  )
  "31.5.5_AttendanceProductivity" = @("Abhyanvaya.Application\Faculty\FacultyEnhancementServices.cs")
  "31.5.6_ProductivityDashboard" = @(
    "Abhyanvaya.Application\Faculty\FacultyEnhancementServices.cs",
    "abhyanvaya-ui\src\pages\faculty\FacultyProductivityCharts.tsx"
  )
  "31.5.7_SmartNotifications" = @("Abhyanvaya.Application\Faculty\FacultyEnhancementServices.cs")
  "31.5.8_Search" = @("Abhyanvaya.Application\Faculty\FacultyEnhancementServices.cs")
  "31.5.9_PerformanceA11y" = @(
    "abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx",
    "abhyanvaya-ui\src\pages\faculty\FacultyWorkspaceEnhancements.tsx"
  )
  "31.5.10_Mobile" = @("abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx")
  "31.5.11_Tests" = @("Abhyanvaya.Application.UnitTests\Faculty\AI315FacultyEnhancementTests.cs")
  "31.5.12_Docs" = @(
    "docs\AI31_5_ENTERPRISE_FACULTY_ENHANCEMENTS.md",
    "docs\AI31_5_ARCHITECTURE_REVIEW.md",
    "docs\AI31_5_IMPLEMENTATION_SUMMARY.md",
    "docs\AI31_5_CALENDAR_INTEGRATION.md",
    "docs\AI31_5_CLASSROOM_NAVIGATION.md"
  )
  "_FULL" = @(
    "Abhyanvaya.Application\Faculty",
    "Abhyanvaya.Application\DTOs\Faculty",
    "Abhyanvaya.Application\DependencyInjection.cs",
    "Abhyanvaya.Application\Common\Interfaces\IApplicationDbContext.cs",
    "Abhyanvaya.Domain\Entities\Scheduling\WorkspacePreference.cs",
    "Abhyanvaya.Infrastructure\Persistence\ApplicationDbContext.cs",
    "Abhyanvaya.Infrastructure\Persistence\Configurations\Scheduling\WorkspacePreferenceConfiguration.cs",
    "Abhyanvaya.Infrastructure\Persistence\Migrations\20260803041051_AI31_5_WorkspacePreference.cs",
    "Abhyanvaya.Infrastructure\Persistence\Migrations\20260803041051_AI31_5_WorkspacePreference.Designer.cs",
    "Abhyanvaya.API\Controllers\FacultyWorkspaceEnhancementController.cs",
    "abhyanvaya-ui\src\pages\faculty",
    "abhyanvaya-ui\src\services\facultyWorkspaceService.ts",
    "Abhyanvaya.Application.UnitTests\Faculty",
    "docs\AI31_5_ENTERPRISE_FACULTY_ENHANCEMENTS.md",
    "docs\AI31_5_ARCHITECTURE_REVIEW.md",
    "docs\AI31_5_IMPLEMENTATION_SUMMARY.md",
    "docs\AI31_5_CALENDAR_INTEGRATION.md",
    "docs\AI31_5_CLASSROOM_NAVIGATION.md"
  )
}

foreach ($destRoot in $targets) {
  Ensure-Dir $destRoot
  foreach ($k in $prompts.Keys) {
    Ensure-Dir (Join-Path $destRoot $k)
    foreach ($rel in $prompts[$k]) { Copy-Rel $rel $destRoot $k }
  }
  Get-ChildItem (Join-Path $root "Abhyanvaya.Infrastructure\Persistence\Migrations\*AI31_5*") -ErrorAction SilentlyContinue |
    ForEach-Object {
      Copy-Rel ("Abhyanvaya.Infrastructure\Persistence\Migrations\" + $_.Name) $destRoot "_FULL"
      Copy-Rel ("Abhyanvaya.Infrastructure\Persistence\Migrations\" + $_.Name) $destRoot "31.5.12_Docs"
    }
  Write-Host "Done -> $destRoot"
}
