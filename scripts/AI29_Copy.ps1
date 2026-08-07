param(
  [Parameter(Mandatory = $false)]
  [ValidateSet("All","_FULL")]
  [string]$Prompt = "All"
)

$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$destRoot = "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI29"

function Ensure-Dir([string]$path) { if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force -Path $path | Out-Null } }
function Copy-Rel([string]$rel) {
  $src = Join-Path $root $rel
  if (-not (Test-Path $src)) { Write-Warning "Missing: $rel"; return }
  $dest = Join-Path $destRoot $rel
  Ensure-Dir (Split-Path $dest -Parent)
  if ((Get-Item $src).PSIsContainer) { Copy-Item -Recurse -Force $src $dest }
  else { Copy-Item -Force $src $dest }
}

$files = @(
  "Abhyanvaya.Domain\Entities\Academic\Section.cs",
  "Abhyanvaya.Domain\Entities\Academic\StudentSection.cs",
  "Abhyanvaya.Domain\Entities\Academic\FacultySectionAssignment.cs",
  "Abhyanvaya.Domain\Entities\Academic\TimetableSection.cs",
  "Abhyanvaya.Domain\Entities\Academic\AttendanceSessionSection.cs",
  "Abhyanvaya.Domain\Entities\Academic\SectionAllocationPreference.cs",
  "Abhyanvaya.Domain\Authorization\PermissionKeys.cs",
  "Abhyanvaya.Application\DTOs\Academic\SectionDtos.cs",
  "Abhyanvaya.Application\DTOs\Scheduling\ConflictDtos.cs",
  "Abhyanvaya.Application\Academic\ISectionManagementService.cs",
  "Abhyanvaya.Application\Academic\SectionManagementService.cs",
  "Abhyanvaya.Application\DependencyInjection.cs",
  "Abhyanvaya.Application\StudentService.cs",
  "Abhyanvaya.Application\Scheduling\Conflicts\AttendanceSessionResolver.cs",
  "Abhyanvaya.Application\Common\Interfaces\IApplicationDbContext.cs",
  "Abhyanvaya.Application.UnitTests\Academic\AI29_SectionManagementTests.cs",
  "Abhyanvaya.Infrastructure\Persistence\ApplicationDbContext.cs",
  "Abhyanvaya.Infrastructure\Persistence\ApplicationDbContext.StaffHubSeed.cs",
  "Abhyanvaya.API\Controllers\SectionsController.cs",
  "Abhyanvaya.API\Controllers\AttendanceController.cs",
  "Abhyanvaya.API\Common\AuthorizationPolicies.cs",
  "Abhyanvaya.API\Program.cs",
  "abhyanvaya-ui\src\services\sectionService.ts",
  "abhyanvaya-ui\src\pages\setup\SectionsPage.tsx",
  "abhyanvaya-ui\src\pages\setup\SetupHub.tsx",
  "abhyanvaya-ui\src\routes\AppRoutes.tsx",
  "abhyanvaya-ui\src\auth\permissionKeys.ts",
  "scripts\Apply_AI29_SectionSchema.sql",
  "scripts\AI29_Copy.ps1",
  "docs\AI29_ACADEMIC_STRUCTURE_AND_SECTION_MANAGEMENT.md",
  "docs\AI29_DATABASE_DESIGN.md",
  "docs\AI29_API_SPECIFICATION.md",
  "docs\AI29_IMPLEMENTATION_SUMMARY.md"
)

Ensure-Dir $destRoot
foreach ($rel in $files) { Copy-Rel $rel }
Write-Host "Copied $($files.Count) paths -> $destRoot"
Write-Host "Done."
