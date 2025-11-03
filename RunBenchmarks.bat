@echo off
echo ============================================
echo PreviewStack Performance Benchmark Runner
echo ============================================
echo.

cd "Simple Icon File Maker.Benchmarks"

REM Check if Perf.png exists in the build output
set "BUILD_DIR=bin\Release\net9.0-windows10.0.22621"
set "TEST_IMAGE=%BUILD_DIR%\Perf.png"

if exist "%TEST_IMAGE%" (
    echo ? Test image found: Perf.png
    echo   Image processing benchmarks will run.
) else (
    echo ? Test image not found: Perf.png
 echo   Image processing benchmarks will be skipped.
    echo.
    echo   To enable image benchmarks:
    echo     1. Run: SetupTestImage.bat
    echo     2. Or manually copy a PNG to: %BUILD_DIR%\Perf.png
    echo.
    choice /C YN /M "Continue without image benchmarks? (Y/N)"
    if errorlevel 2 (
        echo.
      echo Run SetupTestImage.bat to configure a test image.
    pause
        exit /b 0
    )
)

echo.
echo Building benchmark project in Release mode...
dotnet build -c Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build failed! Please check the errors above.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Running benchmarks...
echo This may take several minutes. Please be patient.
echo.

dotnet run -c Release --no-build

echo.
echo ============================================
echo Benchmarks complete!
echo ============================================
echo.
echo ?? Results saved to:
echo  BenchmarkDotNet.Artifacts\results\
echo.
echo ?? Check these files:
echo    - PreviewStackBenchmarks-report.html (Interactive)
echo    - PreviewStackBenchmarks-report.csv  (Data)
echo    - PreviewStackBenchmarks-report.md   (Summary)
echo.
pause
