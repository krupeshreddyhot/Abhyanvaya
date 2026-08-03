# AI30 Phase 2B finalize: free space helpers, tests, desktop copy
$ErrorActionPreference = "Stop"
$repo = "D:\Resheta\AttendenceProject\Abhyanvaya"
$desktop = "C:\Users\Rupesh Reddy\Desktop\Saviter\Abhyanvaya\AI Attandance\AI30 Phase 2B"

Write-Host "Freeing bin/obj..."
Get-ChildItem $repo -Recurse -Directory -Filter bin -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -notmatch "node_modules" } |
  ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
Get-ChildItem $repo -Recurse -Directory -Filter obj -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -notmatch "node_modules" } |
  ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }

Write-Host "Building + Phase2B tests..."
dotnet test "$repo\Abhyanvaya.Application.UnitTests\Abhyanvaya.Application.UnitTests.csproj" --filter "FullyQualifiedName~Phase2B"

Write-Host "Copying desktop deliverables..."
$folders = @(
  "2B.1_Conflict_Engine","2B.2_Faculty_Conflicts","2B.3_Room_Conflicts","2B.4_Student_Conflicts",
  "2B.5_Calendar_Validation","2B.6_Heat_Maps","2B.7_Conflict_Workspace","2B.8_Attendance_Resolver",
  "2B.9_Dashboard","2B.10_Documentation","2B.11_Testing","2B.12_Architecture_Review","_FULL"
)
foreach ($f in $folders) { New-Item -ItemType Directory -Force -Path (Join-Path $desktop $f) | Out-Null }

Copy-Item "$repo\Abhyanvaya.Application\Scheduling\Conflicts\*" "$desktop\2B.1_Conflict_Engine\" -Recurse -Force
Copy-Item "$repo\Abhyanvaya.Application\Scheduling\Conflicts\Rules\FacultyConflictRules.cs" "$desktop\2B.2_Faculty_Conflicts\" -Force
Copy-Item "$repo\Abhyanvaya.Application\Scheduling\Conflicts\Rules\RoomConflictRules.cs" "$desktop\2B.3_Room_Conflicts\" -Force
Copy-Item "$repo\Abhyanvaya.Application\Scheduling\Conflicts\Rules\StudentConflictRules.cs" "$desktop\2B.4_Student_Conflicts\" -Force
Copy-Item "$repo\Abhyanvaya.Application\Scheduling\Conflicts\Rules\CalendarConflictRules.cs" "$desktop\2B.5_Calendar_Validation\" -Force
Copy-Item "$repo\docs\AI30_PHASE2B_ENTERPRISE_CONFLICT_ENGINE.md" "$desktop\2B.6_Heat_Maps\" -Force
Copy-Item "$repo\abhyanvaya-ui\src\pages\setup\scheduling\conflicts\*" "$desktop\2B.7_Conflict_Workspace\" -Force
Copy-Item "$repo\docs\AI30_PHASE2B_ATTENDANCE_RESOLUTION.md" "$desktop\2B.8_Attendance_Resolver\" -Force
Copy-Item "$repo\Abhyanvaya.Application\Scheduling\Conflicts\AttendanceSessionResolver.cs" "$desktop\2B.8_Attendance_Resolver\" -Force
Copy-Item "$repo\abhyanvaya-ui\src\pages\setup\scheduling\conflicts\ConflictDashboardPage.tsx" "$desktop\2B.9_Dashboard\" -Force
Copy-Item "$repo\docs\AI30_PHASE2B_*.md" "$desktop\2B.10_Documentation\" -Force
Copy-Item "$repo\Abhyanvaya.Application.UnitTests\Scheduling\Phase2B\*" "$desktop\2B.11_Testing\" -Force
Copy-Item "$repo\docs\AI30_PHASE2B_ARCHITECTURE_REVIEW.md" "$desktop\2B.12_Architecture_Review\" -Force
Copy-Item "$repo\docs\AI30_PHASE2B_IMPLEMENTATION_SUMMARY.md" "$desktop\2B.12_Architecture_Review\" -Force

# Full bundle
Copy-Item "$repo\docs\AI30_PHASE2B_*.md" "$desktop\_FULL\" -Force
Copy-Item "$repo\Abhyanvaya.Application\Scheduling\Conflicts" "$desktop\_FULL\Conflicts" -Recurse -Force
Copy-Item "$repo\Abhyanvaya.API\Controllers\Scheduling\Phase2BControllers.cs" "$desktop\_FULL\" -Force
Copy-Item "$repo\Abhyanvaya.Infrastructure\Persistence\Migrations\*AI30_Phase2B_ConflictDetection*" "$desktop\_FULL\" -Force
Copy-Item "$repo\docs\AI30_PHASE2B_TEST_REPORT.md" "$desktop\2B.11_Testing\" -Force
Copy-Item "$repo\docs\AI30_PHASE2B_TEST_REPORT.md" "$desktop\2B.10_Documentation\" -Force
Copy-Item "$repo\abhyanvaya-ui\src\pages\setup\scheduling\conflicts" "$desktop\_FULL\ui-conflicts" -Recurse -Force
Copy-Item "$repo\Abhyanvaya.Application.UnitTests\Scheduling\Phase2B" "$desktop\_FULL\tests" -Recurse -Force

Write-Host "Done. Desktop: $desktop"
Get-ChildItem $desktop -Recurse -File | Measure-Object | Select-Object Count
