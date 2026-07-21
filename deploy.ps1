#Requires -Version 5.1
<#
.SYNOPSIS
  Build and package D.A.S.A for Windows deployment.

.DESCRIPTION
  1. Builds the React UI (src/dasa-ui)
  2. Publishes DASA.exe (src/DASA.Host)
  3. Creates a versioned zip under artifacts/ (unless -SkipZip)

.PARAMETER SelfContained
  Publish a self-contained build (includes .NET runtime; larger download).

.PARAMETER SkipZip
  Skip creating the release zip; only publish to the output folder.

.EXAMPLE
  .\deploy.ps1

.EXAMPLE
  .\deploy.ps1 -SelfContained

.EXAMPLE
  .\deploy.ps1 -SkipZip -PublishDir .\out
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [string] $Runtime = 'win-x64',

    [switch] $SelfContained,

    [switch] $SkipZip,

    [string] $PublishDir = ''
)

$ErrorActionPreference = 'Stop'

$RepoRoot = $PSScriptRoot
$UiDir = Join-Path $RepoRoot 'src\dasa-ui'
$HostDir = Join-Path $RepoRoot 'src\DASA.Host'
$Csproj = Join-Path $HostDir 'DASA.Host.csproj'

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $HostDir 'publish'
} elseif (-not [System.IO.Path]::IsPathRooted($PublishDir)) {
    $PublishDir = Join-Path $RepoRoot $PublishDir
}

function Get-ProjectVersion {
    param([string] $ProjectFile)

    [xml] $xml = Get-Content -LiteralPath $ProjectFile
    $versionNode = $xml.Project.PropertyGroup.Version |
        Where-Object { $_ -and $_.Trim().Length -gt 0 } |
        Select-Object -First 1

    if (-not $versionNode) {
        throw "Could not read <Version> from $ProjectFile"
    }

    return $versionNode.Trim()
}

function Write-Step {
    param([string] $Message)

    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Checked {
    param(
        [string] $Label,
        [scriptblock] $Command
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE"
    }
}

$version = Get-ProjectVersion -ProjectFile $Csproj

Write-Host "D.A.S.A deploy v$version" -ForegroundColor Green
Write-Host "Repository: $RepoRoot"

$running = Get-Process -Name 'DASA' -ErrorAction SilentlyContinue
if ($running) {
    throw "DASA is running (PID $($running.Id)). Exit from the system tray, then run deploy.ps1 again."
}

Write-Step 'Building UI (npm ci + npm run build)'
Push-Location $UiDir
try {
    Invoke-Checked 'npm ci' { npm ci }
    Invoke-Checked 'npm run build' { npm run build }
} finally {
    Pop-Location
}

$distIndex = Join-Path $UiDir 'dist\index.html'
if (-not (Test-Path -LiteralPath $distIndex)) {
    throw "UI build missing: $distIndex"
}

Write-Step 'Publishing host (dotnet publish)'
Push-Location $HostDir
try {
    $selfContainedFlag = if ($SelfContained) { 'true' } else { 'false' }
    Invoke-Checked 'dotnet publish' {
        dotnet publish `
            -c $Configuration `
            -r $Runtime `
            --self-contained $selfContainedFlag `
            -o $PublishDir
    }
} finally {
    Pop-Location
}

$exePath = Join-Path $PublishDir 'DASA.exe'
$uiIndex = Join-Path $PublishDir 'ui\index.html'

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Missing output: $exePath"
}

if (-not (Test-Path -LiteralPath $uiIndex)) {
    throw "Missing packaged UI: $uiIndex"
}

$zipPath = $null
if (-not $SkipZip) {
    Write-Step 'Creating release zip'

    $artifactDir = Join-Path $RepoRoot 'artifacts'
    New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

    $zipSuffix = if ($SelfContained) { "$Runtime-selfcontained" } else { $Runtime }
    $zipPath = Join-Path $artifactDir "DASA-v$version-$zipSuffix.zip"

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $PublishDir '*') -DestinationPath $zipPath -Force
}

Write-Host ''
Write-Host 'Deploy complete.' -ForegroundColor Green
Write-Host "  EXE:  $exePath"
Write-Host "  UI:   $(Join-Path $PublishDir 'ui')"
if ($zipPath) {
    Write-Host "  Zip:  $zipPath"
}
Write-Host ''
Write-Host "Run:  & '$exePath'"
