$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$targets = @(
  "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI31"
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
  "31.1_Dashboard" = @(
    "Abhyanvaya.Application\Faculty\FacultyDashboardService.cs",
    "Abhyanvaya.Application\DTOs\Faculty\FacultyWorkspaceDtos.cs",
    "Abhyanvaya.API\Controllers\FacultyWorkspaceController.cs",
    "abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx",
    "abhyanvaya-ui\src\services\facultyWorkspaceService.ts"
  )
  "31.2_CurrentClass" = @(
    "Abhyanvaya.Application\Faculty\FacultyDashboardService.cs",
    "abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx"
  )
  "31.3_OneClickAttendance" = @(
    "abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx",
    "abhyanvaya-ui\src\pages\Login.tsx"
  )
  "31.4_AIClassroom" = @("abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx")
  "31.5_Timetable" = @("Abhyanvaya.Application\Faculty\FacultyDashboardService.cs", "abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx")
  "31.6_Notifications" = @(
    "Abhyanvaya.Application\Faculty\IFacultyScheduleNotifier.cs",
    "Abhyanvaya.API\Hubs\FacultyHub.cs",
    "Abhyanvaya.API\SignalR\FacultySignalRPublisher.cs",
    "Abhyanvaya.Application\Scheduling\TimetableChangeHistoryService.cs",
    "Abhyanvaya.API\Program.cs"
  )
  "31.7_Insights" = @("Abhyanvaya.Application\Faculty\FacultyDashboardService.cs")
  "31.8_QuickActions" = @("abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx")
  "31.9_Mobile" = @("abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx")
  "31.10_Offline" = @("abhyanvaya-ui\src\pages\faculty\FacultyWorkspacePage.tsx")
  "31.11_Tests" = @("Abhyanvaya.Application.UnitTests\Faculty")
  "31.12_Docs" = @(
    "docs\AI31_INTELLIGENT_FACULTY_WORKSPACE.md",
    "docs\AI31_ARCHITECTURE_REVIEW.md",
    "docs\AI31_IMPLEMENTATION_SUMMARY.md"
  )
  "_FULL" = @(
    "Abhyanvaya.Application\Faculty",
    "Abhyanvaya.Application\DTOs\Faculty",
    "Abhyanvaya.Application\DependencyInjection.cs",
    "Abhyanvaya.Application\Scheduling\TimetableChangeHistoryService.cs",
    "Abhyanvaya.API\Controllers\FacultyWorkspaceController.cs",
    "Abhyanvaya.API\Hubs\FacultyHub.cs",
    "Abhyanvaya.API\SignalR\FacultySignalRPublisher.cs",
    "Abhyanvaya.API\Program.cs",
    "abhyanvaya-ui\src\pages\faculty",
    "abhyanvaya-ui\src\services\facultyWorkspaceService.ts",
    "abhyanvaya-ui\src\routes\AppRoutes.tsx",
    "abhyanvaya-ui\src\layouts\MainLayout.tsx",
    "abhyanvaya-ui\src\pages\Login.tsx",
    "Abhyanvaya.Application.UnitTests\Faculty",
    "Abhyanvaya.Application.UnitTests\Scheduling\Phase2A\TimetableChangeHistoryTests.cs",
    "docs\AI31_INTELLIGENT_FACULTY_WORKSPACE.md",
    "docs\AI31_ARCHITECTURE_REVIEW.md",
    "docs\AI31_IMPLEMENTATION_SUMMARY.md"
  )
}

foreach ($destRoot in $targets) {
  Ensure-Dir $destRoot
  foreach ($k in $prompts.Keys) {
    Ensure-Dir (Join-Path $destRoot $k)
    foreach ($rel in $prompts[$k]) { Copy-Rel $rel $destRoot $k }
  }
  Write-Host "Done -> $destRoot"
}
