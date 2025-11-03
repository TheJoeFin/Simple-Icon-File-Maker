@echo off
echo ================================================================
echo  PreviewStack Benchmark - Test Image Setup
echo ================================================================
echo.

set "BUILD_DIR=bin\Release\net9.0-windows10.0.22621"
set "TARGET_FILE=%BUILD_DIR%\Perf.png"

if not exist "%BUILD_DIR%" (
    echo Creating build directory...
    mkdir "%BUILD_DIR%"
)

echo This script helps you set up a test image for the benchmarks.
echo.
echo The test image should be:
echo   - A PNG file
echo   - Recommended size: 512x512 pixels or larger
echo   - Named: Perf.png
echo.
echo Options:
echo   1. I'll drag and drop an image file
echo   2. I'll manually copy it later
echo 3. Exit
echo.

choice /C 123 /N /M "Select an option (1-3): "

if errorlevel 3 goto :EOF
if errorlevel 2 goto :ManualCopy
if errorlevel 1 goto :DragDrop

:DragDrop
echo.
echo Drag and drop your PNG file here, then press Enter:
set /p "SOURCE_FILE="

rem Remove quotes if present
set "SOURCE_FILE=%SOURCE_FILE:"=%"

if not exist "%SOURCE_FILE%" (
    echo.
    echo Error: File not found: %SOURCE_FILE%
    echo.
    pause
    goto :EOF
)

rem Check if it's a PNG file
echo %SOURCE_FILE% | findstr /i ".png" >nul
if errorlevel 1 (
    echo.
    echo Warning: This doesn't appear to be a PNG file.
    echo The benchmarks expect a PNG image.
    choice /C YN /M "Continue anyway? (Y/N)"
    if errorlevel 2 goto :EOF
)

echo.
echo Copying %SOURCE_FILE%
echo      to %TARGET_FILE%...

copy /Y "%SOURCE_FILE%" "%TARGET_FILE%" >nul

if exist "%TARGET_FILE%" (
    echo.
    echo ? Success! Test image installed.
    echo.
    echo You can now run the benchmarks with:
    echo   dotnet run -c Release
    echo.
) else (
    echo.
echo ? Failed to copy the file.
    echo.
)

pause
goto :EOF

:ManualCopy
echo.
echo Manual Setup Instructions:
echo ?????????????????????????????????????????
echo.
echo 1. Choose a PNG image (512x512 or larger recommended)
echo 2. Rename it to: Perf.png
echo 3. Copy it to: %BUILD_DIR%\
echo.
echo Full path: %CD%\%TARGET_FILE%
echo.
echo After copying, run:
echo   dotnet run -c Release
echo.
pause
goto :EOF
