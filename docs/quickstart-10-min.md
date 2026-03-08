# SyncForge Configurator Quickstart (10 Minuten)

Diese Anleitung bringt einen frischen Download in wenigen Minuten zum ersten Dry-Run.

## 1. Voraussetzungen

- .NET 8 SDK installiert
- Repositories liegen auf derselben Ebene:

```text
J:/
  SyncForge/
  SyncForge.Plugin.MsSql/
  SyncForge.Configurator/
```

## 2. Build und Packaging

### Windows (ZIP)

```powershell
Set-Location J:\SyncForge.Configurator
.\scripts\package-configurator-windows.ps1 -Configuration Release
```

Optional self-contained:

```powershell
.\scripts\package-configurator-windows.ps1 -Configuration Release -SelfContained
```

Output:

- `artifacts/packages/windows/SyncForge.Configurator-win-framework-dependent.zip`
- oder self-contained ZIP

### Linux (tar.gz)

```bash
cd /path/to/SyncForge.Configurator
chmod +x ./scripts/package-configurator-linux.sh
chmod +x ./scripts/build-configurator-with-plugins.sh
./scripts/package-configurator-linux.sh --configuration Release
```

Output:

- `artifacts/packages/linux/SyncForge.Configurator-linux-selfcontained-linux-x64.tar.gz`

## 3. Starten

### Windows

- ZIP entpacken
- `SyncForge.Configurator.exe` starten

### Linux

```bash
tar -xzf SyncForge.Configurator-linux-selfcontained-linux-x64.tar.gz
cd <entpackter-ordner>
chmod +x SyncForge.Configurator
./SyncForge.Configurator
```

## 4. Erster Testjob

Beispieljob verwenden:

- `J:/SyncForge/examples/jobs/job-csv-run-upsert.json`

Im Configurator:

1. `Open` klicken und Job laden
2. Optional `Validate`
3. `Dry-Run` starten
4. Logs und Summary prüfen

## 5. Screenshot-Checkliste

Ablage fuer Screenshots:

- `docs/screenshots/`

Empfohlene Bilder:

- `01-main-window.png`
- `02-connector-selection.png`
- `03-dry-run-summary.png`
- `04-log-viewer.png`

## Troubleshooting

- Connector-Dropdowns leer:
  - `Plugin Directory` pruefen
  - `Reload` klicken
- Plugin nicht gefunden:
  - Packaging-Skript ohne `--skip-build`/`-SkipBuild` ausfuehren
- Start dauert beim ersten self-contained Run:
  - normal durch Runtime-Extraktion
