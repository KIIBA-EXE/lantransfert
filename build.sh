#!/bin/bash
# KITRANSFERT Build Script
# Builds self-contained executables for all platforms

set -e

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUTPUT_DIR="$PROJECT_DIR/releases"
PROJECT_PATH="$PROJECT_DIR/src/LanTransfer.Desktop/LanTransfer.Desktop.csproj"

echo "🔨 Building KITRANSFERT..."
echo "=========================="

# Clean previous builds
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Build for each platform
build_platform() {
    local RID=$1
    local NAME=$2
    local EXT=$3
    
    echo ""
    echo "📦 Building for $NAME ($RID)..."
    
    dotnet publish "$PROJECT_PATH" \
        -c Release \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:EnableCompressionInSingleFile=true \
        -o "$OUTPUT_DIR/$RID"
    
    # Rename executable
    if [ -n "$EXT" ]; then
        mv "$OUTPUT_DIR/$RID/LanTransfer.Desktop$EXT" "$OUTPUT_DIR/LanTransfer-$RID$EXT"
    else
        mv "$OUTPUT_DIR/$RID/LanTransfer.Desktop" "$OUTPUT_DIR/LanTransfer-$RID"
        chmod +x "$OUTPUT_DIR/LanTransfer-$RID"
    fi
    
    # Clean up intermediate files
    rm -rf "$OUTPUT_DIR/$RID"
    
    echo "✅ Built: LanTransfer-$RID$EXT"
}

# Build for Windows x64
build_platform "win-x64" "Windows x64" ".exe"

# Build for Linux x64
build_platform "linux-x64" "Linux x64" ""

# Build for macOS x64 (Intel)
build_platform "osx-x64" "macOS Intel" ""

# Build for macOS ARM (Apple Silicon)
build_platform "osx-arm64" "macOS Apple Silicon" ""

echo ""
echo "🎉 Build complete!"
echo "=================="
echo "Executables are in: $OUTPUT_DIR"
ls -la "$OUTPUT_DIR"
