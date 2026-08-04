param(
  [Parameter(Mandatory = $true)]
  [ValidateSet("Prompt1","Prompt2","Prompt3","Prompt4","Prompt5","Prompt6","Prompt7","Prompt8","Prompt9","Prompt10","All")]
  [string]$Prompt
)

$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$destRoot = "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI22.8.6"

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
  "Abhyanvaya.Application\AttendanceRecovery",
  "Abhyanvaya.Application\DTOs\AttendanceRecovery",
  "Abhyanvaya.Application\DependencyInjection.cs",
  "Abhyanvaya.Application\Common\Interfaces\IApplicationDbContext.cs",
  "Abhyanvaya.API\Controllers\AttendanceRecoveryController.cs",
  "Abhyanvaya.API\Controllers\AttendanceRecoveryAdminController.cs",
  "Abhyanvaya.Domain\Entities\AttendanceBulkOperationHistory.cs",
  "Abhyanvaya.Infrastructure\Persistence\ApplicationDbContext.cs",
  "Abhyanvaya.Infrastructure\Persistence\Configurations\AttendanceBulkOperationHistoryConfiguration.cs",
  "Abhyanvaya.Infrastructure\Persistence\Migrations\20260804090000_AI22_8_6_AttendanceBulkOperationHistory.cs",
  "abhyanvaya-ui\src\services\attendanceRecoveryService.ts",
  "abhyanvaya-ui\src\components\attendance-recovery"
)

$map = @{
  "Prompt1" = $common + @(
    "docs\AI22_8_6_SLA.md",
    "abhyanvaya-ui\src\components\attendance-recovery\PendingSessionCard.tsx"
  )
  "Prompt2" = $common + @(
    "abhyanvaya-ui\src\pages\setup\AttendanceRecoveryDashboardPage.tsx"
  )
  "Prompt3" = $common + @(
    "abhyanvaya-ui\src\components\attendance-recovery\SessionTimeline.tsx",
    "abhyanvaya-ui\src\pages\faculty\FacultyRecoveryCenterPage.tsx"
  )
  "Prompt4" = $common + @(
    "scripts\Apply_AI22_8_6_PolishSchema.sql",
    "abhyanvaya-ui\src\pages\setup\AttendanceRecoveryDashboardPage.tsx"
  )
  "Prompt5" = $common + @(
    "abhyanvaya-ui\src\pages\setup\AttendanceRecoveryDashboardPage.tsx"
  )
  "Prompt6" = $common + @(
    "abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx"
  )
  "Prompt7" = $common
  "Prompt8" = $common
  "Prompt9" = $common + @(
    "Abhyanvaya.Application.UnitTests\AttendanceRecovery\AI2286EnterpriseOperationsPolishTests.cs"
  )
  "Prompt10" = $common + @(
    "docs\AI22_8_6_ENTERPRISE_OPERATIONS_POLISH.md",
    "docs\AI22_8_6_SLA.md",
    "scripts\AI22_8_6_Copy.ps1",
    "scripts\Apply_AI22_8_6_PolishSchema.sql",
    "Abhyanvaya.Application.UnitTests\AttendanceRecovery\AI2286EnterpriseOperationsPolishTests.cs"
  )
}

$targets = if ($Prompt -eq "All") { @("Prompt1","Prompt2","Prompt3","Prompt4","Prompt5","Prompt6","Prompt7","Prompt8","Prompt9","Prompt10") } else { @($Prompt) }

Ensure-Dir $destRoot
foreach ($p in $targets) {
  Ensure-Dir (Join-Path $destRoot $p)
  foreach ($rel in $map[$p]) { Copy-Rel $rel $p }
  Write-Host "Copied -> $destRoot\$p"
}

Write-Host "Done."
