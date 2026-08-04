param(
  [Parameter(Mandatory = $false)]
  [ValidateSet("All","Prompt1","Prompt2","Prompt3","Prompt4","Prompt5","Prompt6","Prompt7","Prompt8","Prompt9","Prompt10","Prompt11","Prompt12")]
  [string]$Prompt = "All"
)

$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$destRoot = "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI30 Phase 3.5"

function Ensure-Dir([string]$path) { if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force -Path $path | Out-Null } }
function Copy-Rel([string]$rel, [string]$promptFolder) {
  $src = Join-Path $root $rel
  if (-not (Test-Path $src)) { Write-Warning "Missing: $rel"; return }
  $dest = Join-Path (Join-Path $destRoot $promptFolder) $rel
  Ensure-Dir (Split-Path $dest -Parent)
  if ((Get-Item $src).PSIsContainer) { Copy-Item -Recurse -Force $src $dest }
  else { Copy-Item -Force $src $dest }
}

$common = @(
  "Abhyanvaya.Application\Scheduling\Configuration",
  "Abhyanvaya.Application\DTOs\Scheduling\SchedulingConfigurationExperienceDtos.cs",
  "Abhyanvaya.Application\DependencyInjection.cs",
  "Abhyanvaya.API\Controllers\Scheduling\Phase35Controllers.cs",
  "abhyanvaya-ui\src\pages\setup\scheduling\schedulingCatalogConfig.tsx",
  "abhyanvaya-ui\src\pages\setup\scheduling\SchedulingHub.tsx",
  "abhyanvaya-ui\src\services\schedulingService.ts",
  "abhyanvaya-ui\src\routes\AppRoutes.tsx"
)

$map = @{
  "Prompt1" = $common
  "Prompt2" = $common + @(
    "abhyanvaya-ui\src\pages\setup\scheduling\MarkdownDocViewer.tsx",
    "abhyanvaya-ui\src\pages\setup\scheduling\SchedulingConfigurationGuidePage.tsx",
    "abhyanvaya-ui\public\docs\scheduling\configuration-guide.md",
    "abhyanvaya-ui\src\pages\setup\SetupHub.tsx"
  )
  "Prompt3" = $common + @(
    "abhyanvaya-ui\src\pages\setup\scheduling\SchedulingQuickStartWizardPage.tsx",
    "abhyanvaya-ui\public\docs\scheduling\quick-start.md"
  )
  "Prompt4" = $common
  "Prompt5" = $common
  "Prompt6" = $common
  "Prompt7" = $common + @("abhyanvaya-ui\src\pages\setup\scheduling\SchedulingDashboardPage.tsx")
  "Prompt8" = $common + @(
    "abhyanvaya-ui\src\pages\setup\scheduling\ModuleHelpDrawer.tsx",
    "abhyanvaya-ui\public\docs\scheduling\modules"
  )
  "Prompt9" = $common
  "Prompt10" = @(
    "docs\AI30_PHASE35_CONFIGURATION_GUIDE.md",
    "docs\AI30_PHASE35_ARCHITECTURE_REVIEW.md",
    "docs\AI30_PHASE35_IMPLEMENTATION_SUMMARY.md",
    "docs\AI30_PHASE35_VERIFICATION_REPORT.md",
    "scripts\AI30_Phase35_Copy.ps1"
  )
  "Prompt11" = @("Abhyanvaya.Application.UnitTests\Scheduling\Phase35") + $common
  "Prompt12" = @(
    "docs\AI30_PHASE35_ARCHITECTURE_REVIEW.md",
    "docs\AI30_PHASE35_IMPLEMENTATION_SUMMARY.md",
    "docs\AI30_PHASE35_VERIFICATION_REPORT.md"
  ) + $common
}

$targets = if ($Prompt -eq "All") {
  @("Prompt1","Prompt2","Prompt3","Prompt4","Prompt5","Prompt6","Prompt7","Prompt8","Prompt9","Prompt10","Prompt11","Prompt12")
} else { @($Prompt) }

Ensure-Dir $destRoot
foreach ($p in $targets) {
  Ensure-Dir (Join-Path $destRoot $p)
  foreach ($rel in $map[$p]) { Copy-Rel $rel $p }
  Write-Host "Copied -> $destRoot\$p"
}
Write-Host "Done."
