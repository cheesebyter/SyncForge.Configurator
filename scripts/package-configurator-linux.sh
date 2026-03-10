#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="Release"
FRAMEWORK="net8.0"
RUNTIME_IDENTIFIER="linux-x64"
SELF_CONTAINED="true"
SKIP_BUILD="false"
OUTPUT_ROOT=""
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
    -r|--runtime)
      RUNTIME_IDENTIFIER="$2"
      shift 2
      ;;
    --self-contained)
      SELF_CONTAINED="$2"
      shift 2
      ;;
    --skip-build)
      SKIP_BUILD="true"
      shift
      ;;
    -o|--output-root)
      OUTPUT_ROOT="$2"
      shift 2
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
Usage: package-configurator-linux.sh [options]

Options:
  -c, --configuration <Debug|Release>   Build configuration (default: Release)
  -f, --framework <TFM>                 Target framework (default: net8.0)
  -r, --runtime <RID>                   Runtime identifier (default: linux-x64)
      --self-contained <true|false>     Publish self-contained (default: true)
      --skip-build                       Skip build-configurator-with-plugins step
  -o, --output-root <path>              Output package directory
      --version <value>                  Semantic version metadata (default: 0.2.1)
      --commit <sha>                     Source commit metadata (default: git HEAD)
      --build-timestamp-utc <iso8601>    Build timestamp metadata (default: now)
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
PROJECT_FILE="$CONFIGURATOR_ROOT/SyncForge.Configurator.csproj"

if [[ -z "$OUTPUT_ROOT" ]]; then
  OUTPUT_ROOT="$CONFIGURATOR_ROOT/artifacts/packages/linux"
fi

STAGING_ROOT="$OUTPUT_ROOT/staging"
PUBLISH_ROOT="$STAGING_ROOT/publish"
mkdir -p "$OUTPUT_ROOT"
rm -rf "$STAGING_ROOT"
mkdir -p "$PUBLISH_ROOT"

if [[ "$SKIP_BUILD" != "true" ]]; then
  "$SCRIPT_ROOT/build-configurator-with-plugins.sh" \
    --configuration "$CONFIGURATION" \
    --framework "$FRAMEWORK" \
    --output-root "$PUBLISH_ROOT" \
    --version "$VERSION" \
    --commit "$COMMIT" \
    --build-timestamp-utc "$BUILD_TIMESTAMP_UTC"
fi

if [[ "$SELF_CONTAINED" == "true" ]]; then
  SELFCONTAINED_ROOT="$STAGING_ROOT/selfcontained"
  mkdir -p "$SELFCONTAINED_ROOT"

  dotnet publish "$PROJECT_FILE" \
    -c "$CONFIGURATION" \
    -f "$FRAMEWORK" \
    -r "$RUNTIME_IDENTIFIER" \
    --self-contained true \
    -o "$SELFCONTAINED_ROOT" \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true

  if [[ -d "$PUBLISH_ROOT/plugins" ]]; then
    cp -R "$PUBLISH_ROOT/plugins" "$SELFCONTAINED_ROOT/plugins"
  fi

  rm -rf "$PUBLISH_ROOT"
  mkdir -p "$PUBLISH_ROOT"
  cp -R "$SELFCONTAINED_ROOT"/* "$PUBLISH_ROOT"/
fi

if [[ "$SELF_CONTAINED" == "true" ]]; then
  ARCHIVE_NAME="SyncForge.Configurator-linux-selfcontained-$RUNTIME_IDENTIFIER.tar.gz"
else
  ARCHIVE_NAME="SyncForge.Configurator-linux-framework-dependent.tar.gz"
fi
ARCHIVE_PATH="$OUTPUT_ROOT/$ARCHIVE_NAME"

rm -f "$ARCHIVE_PATH"
(
  cd "$PUBLISH_ROOT"
  tar -czf "$ARCHIVE_PATH" .
)

echo "Linux package created: $ARCHIVE_PATH"
