[CmdletBinding(SupportsShouldProcess)]
param([switch]$SkipInstaller)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = (Get-Content -LiteralPath (Join-Path $repoRoot 'VERSION.txt') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid VERSION.txt: $version" }

& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
$publish = Join-Path $repoRoot 'publish_v13_5_vm'

if ($PSCmdlet.ShouldProcess($publish, "Publish Tool TikTok $version")) {
    if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
    dotnet publish (Join-Path $repoRoot 'src\ToolTikTok.Worker\ToolTikTok.Worker.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=false -p:PublishTrimmed=false -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Worker publish failed.' }
    dotnet publish (Join-Path $repoRoot 'src\ToolTikTok.Manager\ToolTikTok.Manager.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=false -p:PublishTrimmed=false -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Manager publish failed.' }
}

if (-not $SkipInstaller) {
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $iscc) { throw 'Inno Setup 6 not found. Use -SkipInstaller to publish without an installer.' }
    & $iscc "/DMyAppVersion=$version" (Join-Path $repoRoot 'installer\windows\ToolTikTok.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
    $setup = Join-Path $repoRoot "SETUP_OUTPUT\ToolTikTok_V${version}_Setup.exe"
    & (Join-Path $PSScriptRoot 'SYNC_VERSION.ps1') -Root $repoRoot -SetupPath $setup
}
