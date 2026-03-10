#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="Release"
FRAMEWORK="net8.0"
OUTPUT_ROOT=""
SKIP_CONFIGURATOR_PUBLISH="false"
SKIP_PLUGIN_PUBLISH="false"
VERSION="0.2.1"
COMMIT=""
BUILD_TIMESTAMP_UTC=""

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
    --version)
      VERSION="$2"
      shift 2
      ;;
    --commit)
      COMMIT="$2"
      shift 2
      ;;
    --build-timestamp-utc)
      BUILD_TIMESTAMP_UTC="$2"
      shift 2
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
      --version <value>                  Semantic version metadata (default: 0.2.1)
      --commit <sha>                     Source commit metadata (default: git HEAD)
      --build-timestamp-utc <iso8601>    Build timestamp metadata (default: now)
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

if [[ -z "$COMMIT" ]]; then
  if command -v git >/dev/null 2>&1; then
    COMMIT="$(git -C "$WORKSPACE_ROOT" rev-parse --verify HEAD 2>/dev/null || true)"
  fi
  if [[ -z "$COMMIT" ]]; then
    COMMIT="unknown"
  fi
fi

if [[ -z "$BUILD_TIMESTAMP_UTC" ]]; then
  BUILD_TIMESTAMP_UTC="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
fi

INFORMATIONAL_VERSION="$VERSION+$COMMIT"

deterministic_props=(
  "-p:ContinuousIntegrationBuild=true"
  "-p:Deterministic=true"
  "-p:Version=$VERSION"
  "-p:InformationalVersion=$INFORMATIONAL_VERSION"
  "-p:SourceRevisionId=$COMMIT"
  "-p:RepositoryCommit=$COMMIT"
)

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
  dotnet publish "$CONFIGURATOR_PROJECT" -c "$CONFIGURATION" -f "$FRAMEWORK" -o "$OUTPUT_ROOT" "${deterministic_props[@]}"
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
    dotnet publish "$plugin_project" -c "$CONFIGURATION" -f "$FRAMEWORK" -o "$plugin_output" "${deterministic_props[@]}"
  done
fi

TRUST_FILE="$OUTPUT_ROOT/trusted-plugins.json"
tmp_plugins_json=""
if command -v python3 >/dev/null 2>&1; then
  tmp_plugins_json="$(mktemp)"
  {
    echo "["
    first=true
    while IFS= read -r dll; do
      assembly_name="$(basename "$dll" .dll)"
      sha256="$(sha256sum "$dll" | awk '{print $1}')"
      if [[ "$first" == true ]]; then
        first=false
      else
        echo ","
      fi
      printf '  {"assemblyName":"%s","sha256":"%s"}' "$assembly_name" "$sha256"
    done < <(find "$PLUGINS_OUTPUT_ROOT" -type f -name 'SyncForge.Plugin.*.dll' | sort)
    echo
    echo "]"
  } > "$tmp_plugins_json"

  python3 - <<PY
import json
from pathlib import Path
plugins = json.loads(Path("$tmp_plugins_json").read_text(encoding="utf-8"))
doc = {"plugins": plugins}
Path("$TRUST_FILE").write_text(json.dumps(doc, indent=2), encoding="utf-8")
PY
  rm -f "$tmp_plugins_json"
else
  echo '{"plugins":[]}' > "$TRUST_FILE"
fi

METADATA_FILE="$OUTPUT_ROOT/build-metadata.json"
cat > "$METADATA_FILE" <<EOF
{
  "version": "$VERSION",
  "commit": "$COMMIT",
  "buildTimestampUtc": "$BUILD_TIMESTAMP_UTC",
  "configuration": "$CONFIGURATION",
  "framework": "$FRAMEWORK",
  "outputRoot": "$OUTPUT_ROOT",
  "pluginsOutputRoot": "$PLUGINS_OUTPUT_ROOT",
  "trustedPluginsFile": "$TRUST_FILE"
}
EOF

echo "Build complete."
echo "Run Configurator from: $OUTPUT_ROOT"
echo "Plugin directory in UI: $PLUGINS_OUTPUT_ROOT"
echo "Build metadata: $METADATA_FILE"
echo "Trusted plugin manifest: $TRUST_FILE"
