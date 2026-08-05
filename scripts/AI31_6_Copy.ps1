param(
  [Parameter(Mandatory = $false)]
  [ValidateSet("All","Prompt1","Prompt2","Prompt3","Prompt4","Prompt5","Prompt6","Prompt7","Prompt8","Prompt9","Prompt10","Prompt11","Prompt12")]
  [string]$Prompt = "All"
)

$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$destRoot = "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI31.6"

function Ensure-Dir([string]$path) { if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force -Path $path | Out-Null } }
function Copy-Rel([string]$rel, [string]$promptFolder) {
  $src = Join-Path $root $rel
  if (-not (Test-Path $src)) { Write-Warning "Missing: $rel"; return }
  $dest = Join-Path (Join-Path $destRoot $promptFolder) $rel
  Ensure-Dir (Split-Path $dest -Parent)
  if ((Get-Item $src).PSIsContainer) { Copy-Item -Recurse -Force $src $dest }
  else { Copy-Item -Force $src $dest }
}

$backend = @(
  "Abhyanvaya.Domain\Entities\Dashboards",
  "Abhyanvaya.Application\Dashboards",
  "Abhyanvaya.Application\DTOs\Dashboards",
  "Abhyanvaya.Application\DependencyInjection.cs",
  "Abhyanvaya.Application\Common\Interfaces\IApplicationDbContext.cs",
  "Abhyanvaya.Infrastructure\Persistence\ApplicationDbContext.cs",
  "Abhyanvaya.API\Controllers\EnterpriseDashboardController.cs",
  "scripts\Apply_AI31_6_DashboardSchema.sql"
)

$ui = @(
  "abhyanvaya-ui\src\services\enterpriseDashboardService.ts",
  "abhyanvaya-ui\src\components\dashboards",
  "abhyanvaya-ui\src\pages\dashboards",
  "abhyanvaya-ui\src\routes\AppRoutes.tsx",
  "abhyanvaya-ui\src\layouts\MainLayout.tsx"
)

$docs = @(
  "docs\AI31_6_ENTERPRISE_DASHBOARDS.md",
  "docs\AI31_6_FACULTY_DASHBOARD_GUIDE.md",
  "docs\AI31_6_ADMIN_DASHBOARD_GUIDE.md",
  "docs\AI31_6_OPERATIONAL_INTELLIGENCE_GUIDE.md",
  "docs\AI31_6_NOTIFICATION_CENTER_GUIDE.md",
  "docs\AI31_6_ARCHITECTURE_REVIEW.md",
  "docs\AI31_6_IMPLEMENTATION_SUMMARY.md",
  "docs\AI31_6_VERIFICATION_REPORT.md",
  "scripts\AI31_6_Copy.ps1"
)

$tests = @("Abhyanvaya.Application.UnitTests\Dashboards")
$all = $backend + $ui + $docs + $tests

$map = @{
  "Prompt1" = $backend + $ui
  "Prompt2" = $backend + $ui
  "Prompt3" = $backend + $ui
  "Prompt4" = $backend + $ui
  "Prompt5" = $backend + $ui
  "Prompt6" = $backend + $ui
  "Prompt7" = $backend + $ui
  "Prompt8" = $backend + $ui
  "Prompt9" = $backend + $ui
  "Prompt10" = $backend + $ui
  "Prompt11" = $tests + $backend
  "Prompt12" = $docs + $all
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
