param(
  [Parameter(Mandatory = $true)]
  [ValidateSet("Prompt1","Prompt2","Prompt3","Prompt4","Prompt5","Prompt6","Prompt7","Prompt8","Prompt9","Prompt10","All")]
  [string]$Prompt
)

$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$destRoot = "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI22.8.5"

function Ensure-Dir([string]$path) { if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force -Path $path | Out-Null } }
function Copy-Rel([string]$rel, [string]$promptFolder) {
  $src = Join-Path $root $rel
  if (-not (Test-Path $src)) { Write-Warning "Missing: $rel"; return }
  $dest = Join-Path (Join-Path $destRoot $promptFolder) $rel
  Ensure-Dir (Split-Path $dest -Parent)
  if ((Get-Item $src).PSIsContainer) { Copy-Item -Recurse -Force $src $dest }
  else { Copy-Item -Force $src $dest }
}

$commonBackend = @(
  "Abhyanvaya.Application\AttendanceRecovery",
  "Abhyanvaya.Application\DTOs\AttendanceRecovery",
  "Abhyanvaya.Application\DependencyInjection.cs",
  "Abhyanvaya.Application\Common\Interfaces\IApplicationDbContext.cs",
  "Abhyanvaya.API\Controllers\AttendanceRecoveryController.cs",
  "Abhyanvaya.API\Controllers\AttendanceRecoveryAdminController.cs",
  "Abhyanvaya.Infrastructure\Persistence\ApplicationDbContext.cs",
  "Abhyanvaya.Infrastructure\DependencyInjection.cs",
  "abhyanvaya-ui\src\services\attendanceRecoveryService.ts"
)

$map = @{
  "Prompt1" = $commonBackend + @(
    "abhyanvaya-ui\src\components\attendance-recovery\PendingSessionCard.tsx",
    "abhyanvaya-ui\src\pages\faculty\FacultyPendingAttendancePanel.tsx"
  )
  "Prompt2" = $commonBackend + @(
    "Abhyanvaya.Application\AttendanceRecovery\AttendanceSessionPriorityEngine.cs",
    "abhyanvaya-ui\src\components\attendance-recovery\PendingSessionCard.tsx",
    "abhyanvaya-ui\src\pages\faculty\FacultyPendingAttendancePanel.tsx"
  )
  "Prompt3" = $commonBackend + @(
    "Abhyanvaya.Domain\Entities\AttendanceRecoveryPreference.cs",
    "Abhyanvaya.Infrastructure\Persistence\Configurations\AttendanceRecoveryPreferenceConfiguration.cs",
    "Abhyanvaya.Infrastructure\Persistence\Migrations\20260803180000_AI22_8_5_AttendanceRecoveryPreference.cs",
    "abhyanvaya-ui\src\pages\faculty\FacultyRecoveryCenterPage.tsx"
  )
  "Prompt4" = $commonBackend + @(
    "abhyanvaya-ui\src\pages\faculty\FacultyRecoveryCenterPage.tsx",
    "abhyanvaya-ui\src\routes\AppRoutes.tsx",
    "abhyanvaya-ui\src\components\attendance-recovery\PendingSessionCard.tsx"
  )
  "Prompt5" = $commonBackend + @(
    "abhyanvaya-ui\src\pages\setup\AttendanceRecoveryDashboardPage.tsx"
  )
  "Prompt6" = $commonBackend + @(
    "abhyanvaya-ui\src\pages\setup\AttendanceRecoveryDashboardPage.tsx"
  )
  "Prompt7" = $commonBackend + @(
    "Abhyanvaya.Infrastructure\BackgroundWorkers\AttendanceHealthMonitorHostedService.cs",
    "abhyanvaya-ui\src\pages\setup\AttendanceRecoveryDashboardPage.tsx"
  )
  "Prompt8" = $commonBackend + @(
    "abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx",
    "abhyanvaya-ui\src\components\attendance-recovery\PendingSessionCard.tsx",
    "abhyanvaya-ui\src\pages\faculty\FacultyPendingAttendancePanel.tsx",
    "abhyanvaya-ui\src\routes\AppRoutes.tsx"
  )
  "Prompt9" = @(
    "docs\AI22_8_5_ENTERPRISE_ATTENDANCE_OPERATIONS.md",
    "scripts\AI22_8_5_Copy.ps1"
  )
  "Prompt10" = @(
    "Abhyanvaya.Application.UnitTests\AttendanceRecovery",
    "docs\AI22_8_5_ENTERPRISE_ATTENDANCE_OPERATIONS.md",
    "scripts\AI22_8_5_Copy.ps1"
  ) + $commonBackend
}

$targets = if ($Prompt -eq "All") { @("Prompt1","Prompt2","Prompt3","Prompt4","Prompt5","Prompt6","Prompt7","Prompt8","Prompt9","Prompt10") } else { @($Prompt) }

Ensure-Dir $destRoot
foreach ($p in $targets) {
  Ensure-Dir (Join-Path $destRoot $p)
  foreach ($rel in $map[$p]) { Copy-Rel $rel $p }
  Write-Host "Copied -> $destRoot\$p"
}

Write-Host "Done."
