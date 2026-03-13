param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Framework = "net8.0",
    [string]$OutputRoot = "",
    [switch]$SkipConfiguratorPublish,
    [switch]$SkipPluginPublish,
    [string]$Version = "0.2.1",
    [string]$Commit = "",
    [string]$BuildTimestampUtc = ""
)

$ErrorActionPreference = "Stop"

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host ("dotnet " + ($Arguments -join " "))
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE"
    }
}

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string]$FilePath)
    return (Get-FileHash -Path $FilePath -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-DeterministicPublishProperties {
    return @(
        "-p:ContinuousIntegrationBuild=true",
        "-p:Deterministic=true",
        "-p:Version=$Version",
        "-p:InformationalVersion=$informationalVersion",
        "-p:SourceRevisionId=$Commit",
        "-p:RepositoryCommit=$Commit"
    )
}

$scriptRoot = Split-Path -Path $MyInvocation.MyCommand.Path -Parent
$configuratorRoot = Split-Path -Path $scriptRoot -Parent
$workspaceRoot = Split-Path -Path $configuratorRoot -Parent

$expectedSyncForgeSolution = Join-Path $workspaceRoot "SyncForge\src\SyncForge.sln"
if ($workspaceRoot -match '^[A-Za-z]:\\$') {
    Write-Warning "Workspace root resolves to drive root ('$workspaceRoot'). Plugin repositories may be missing or in a different location."
}

if (-not (Test-Path $expectedSyncForgeSolution)) {
    Write-Warning "Expected OSS solution not found at '$expectedSyncForgeSolution'. Plugin publish may be partially skipped."
}

if ([string]::IsNullOrWhiteSpace($Commit)) {
    try {
        $Commit = (& git -C $workspaceRoot rev-parse --verify HEAD 2>$null).Trim()
    }
    catch {
        $Commit = "unknown"
    }
}

if ([string]::IsNullOrWhiteSpace($BuildTimestampUtc)) {
    $BuildTimestampUtc = [DateTimeOffset]::UtcNow.ToString("O")
}

$informationalVersion = "$Version+$Commit"

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $configuratorRoot "artifacts\publish"
}

$configuratorProject = Join-Path $configuratorRoot "SyncForge.Configurator.csproj"
$pluginsOutputRoot = Join-Path $OutputRoot "plugins"

$pluginProjects = @(
    Join-Path $workspaceRoot "SyncForge\src\SyncForge.Plugin.Csv\SyncForge.Plugin.Csv.csproj"
    Join-Path $workspaceRoot "SyncForge\src\SyncForge.Plugin.Excel\SyncForge.Plugin.Excel.csproj"
    Join-Path $workspaceRoot "SyncForge\src\SyncForge.Plugin.Rest\SyncForge.Plugin.Rest.csproj"
    Join-Path $workspaceRoot "SyncForge.Plugin.MsSql\SyncForge.Plugin.MsSql.csproj"
)

Write-Host "Workspace root: $workspaceRoot"
Write-Host "Configurator root: $configuratorRoot"
Write-Host "Output root: $OutputRoot"

New-Item -Path $OutputRoot -ItemType Directory -Force | Out-Null
New-Item -Path $pluginsOutputRoot -ItemType Directory -Force | Out-Null

if (-not $SkipConfiguratorPublish) {
    Write-Host "Publishing SyncForge.Configurator..."
    $publishArgs = @(
        "publish",
        $configuratorProject,
        "-c", $Configuration,
        "-f", $Framework,
        "-o", $OutputRoot
    )
    $publishArgs += Get-DeterministicPublishProperties
    Invoke-DotNet -Arguments $publishArgs
}

if (-not $SkipPluginPublish) {
    $publishedPluginCount = 0

    foreach ($pluginProject in $pluginProjects) {
        if (-not (Test-Path $pluginProject)) {
            Write-Warning "Plugin project not found, skipping: $pluginProject"
            continue
        }

        $pluginName = [System.IO.Path]::GetFileNameWithoutExtension($pluginProject)
        $pluginOutput = Join-Path $pluginsOutputRoot $pluginName
        New-Item -Path $pluginOutput -ItemType Directory -Force | Out-Null

        Write-Host "Publishing plugin $pluginName..."
        $pluginPublishArgs = @(
            "publish",
            $pluginProject,
            "-c", $Configuration,
            "-f", $Framework,
            "-o", $pluginOutput
        )
        $pluginPublishArgs += Get-DeterministicPublishProperties
        Invoke-DotNet -Arguments $pluginPublishArgs
        $publishedPluginCount++
    }

    if ($publishedPluginCount -eq 0) {
        Write-Warning "No plugin projects were published. Use -SkipPluginPublish intentionally or verify repository layout."
    }
}

$trustedPlugins = @()
if (Test-Path $pluginsOutputRoot) {
    $pluginDlls = Get-ChildItem -Path $pluginsOutputRoot -Filter "SyncForge.Plugin.*.dll" -Recurse -File
    foreach ($dll in $pluginDlls) {
        $trustedPlugins += [ordered]@{
            assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($dll.Name)
            sha256 = (Get-Sha256Hex -FilePath $dll.FullName)
        }
    }
}

$trustManifestPath = Join-Path $OutputRoot "trusted-plugins.json"
$trustDocument = [ordered]@{
    plugins = $trustedPlugins
}
$trustDocument | ConvertTo-Json -Depth 6 | Set-Content -Path $trustManifestPath -Encoding UTF8

$metadataPath = Join-Path $OutputRoot "build-metadata.json"
$metadata = [ordered]@{
    version = $Version
    commit = $Commit
    buildTimestampUtc = $BuildTimestampUtc
    configuration = $Configuration
    framework = $Framework
    outputRoot = (Resolve-Path -Path $OutputRoot).Path
    pluginsOutputRoot = (Resolve-Path -Path $pluginsOutputRoot).Path
    trustedPluginsFile = $trustManifestPath
}
$metadata | ConvertTo-Json -Depth 6 | Set-Content -Path $metadataPath -Encoding UTF8

Write-Host "Build complete."
Write-Host "Run Configurator from: $OutputRoot"
Write-Host "Plugin directory in UI: $pluginsOutputRoot"
Write-Host "Build metadata: $metadataPath"
Write-Host "Trusted plugin manifest: $trustManifestPath"
