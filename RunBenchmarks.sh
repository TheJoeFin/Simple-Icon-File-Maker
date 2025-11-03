#!/bin/bash

echo "============================================"
echo "PreviewStack Performance Benchmark Runner"
echo "============================================"
echo ""

cd "Simple Icon File Maker.Benchmarks"

# Check if Perf.png exists in the build output
BUILD_DIR="bin/Release/net9.0-windows10.0.22621"
TEST_IMAGE="$BUILD_DIR/Perf.png"

if [ -f "$TEST_IMAGE" ]; then
    echo "? Test image found: Perf.png"
    echo "  Image processing benchmarks will run."
else
    echo "? Test image not found: Perf.png"
    echo "  Image processing benchmarks will be skipped."
    echo ""
    echo "  To enable image benchmarks:"
    echo "    1. Run: ./SetupTestImage.sh"
    echo "  2. Or manually copy a PNG to: $BUILD_DIR/Perf.png"
    echo ""
    read -p "Continue without image benchmarks? (y/n): " choice
    if [[ ! "$choice" =~ ^[Yy]$ ]]; then
        echo ""
        echo "Run ./SetupTestImage.sh to configure a test image."
        exit 0
    fi
fi

echo ""
echo "Building benchmark project in Release mode..."
dotnet build -c Release

if [ $? -ne 0 ]; then
    echo ""
    echo "Build failed! Please check the errors above."
    exit 1
fi

echo ""
echo "Running benchmarks..."
echo "This may take several minutes. Please be patient."
echo ""

dotnet run -c Release --no-build

echo ""
echo "============================================"
echo "Benchmarks complete!"
echo "============================================"
echo ""
echo "?? Results saved to:"
echo "  BenchmarkDotNet.Artifacts/results/"
echo ""
echo "?? Check these files:"
echo "   - PreviewStackBenchmarks-report.html (Interactive)"
echo "   - PreviewStackBenchmarks-report.csv  (Data)"
echo "   - PreviewStackBenchmarks-report.md   (Summary)"
echo ""
