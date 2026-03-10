[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Framework = "net8.0",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SelfContained,
    [switch]$SkipBuild,
    [string]$OutputRoot = "",
    [string]$Version = "0.2.1",
    [string]$Commit = "",
    [string]$BuildTimestampUtc = ""
)

$ErrorActionPreference = "Stop"

function Invoke-DotNet {
    param([string[]]$Arguments)
    Write-Host ("dotnet " + ($Arguments -join " "))
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE"
    }
}

$scriptRoot = Split-Path -Path $MyInvocation.MyCommand.Path -Parent
$configuratorRoot = Split-Path -Path $scriptRoot -Parent
$projectFile = Join-Path $configuratorRoot "SyncForge.Configurator.csproj"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $configuratorRoot "artifacts\packages\windows"
}

$stagingRoot = Join-Path $OutputRoot "staging"
$publishRoot = Join-Path $stagingRoot "publish"
$archiveName = if ($SelfContained) {
    "SyncForge.Configurator-win-selfcontained-$RuntimeIdentifier.zip"
} else {
    "SyncForge.Configurator-win-framework-dependent.zip"
}
$archivePath = Join-Path $OutputRoot $archiveName

New-Item -Path $OutputRoot -ItemType Directory -Force | Out-Null
if (Test-Path $stagingRoot) {
    Remove-Item $stagingRoot -Recurse -Force
}
New-Item -Path $publishRoot -ItemType Directory -Force | Out-Null

if (-not $SkipBuild) {
    $buildScript = Join-Path $scriptRoot "build-configurator-with-plugins.ps1"
    & $buildScript -Configuration $Configuration -Framework $Framework -OutputRoot $publishRoot -Version $Version -Commit $Commit -BuildTimestampUtc $BuildTimestampUtc
    if ($LASTEXITCODE -ne 0) {
        throw "build-configurator-with-plugins.ps1 failed with exit code $LASTEXITCODE"
    }
}
elseif (-not (Test-Path (Join-Path $publishRoot "SyncForge.Configurator.dll"))) {
    throw "SkipBuild was specified but no staged publish output was found at '$publishRoot'."
}

if ($SelfContained) {
    $selfContainedRoot = Join-Path $stagingRoot "selfcontained"
    New-Item -Path $selfContainedRoot -ItemType Directory -Force | Out-Null

    Invoke-DotNet -Arguments @(
        "publish",
        $projectFile,
        "-c", $Configuration,
        "-f", $Framework,
        "-r", $RuntimeIdentifier,
        "--self-contained", "true",
        "-o", $selfContainedRoot,
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true"
    )

    $pluginSource = Join-Path $publishRoot "plugins"
    $pluginTarget = Join-Path $selfContainedRoot "plugins"
    if (Test-Path $pluginSource) {
        Copy-Item $pluginSource $pluginTarget -Recurse -Force
    }

    $trustSource = Join-Path $publishRoot "trusted-plugins.json"
    if (Test-Path $trustSource) {
        Copy-Item $trustSource (Join-Path $selfContainedRoot "trusted-plugins.json") -Force
    }

    $metadataSource = Join-Path $publishRoot "build-metadata.json"
    if (Test-Path $metadataSource) {
        Copy-Item $metadataSource (Join-Path $selfContainedRoot "build-metadata.json") -Force
    }

    Remove-Item $publishRoot -Recurse -Force
    Copy-Item (Join-Path $selfContainedRoot "*") $publishRoot -Recurse -Force
}

if (Test-Path $archivePath) {
    Remove-Item $archivePath -Force
}

$archiveCreated = $false
for ($attempt = 1; $attempt -le 20; $attempt++) {
    try {
        Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $archivePath -Force -ErrorAction Stop
        $archiveCreated = $true
        break
    }
    catch {
        if ($attempt -eq 20) {
            throw "Failed to create archive after $attempt attempts. Last error: $($_.Exception.Message)"
        }

        Start-Sleep -Milliseconds 1500
    }
}

if (-not $archiveCreated -or -not (Test-Path $archivePath)) {
    throw "Archive creation failed: $archivePath"
}

Write-Host "Windows package created: $archivePath"
