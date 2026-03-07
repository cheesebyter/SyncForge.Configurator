param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Framework = "net8.0",
    [string]$OutputRoot = "",
    [switch]$SkipConfiguratorPublish,
    [switch]$SkipPluginPublish
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

$scriptRoot = Split-Path -Path $MyInvocation.MyCommand.Path -Parent
$configuratorRoot = Split-Path -Path $scriptRoot -Parent
$workspaceRoot = Split-Path -Path $configuratorRoot -Parent

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
    Invoke-DotNet -Arguments @(
        "publish",
        $configuratorProject,
        "-c", $Configuration,
        "-f", $Framework,
        "-o", $OutputRoot
    )
}

if (-not $SkipPluginPublish) {
    foreach ($pluginProject in $pluginProjects) {
        if (-not (Test-Path $pluginProject)) {
            Write-Warning "Plugin project not found, skipping: $pluginProject"
            continue
        }

        $pluginName = [System.IO.Path]::GetFileNameWithoutExtension($pluginProject)
        $pluginOutput = Join-Path $pluginsOutputRoot $pluginName
        New-Item -Path $pluginOutput -ItemType Directory -Force | Out-Null

        Write-Host "Publishing plugin $pluginName..."
        Invoke-DotNet -Arguments @(
            "publish",
            $pluginProject,
            "-c", $Configuration,
            "-f", $Framework,
            "-o", $pluginOutput
        )
    }
}

Write-Host "Build complete."
Write-Host "Run Configurator from: $OutputRoot"
Write-Host "Plugin directory in UI: $pluginsOutputRoot"
