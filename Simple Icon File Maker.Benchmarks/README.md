# PreviewStack Performance Benchmarks

This project contains comprehensive benchmarks for the PreviewStack optimizations.

## Running the Benchmarks

```bash
cd "Simple Icon File Maker.Benchmarks"
dotnet run -c Release
```

## Test Image Setup (Optional but Recommended)

For more realistic benchmarks, place a test image named **`Perf.png`** in the benchmark output directory:

### Option 1: Copy to Build Output
```bash
# Copy your test image to the build output
copy YourTestImage.png "bin\Release\net9.0-windows10.0.22621\Perf.png"
```

### Option 2: Create a Sample Image
Use any PNG image (recommended: 512x512 or larger) and name it `Perf.png`

**Recommended Test Image Specs:**
- Format: PNG
- Size: 512x512 pixels or larger
- Purpose: Tests realistic image processing operations (resize, scale, sharpen)

### What Happens Without Perf.png?

The benchmarks will still run all collection and algorithm tests. You'll see:
```
? Test image not found at: [path]
  Image processing benchmarks will be skipped.
  Add a Perf.png file to the benchmark directory to enable them.
```

Image processing benchmarks will return early and show minimal impact on results.

## What's Being Tested

### 1. **FilterSelectedSizes** - LINQ vs For Loops
- Tests the performance difference between LINQ (`Where().Select()`) and manual for loops
- Includes tests with both small (7 items) and large (100 items) collections

### 2. **UpdateIsEnabled** - Assignment Optimization
- Compares `foreach` with two assignments vs `for` with single conditional assignment
- Tests the impact of reducing property setter calls

### 3. **CheckIfRefreshIsNeeded** - Collection Comparison
- Original: LINQ + `All().Contains()` (O(n²) complexity)
- Optimized: For loops + `HashSet` (O(n) complexity)
- This should show dramatic improvement for larger datasets

### 4. **ChooseTheseSizes** - LINQ vs Direct Iteration
- Measures the overhead of LINQ query construction and execution

### 5. **String Operations** - Parsing Optimization
- `Split()` vs `IndexOf()` + `Substring()`
- Also includes span-based alternative for .NET 9

### 6. **Image Processing** (requires Perf.png)
- **Get dimensions**: Simple property access
- **Find smaller side**: LINQ Min() vs Math.Min()
- **Resize operations**: 256x256 and 64x64 scaling
- **Scale and sharpen**: Typical icon generation workflow

### 7. **Collection Operations**
- foreach with deconstruction vs for with indexed access

## Expected Results

Based on typical .NET performance characteristics:

| Operation | Expected Improvement | Memory Reduction |
|-----------|---------------------|------------------|
| FilterSelectedSizes | 15-30% faster | 20-40% less |
| UpdateIsEnabled | 10-20% faster | 10-15% less |
| CheckIfRefreshIsNeeded | 30-50% faster | 15-25% less |
| String Operations | 5-15% faster | 10-20% less |
| Collection Operations | 5-10% faster | 10-15% less |
| **Image Processing** | **5-20% faster** | **Varies by operation** |

## Interpreting Results

Look for:
- **Mean**: Average execution time (lower is better)
- **Allocated**: Memory allocated per operation (lower is better)
- **Gen0/Gen1/Gen2**: Garbage collection pressure (lower is better)
- **Ratio**: Comparison to baseline (1.00 = same as baseline, 0.50 = 2x faster)

### Example Output
```
| Method         | Mean    | Allocated | Ratio |
|------------------------------------- |-----------:|----------:|------:|
| LINQ Where().Select()      | 45.26 ns   |     232 B |  1.00 |
| For loop with List      | 14.72 ns   |     152 B |  0.33 | <- 3x faster!
```

## Image Processing Benchmarks

When `Perf.png` is available, you'll see additional benchmarks:

```
| Method           | Mean        | Allocated |
|------------------------------------------ |------------:|----------:|
| Image: Get dimensions          |   ~5-10 ns  |       0 B |
| Image: Find smaller side - LINQ           |   ~20-30 ns |      32 B |
| Image: Find smaller side - Math.Min       |   ~5-10 ns  |       0 B |
| Image: Clone and resize to 256x256        |   ~5-15 ms  |    ~256KB |
| Image: Clone and resize to 64x64    |   ~2-8 ms   |    ~16KB  |
| Image: Scale and sharpen        |   ~8-20 ms  |    ~64KB  |
```

**Note:** Image processing times are in milliseconds (ms) vs nanoseconds (ns) for collection operations.

## Next Steps

After reviewing the benchmark results:
1. Apply optimizations that show significant improvement (>10%)
2. Consider trade-offs between code readability and performance
3. Re-run benchmarks after applying changes to verify improvements

## Troubleshooting

### "Test image not found"
- This is informational only - collection benchmarks still run
- Add a `Perf.png` file to enable image processing tests

### Build Errors
```bash
cd "Simple Icon File Maker.Benchmarks"
dotnet restore
dotnet build -c Release
```

### Benchmark Crashes or Errors
- Ensure you're running in Release mode (`-c Release`)
- Check that the main project builds successfully
- Verify `Perf.png` is a valid PNG image if using image benchmarks

## Output Files

Results are saved to:
```
BenchmarkDotNet.Artifacts/results/
??? PreviewStackBenchmarks-report.html    (Interactive report)
??? PreviewStackBenchmarks-report.csv     (Data export)
??? PreviewStackBenchmarks-report.md      (Markdown summary)
