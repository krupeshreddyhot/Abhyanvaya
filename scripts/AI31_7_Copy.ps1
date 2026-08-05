param(
  [Parameter(Mandatory = $false)]
  [ValidateSet("All","Prompt1","Prompt2","Prompt3","Prompt4","Prompt5","Prompt6","Prompt7","Prompt8","Prompt9","Prompt10","Prompt11","Prompt12")]
  [string]$Prompt = "All"
)

$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$destRoot = "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI31.7"

function Ensure-Dir([string]$path) { if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force -Path $path | Out-Null } }
function Copy-Rel([string]$rel, [string]$promptFolder) {
  $src = Join-Path $root $rel
  if (-not (Test-Path $src)) { Write-Warning "Missing: $rel"; return }
  $dest = Join-Path (Join-Path $destRoot $promptFolder) $rel
  Ensure-Dir (Split-Path $dest -Parent)
  if ((Get-Item $src).PSIsContainer) { Copy-Item -Recurse -Force $src $dest }
  else { Copy-Item -Force $src $dest }
}

$core = @(
  "Abhyanvaya.Application\Dashboards\OperationsCommandCenterService.cs",
  "Abhyanvaya.Application\Dashboards\DashboardWidgetCatalog.cs",
  "Abhyanvaya.Application\DTOs\Dashboards\OperationsCommandCenterDtos.cs",
  "Abhyanvaya.Application\DTOs\Dashboards\EnterpriseDashboardDtos.cs",
  "Abhyanvaya.Application\DependencyInjection.cs",
  "Abhyanvaya.API\Controllers\EnterpriseDashboardController.cs",
  "abhyanvaya-ui\src\services\enterpriseDashboardService.ts",
  "abhyanvaya-ui\src\components\dashboards\DashboardWidgets.tsx",
  "abhyanvaya-ui\src\pages\dashboards\AdminOperationsDashboardPage.tsx",
  "abhyanvaya-ui\src\utils\dashboardNavigation.ts"
)

$docs = @(
  "docs\AI31_7_ENTERPRISE_OPERATIONS_COMMAND_CENTER.md",
  "docs\AI31_7_IMPLEMENTATION_SUMMARY.md",
  "docs\AI31_7_ARCHITECTURE_REVIEW.md",
  "scripts\AI31_7_Copy.ps1"
)

$tests = @("Abhyanvaya.Application.UnitTests\Dashboards\AI31_7_OperationsCommandCenterTests.cs")

$map = @{
  "Prompt1" = $core
  "Prompt2" = $core
  "Prompt3" = $core
  "Prompt4" = $core
  "Prompt5" = $core
  "Prompt6" = $core
  "Prompt7" = $core
  "Prompt8" = $core
  "Prompt9" = $core
  "Prompt10" = $core
  "Prompt11" = $tests + $core
  "Prompt12" = $docs + $core + $tests
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
