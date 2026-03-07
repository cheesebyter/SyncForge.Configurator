#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="Release"
FRAMEWORK="net8.0"
OUTPUT_ROOT=""
SKIP_CONFIGURATOR_PUBLISH="false"
SKIP_PLUGIN_PUBLISH="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    -f|--framework)
      FRAMEWORK="$2"
      shift 2
      ;;
    -o|--output-root)
      OUTPUT_ROOT="$2"
      shift 2
      ;;
    --skip-configurator-publish)
      SKIP_CONFIGURATOR_PUBLISH="true"
      shift
      ;;
    --skip-plugin-publish)
      SKIP_PLUGIN_PUBLISH="true"
      shift
      ;;
    -h|--help)
      cat <<'EOF'
Usage: build-configurator-with-plugins.sh [options]

Options:
  -c, --configuration <Debug|Release>    Build configuration (default: Release)
  -f, --framework <TFM>                  Target framework (default: net8.0)
  -o, --output-root <path>               Publish output root
      --skip-configurator-publish        Skip publishing SyncForge.Configurator
      --skip-plugin-publish              Skip publishing plugins
  -h, --help                             Show this help
EOF
      exit 0
      ;;
    *)
      echo "Unknown argument: $1"
      exit 1
      ;;
  esac
done

SCRIPT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIGURATOR_ROOT="$(cd "$SCRIPT_ROOT/.." && pwd)"
WORKSPACE_ROOT="$(cd "$CONFIGURATOR_ROOT/.." && pwd)"

if [[ -z "$OUTPUT_ROOT" ]]; then
  OUTPUT_ROOT="$CONFIGURATOR_ROOT/artifacts/publish"
fi

CONFIGURATOR_PROJECT="$CONFIGURATOR_ROOT/SyncForge.Configurator.csproj"
PLUGINS_OUTPUT_ROOT="$OUTPUT_ROOT/plugins"

PLUGIN_PROJECTS=(
  "$WORKSPACE_ROOT/SyncForge/src/SyncForge.Plugin.Csv/SyncForge.Plugin.Csv.csproj"
  "$WORKSPACE_ROOT/SyncForge/src/SyncForge.Plugin.Excel/SyncForge.Plugin.Excel.csproj"
  "$WORKSPACE_ROOT/SyncForge/src/SyncForge.Plugin.Rest/SyncForge.Plugin.Rest.csproj"
  "$WORKSPACE_ROOT/SyncForge.Plugin.MsSql/SyncForge.Plugin.MsSql.csproj"
)

echo "Workspace root: $WORKSPACE_ROOT"
echo "Configurator root: $CONFIGURATOR_ROOT"
echo "Output root: $OUTPUT_ROOT"

mkdir -p "$OUTPUT_ROOT"
mkdir -p "$PLUGINS_OUTPUT_ROOT"

if [[ "$SKIP_CONFIGURATOR_PUBLISH" != "true" ]]; then
  echo "Publishing SyncForge.Configurator..."
  dotnet publish "$CONFIGURATOR_PROJECT" -c "$CONFIGURATION" -f "$FRAMEWORK" -o "$OUTPUT_ROOT"
fi

if [[ "$SKIP_PLUGIN_PUBLISH" != "true" ]]; then
  for plugin_project in "${PLUGIN_PROJECTS[@]}"; do
    if [[ ! -f "$plugin_project" ]]; then
      echo "Warning: Plugin project not found, skipping: $plugin_project"
      continue
    fi

    plugin_name="$(basename "$plugin_project" .csproj)"
    plugin_output="$PLUGINS_OUTPUT_ROOT/$plugin_name"
    mkdir -p "$plugin_output"

    echo "Publishing plugin $plugin_name..."
    dotnet publish "$plugin_project" -c "$CONFIGURATION" -f "$FRAMEWORK" -o "$plugin_output"
  done
fi

echo "Build complete."
echo "Run Configurator from: $OUTPUT_ROOT"
echo "Plugin directory in UI: $PLUGINS_OUTPUT_ROOT"
