# SyncForge.Configurator

Desktop-UI fuer SyncForge (Avalonia, .NET 8) mit JSON-First Ansatz.

Der Configurator erstellt und bearbeitet `job.json` Dateien, ohne Core-Businesslogik zu duplizieren.
Execution (Dry-Run/Run) nutzt denselben Orchestrator-Pfad wie die CLI.

## Scope

- Projekt: `../SyncForge.Configurator`
- Framework: `.NET 8`
- UI: `Avalonia 11`
- Startpunkt: `Program.cs` -> `App` -> `MainWindow`
- DataContext: `MainWindowViewModel`

## Features

### JSON-First Editing

- New / Open / Save / Save As fuer `job.json`
- JSON bleibt direkt editierbar
- Live JSON Preview mit gueltig/ungueltig Status
- Bei gueltigem JSON: Sync zur UI (Connector, Settings, Mapping)

### Validation

- Validierung ueber `JobDefinitionValidator` (aus Abstractions/Core-Pfad)
- Fehlerliste im UI
- Erweiterte Mapping-Pruefungen (z. B. required target/source)

### Connector Integration

- Reflection-basierte Discovery von Source/Target Connectors
- Connector-Auswahl aktualisiert JSON (`source.*`, `target.*`)
- Dynamische Settings Panels je Connector-Typ

### Mapping Grid

- Source Preview fuer `csv`, `xlsx`, `jsonl`
- Mapping Rows (add/remove)
- `SourceField`, `TargetField`, `IsRequired`
- Bidirektionale Sync in `mappings`

### Execution

- Dry-Run direkt aus UI
- Run direkt aus UI
- Live Logs und JSON Summary fuer beide Modi
- Run-Progressbar waehrend Ausfuehrung

### Log Viewer

- Zentraler Log-Viewer (Dry-Run + Run)
- Level-Filter (`All`, `INFO`, `WARN`, `ERROR`)
- Textsuche
- Export als `.log` / `.txt`

### New Job Wizard

Gefuehrter 4-Schritt-Flow:

1. Source waehlen
2. Target waehlen
3. Strategy waehlen
4. Basisparameter setzen

`Finish` erzeugt gueltiges `JobDefinition` JSON.

### Error UX

- Nutzerfreundliche Fehlerzusammenfassung (ohne Stacktrace im UI)
- Reproduzierbare Error-Details (Timestamp, Kontext)
- `Copy Error Details` in Clipboard

## Build und Start

Aus dem Repo-Root (`SyncForge`):

```powershell
# Build komplette Solution
dotnet build .\src\SyncForge.sln -c Release

# Configurator starten
dotnet run --project ..\SyncForge.Configurator\SyncForge.Configurator.csproj
```

## Visual Studio Build Automation

Der Configurator ist so konfiguriert, dass ein normaler Build in Visual Studio (Build / Rebuild) automatisch Plugin-Artefakte erzeugt.

Beim Build von `SyncForge.Configurator.csproj` passiert zusaetzlich:

- Publish der Plugin-Projekte (CSV, Excel, REST, MSSQL falls vorhanden)
- Kopieren der Plugin-DLLs nach `plugins\`
- Erzeugung von `trusted-plugins.json`

Damit verhaelt sich Visual Studio konsistent zu den bisherigen Skripten.

Direkter CLI-Aufruf (gleiches Verhalten wie in Visual Studio):

```powershell
dotnet build .\SyncForge.Configurator.csproj -c Release
```

Hinweis:

- Das automatische Bundling kann bei Bedarf deaktiviert werden mit:

```powershell
dotnet build .\SyncForge.Configurator.csproj -c Release /p:SyncForgeBundlePluginsOnBuild=false
```

## Build-Skripte (Configurator + Plugins)

Fuer interne Builds stehen Skripte bereit, die den Configurator publishen und die verfuegbaren Plugin-Repositories mitbauen.
Die Plugin-Ausgaben landen unter `artifacts/publish/plugins/<PluginName>`.

PowerShell (Windows):

```powershell
Set-Location .\SyncForge.Configurator
.\scripts\build-configurator-with-plugins.ps1 -Configuration Release
```

Bash (Linux/macOS):

```bash
cd ./SyncForge.Configurator
chmod +x ./scripts/build-configurator-with-plugins.sh
./scripts/build-configurator-with-plugins.sh --configuration Release
```

Wichtige Optionen:

- `--skip-plugin-publish` / `-SkipPluginPublish`
- `--skip-configurator-publish` / `-SkipConfiguratorPublish`
- `--output-root <path>` / `-OutputRoot <path>`
- `--version <value>` / `-Version <value>`
- `--commit <sha>` / `-Commit <sha>`
- `--build-timestamp-utc <iso8601>` / `-BuildTimestampUtc <iso8601>`

Hinweis: Fehlende Plugin-Repositories werden mit Warnung uebersprungen, damit der Build trotzdem laeuft.

Die Build-Skripte schreiben zusaetzlich:

- `build-metadata.json` (Version, Commit, Build-Zeit, Output-Pfade)
- `trusted-plugins.json` (SHA256-Allowlist fuer Plugin-Trust-Policy)

Die Skripte bleiben fuer Packaging/CI nuetzlich; fuer lokale Visual-Studio-Builds ist kein separates Skript mehr erforderlich.

## Packaging (EPIC 12)

Windows ZIP (framework-dependent):

```powershell
Set-Location .\SyncForge.Configurator
.\scripts\package-configurator-windows.ps1 -Configuration Release
```

Windows ZIP (self-contained):

```powershell
.\scripts\package-configurator-windows.ps1 -Configuration Release -SelfContained
```

Linux tar.gz (self-contained):

```bash
cd ./SyncForge.Configurator
chmod +x ./scripts/package-configurator-linux.sh
./scripts/package-configurator-linux.sh --configuration Release
```

## Quickstart

- 10-Minuten-Guide: `docs/quickstart-10-min.md`
- Screenshot-Ablage: `docs/screenshots/README.md`

## Abhaengigkeiten

### Projektverweise

- `SyncForge.Core`
- `SyncForge.Abstractions`

### NuGet

- `Avalonia`
- `Avalonia.Desktop`
- `Avalonia.Fonts.Inter`
- `DocumentFormat.OpenXml` (XLSX Source Preview)
- `Microsoft.Extensions.DependencyInjection`

## Wichtige Dateien

- `Views/MainWindow.axaml`
- `Views/MainWindow.axaml.cs`
- `ViewModels/MainWindowViewModel.cs`
- `docs/design-system.md`
- `Styles/SyncForgeTheme.axaml`
- `Styles/Tokens.axaml`
- `Styles/Controls/Button.axaml`
- `Styles/Controls/TextBox.axaml`
- `Styles/Controls/ComboBox.axaml`
- `Styles/Controls/ListBox.axaml`
- `Styles/Controls/ScrollBar.axaml`
- `Services/ConnectorDiscoveryService.cs`
- `Services/ConnectorConfigSchemaService.cs`
- `Services/SourcePreviewService.cs`
- `Services/DryRunExecutionService.cs`
- `Services/JsonLinesConnectors.cs`

## Design-System Dokumentation

- Vollstaendige Dokumentation: `docs/design-system.md`

## Hinweise zur Plugin-Aufloesung

- Plugin Discovery scannt den konfigurierten Plugin-Pfad plus `src`-Baum.
- `Plugin Directory` ist in der UI editierbar.
- Fuer `target/source = jsonl` sind eingebaute Runtime-Connectoren im Configurator vorhanden.

## Bekannte Grenzen

- Kein Scheduling / Multi-User / Monitoring (ausserhalb Scope R 0.2.0)
- Kein Lizenzmechanismus in `0.2.0`
