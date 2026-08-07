param(
  [Parameter(Mandatory = $false)]
  [ValidateSet("All","Prompt1","Prompt2","Prompt3","Prompt4","Prompt5","Prompt6","Prompt7","Prompt8","Prompt9","Prompt10","Prompt11","Prompt12","_FULL")]
  [string]$Prompt = "All"
)

$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$destRoot = "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI29.1A"
$destNested = "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI29.1\AI29.1A"

function Ensure-Dir([string]$path) { if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force -Path $path | Out-Null } }
function Copy-Rel([string]$rel, [string]$base) {
  $src = Join-Path $root $rel
  if (-not (Test-Path $src)) { Write-Warning "Missing: $rel"; return }
  $dest = Join-Path $base $rel
  Ensure-Dir (Split-Path $dest -Parent)
  if ((Get-Item $src).PSIsContainer) { Copy-Item -Recurse -Force $src $dest }
  else { Copy-Item -Force $src $dest }
}

$core = @(
  "Abhyanvaya.Domain\Entities\Academic\Program.cs",
  "Abhyanvaya.Domain\Entities\Academic\TenantAcademicConfiguration.cs",
  "Abhyanvaya.Domain\Entities\Course.cs",
  "Abhyanvaya.Domain\Authorization\PermissionKeys.cs",
  "Abhyanvaya.Application\DTOs\Academic\ProgramDtos.cs",
  "Abhyanvaya.Application\DTOs\Course\CreateCourseRequest.cs",
  "Abhyanvaya.Application\DTOs\Course\UpdateCourseRequest.cs",
  "Abhyanvaya.Application\Academic\IAcademicStructureService.cs",
  "Abhyanvaya.Application\Academic\AcademicStructureService.cs",
  "Abhyanvaya.Application\Academic\Validators\ProgramValidators.cs",
  "Abhyanvaya.Application\DependencyInjection.cs",
  "Abhyanvaya.Application\Common\Interfaces\IApplicationDbContext.cs",
  "Abhyanvaya.Application.UnitTests\Academic\AI29_1A_ProgramManagementTests.cs",
  "Abhyanvaya.Infrastructure\Persistence\ApplicationDbContext.cs",
  "Abhyanvaya.Infrastructure\Persistence\ApplicationDbContext.StaffHubSeed.cs",
  "Abhyanvaya.API\Controllers\ProgramsController.cs",
  "Abhyanvaya.API\Controllers\CourseController.cs",
  "Abhyanvaya.API\Common\AuthorizationPolicies.cs",
  "Abhyanvaya.API\Program.cs",
  "abhyanvaya-ui\src\services\programService.ts",
  "abhyanvaya-ui\src\pages\setup\ProgramsPage.tsx",
  "abhyanvaya-ui\src\pages\setup\SetupHub.tsx",
  "abhyanvaya-ui\src\routes\AppRoutes.tsx",
  "abhyanvaya-ui\src\auth\permissionKeys.ts",
  "scripts\Apply_AI29_1A_ProgramSchema.sql",
  "scripts\AI29_1A_Copy.ps1"
)

$docs = @(
  "docs\AI29_1A_ACADEMIC_HIERARCHY.md",
  "docs\AI29_1A_PROGRAM_MANAGEMENT.md",
  "docs\AI29_1A_DATABASE_DESIGN.md",
  "docs\AI29_1A_API_SPECIFICATION.md",
  "docs\AI29_1A_IMPLEMENTATION_SUMMARY.md",
  "docs\AI29_1A_ARCHITECTURE_REVIEW.md"
)

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
  "Prompt10" = $docs + $core
  "Prompt11" = $docs + $core
  "Prompt12" = $docs + $core
  "_FULL" = $docs + $core
}

$targets = if ($Prompt -eq "All") {
  @("Prompt1","Prompt2","Prompt3","Prompt4","Prompt5","Prompt6","Prompt7","Prompt8","Prompt9","Prompt10","Prompt11","Prompt12","_FULL")
} else { @($Prompt) }

Ensure-Dir $destRoot
Ensure-Dir $destNested
foreach ($p in $targets) {
  $folder = Join-Path $destRoot $p
  Ensure-Dir $folder
  foreach ($rel in $map[$p]) { Copy-Rel $rel $folder }
  Write-Host "Copied -> $folder"
}
# Also mirror full pack under AI29.1\AI29.1A
foreach ($rel in ($docs + $core)) { Copy-Rel $rel $destNested }
Write-Host "Mirrored _FULL content -> $destNested"
Write-Host "Done."
