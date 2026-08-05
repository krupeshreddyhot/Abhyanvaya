param(
  [Parameter(Mandatory = $false)]
  [ValidateSet("All","Prompt1","Prompt2","Prompt3","Prompt4","Prompt5","Prompt6","Prompt7","Prompt8","Prompt9","Prompt10","_FULL")]
  [string]$Prompt = "All"
)

$ErrorActionPreference = "Stop"
$root = "D:\Resheta\AttendenceProject\Abhyanvaya"
$destRoot = "D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI31.8.1A"

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
  "abhyanvaya-ui\src\components\dashboards\dashboardLayoutTokens.ts",
  "abhyanvaya-ui\src\components\dashboards\dashboardLayoutTokens.test.ts",
  "abhyanvaya-ui\src\components\dashboards\DashboardWidgets.tsx",
  "abhyanvaya-ui\src\components\dashboards\DashboardExcellencePanels.tsx",
  "abhyanvaya-ui\src\pages\dashboards\AdminOperationsDashboardPage.tsx"
)

$docs = @(
  "docs\AI31_8_1A_ENTERPRISE_DASHBOARD_VISUAL_REFINEMENT.md",
  "docs\AI31_8_1A_ARCHITECTURE_REVIEW.md",
  "docs\AI31_8_1A_IMPLEMENTATION_SUMMARY.md",
  "docs\AI31_8_1A_RESPONSIVE_VALIDATION.md",
  "docs\AI31_8_1A_UX_GUIDELINES.md",
  "scripts\AI31_8_1A_Copy.ps1"
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
  "_FULL" = $docs + $core
}

$targets = if ($Prompt -eq "All") {
  @("Prompt1","Prompt2","Prompt3","Prompt4","Prompt5","Prompt6","Prompt7","Prompt8","Prompt9","Prompt10","_FULL")
} else { @($Prompt) }

Ensure-Dir $destRoot
foreach ($p in $targets) {
  Ensure-Dir (Join-Path $destRoot $p)
  foreach ($rel in $map[$p]) { Copy-Rel $rel $p }
  Write-Host "Copied -> $destRoot\$p"
}
Write-Host "Done."
