# ?? Perf.png Quick Reference

## Setup (One Time)

### Windows
```cmd
cd "Simple Icon File Maker.Benchmarks"
SetupTestImage.bat
```

### Linux/Mac
```bash
cd "Simple Icon File Maker.Benchmarks"
chmod +x SetupTestImage.sh
./SetupTestImage.sh
```

### Manual
Copy any PNG (512x512+ recommended) to:
```
Simple Icon File Maker.Benchmarks/
  bin/Release/net9.0-windows10.0.22621/Perf.png
```

## Run Benchmarks

```cmd
RunBenchmarks.bat   # Windows
./RunBenchmarks.sh  # Linux/Mac
```

## What Perf.png Enables

| Without Perf.png | With Perf.png |
|------------------|---------------|
| ? Collection benchmarks | ? Collection benchmarks |
| ? Algorithm benchmarks | ? Algorithm benchmarks |
| ? String operations | ? String operations |
| ? Image processing | ? **Image processing** |

## New Benchmarks

? Get image dimensions  
? Find smaller side (LINQ vs Math.Min)  
? Resize to 256x256  
? Resize to 64x64  
? Scale and sharpen (real workflow)

## Expected Results

### Math.Min vs LINQ
```
LINQ:     ~25 ns | 32 B
Math.Min:  ~6 ns | 0 B  ? 4x faster!
```

### Image Processing
```
Get dimensions: < 10 ns
Resize 256x256: ~8 ms | ~256 KB
Resize 64x64:   ~3 ms | ~16 KB
Scale+Sharpen:  ~12 ms | ~64 KB
```

## Troubleshooting

? **"Test image not found"**  
? Run SetupTestImage script or copy PNG manually

? **Build fails**  
? `dotnet restore && dotnet build -c Release`

? **Benchmarks show "NA"**  
? Check Perf.png is valid PNG and accessible

## Documentation

- ?? **PERF_IMAGE_GUIDE.md** - Full guide
- ?? **README.md** - Benchmark documentation
- ?? **UPDATE_SUMMARY.md** - What changed

## Quick Commands

```bash
# Setup
cd "Simple Icon File Maker.Benchmarks"
SetupTestImage.bat

# Build
dotnet build -c Release

# Run
dotnet run -c Release

# Or use scripts
..\RunBenchmarks.bat
```

## Pro Tip ??

Run benchmarks **twice**:
1. Without Perf.png (collection/algorithm focus)
2. With Perf.png (+ image processing)

Compare results to see the full picture!

---

? **Ready to benchmark? Add Perf.png and run!** ?
