$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$destRoot = "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI22.8"

function Ensure-Dir([string]$path) { if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force -Path $path | Out-Null } }
function Copy-Rel([string]$rel) {
  $src = Join-Path $root $rel
  if (-not (Test-Path $src)) { Write-Warning "Missing: $rel"; return }
  $dest = Join-Path $destRoot $rel
  Ensure-Dir (Split-Path $dest -Parent)
  if ((Get-Item $src).PSIsContainer) { Copy-Item -Recurse -Force $src $dest }
  else { Copy-Item -Force $src $dest }
}

Ensure-Dir $destRoot
@(
  "Abhyanvaya.Domain\Enums\AttendanceWorkflowStatus.cs",
  "Abhyanvaya.Domain\Entities\AttendanceRetryHistory.cs",
  "Abhyanvaya.Domain\Entities\AttendanceSession.cs",
  "Abhyanvaya.Domain\Entities\Scheduling\WorkspacePreference.cs",
  "Abhyanvaya.Application\AttendanceRecovery",
  "Abhyanvaya.Application\DTOs\AttendanceRecovery",
  "Abhyanvaya.Application\AttendanceSessionFinalizer.cs",
  "Abhyanvaya.Application\DependencyInjection.cs",
  "Abhyanvaya.Application\Common\Interfaces\IApplicationDbContext.cs",
  "Abhyanvaya.Application.UnitTests\AttendanceRecovery",
  "Abhyanvaya.Infrastructure\ClassroomAttendance\AttendanceSessionManager.cs",
  "Abhyanvaya.Infrastructure\Recognition\ClassroomRecognitionPipeline.cs",
  "Abhyanvaya.Infrastructure\Persistence\ApplicationDbContext.cs",
  "Abhyanvaya.Infrastructure\Persistence\Configurations\AttendanceSessionConfiguration.cs",
  "Abhyanvaya.Infrastructure\Persistence\Configurations\AttendanceRetryHistoryConfiguration.cs",
  "Abhyanvaya.Infrastructure\Persistence\Configurations\Scheduling\WorkspacePreferenceConfiguration.cs",
  "Abhyanvaya.Infrastructure\Persistence\Migrations\20260803123000_AI22_8_AttendanceRecovery.cs",
  "Abhyanvaya.Infrastructure\BackgroundWorkers\AttendanceSessionExpirationCleanupService.cs",
  "Abhyanvaya.Infrastructure\DependencyInjection.cs",
  "Abhyanvaya.API\Controllers\AttendanceRecoveryController.cs",
  "Abhyanvaya.API\Controllers\AttendanceRecoveryAdminController.cs",
  "Abhyanvaya.API\SignalR\AttendanceRecoverySignalRPublisher.cs",
  "Abhyanvaya.API\Program.cs",
  "Abhyanvaya.API\appsettings.json",
  "abhyanvaya-ui\src\services\attendanceRecoveryService.ts",
  "abhyanvaya-ui\src\pages\AttendanceRecognitionReviewPage.tsx",
  "abhyanvaya-ui\src\pages\faculty\FacultyPendingAttendancePanel.tsx",
  "abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx",
  "abhyanvaya-ui\src\pages\setup\AttendanceRecoveryDashboardPage.tsx",
  "abhyanvaya-ui\src\pages\setup\SetupHub.tsx",
  "abhyanvaya-ui\src\routes\AppRoutes.tsx",
  "docs\AI22_8_ATTENDANCE_LIFECYCLE.md",
  "docs\AI22_8_ENTERPRISE_ATTENDANCE_RECOVERY.md",
  "docs\AI22_8_ARCHITECTURE_REVIEW.md",
  "docs\AI22_8_IMPLEMENTATION_SUMMARY.md",
  "docs\AI22_8_RECOVERY_FLOW.md",
  "docs\AI22_8_RETRY_FLOW.md",
  "docs\AI22_8_MIGRATION_GUIDE.md",
  "docs\AI22_8_TEST_REPORT.md",
  "scripts\AI22_8_Finalize.ps1"
) | ForEach-Object { Copy-Rel $_ }

Write-Host "Done -> $destRoot"
