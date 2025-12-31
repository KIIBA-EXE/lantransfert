@echo off
REM KITRANSFERT Build Script for Windows
REM Builds self-contained executables for all platforms

echo 🔨 Building KITRANSFERT...
echo ==========================

set PROJECT_DIR=%~dp0
set OUTPUT_DIR=%PROJECT_DIR%releases
set PROJECT_PATH=%PROJECT_DIR%src\LanTransfer.Desktop\LanTransfer.Desktop.csproj

REM Clean previous builds
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%"

echo.
echo 📦 Building for Windows x64...
dotnet publish "%PROJECT_PATH%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUTPUT_DIR%\win-x64"
move "%OUTPUT_DIR%\win-x64\LanTransfer.Desktop.exe" "%OUTPUT_DIR%\LanTransfer-win-x64.exe"
rmdir /s /q "%OUTPUT_DIR%\win-x64"
echo ✅ Built: LanTransfer-win-x64.exe

echo.
echo 📦 Building for Linux x64...
dotnet publish "%PROJECT_PATH%" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUTPUT_DIR%\linux-x64"
move "%OUTPUT_DIR%\linux-x64\LanTransfer.Desktop" "%OUTPUT_DIR%\LanTransfer-linux-x64"
rmdir /s /q "%OUTPUT_DIR%\linux-x64"
echo ✅ Built: LanTransfer-linux-x64

echo.
echo 📦 Building for macOS Intel...
dotnet publish "%PROJECT_PATH%" -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUTPUT_DIR%\osx-x64"
move "%OUTPUT_DIR%\osx-x64\LanTransfer.Desktop" "%OUTPUT_DIR%\LanTransfer-osx-x64"
rmdir /s /q "%OUTPUT_DIR%\osx-x64"
echo ✅ Built: LanTransfer-osx-x64

echo.
echo 📦 Building for macOS Apple Silicon...
dotnet publish "%PROJECT_PATH%" -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUTPUT_DIR%\osx-arm64"
move "%OUTPUT_DIR%\osx-arm64\LanTransfer.Desktop" "%OUTPUT_DIR%\LanTransfer-osx-arm64"
rmdir /s /q "%OUTPUT_DIR%\osx-arm64"
echo ✅ Built: LanTransfer-osx-arm64

echo.
echo 🎉 Build complete!
echo ==================
echo Executables are in: %OUTPUT_DIR%
dir "%OUTPUT_DIR%"
pause
