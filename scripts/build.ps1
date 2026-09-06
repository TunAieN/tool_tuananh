[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solution = Join-Path $repoRoot 'ToolTikTok.sln'

dotnet restore $solution -m:1
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
dotnet build $solution -c $Configuration --no-restore -m:1
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

if (-not $SkipTests) {
    dotnet test $solution -c $Configuration --no-build -m:1
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }
}
