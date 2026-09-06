[CmdletBinding()]
param([switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $NoBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
}

$manager = Join-Path $repoRoot 'dist_v13\ToolTikTokManagerV13.exe'
if (-not (Test-Path -LiteralPath $manager)) { throw "Manager executable not found: $manager" }
Start-Process -FilePath $manager -WorkingDirectory (Split-Path -Parent $manager)
