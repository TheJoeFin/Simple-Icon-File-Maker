using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ImageMagick;
using Simple_Icon_File_Maker.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simple_Icon_File_Maker.Benchmarks;

/// <summary>
/// Benchmarks for PreviewStack optimization opportunities.
/// Run with: dotnet run -c Release
/// 
/// Note: Place a 'Perf.png' file in the benchmark directory for image processing tests.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class PreviewStackBenchmarks
{
    private List<IconSize> _iconSizes = null!;
    private List<IconSize> _largeIconSizeList = null!;
    private List<(string, string)> _imagePaths = null!;
    private string? _testImagePath;
    private MagickImage? _testImage;
 private bool _hasTestImage;

    [GlobalSetup]
    public void Setup()
    {
        // Small list - typical scenario
        _iconSizes = new List<IconSize>
        {
 new(16) { IsSelected = true, IsEnabled = true },
            new(32) { IsSelected = true, IsEnabled = true },
            new(48) { IsSelected = true, IsEnabled = true },
   new(64) { IsSelected = false, IsEnabled = true },
          new(128) { IsSelected = true, IsEnabled = true },
            new(256) { IsSelected = true, IsEnabled = true },
    new(512) { IsSelected = false, IsEnabled = true }
        };

        // Large list - stress test
  _largeIconSizeList = new List<IconSize>(100);
    for (int i = 0; i < 100; i++)
        {
    _largeIconSizeList.Add(new IconSize(16 + i * 4)
         {
                IsSelected = i % 2 == 0,
      IsEnabled = i % 3 != 0
            });
        }

        // Image paths for CheckIfRefreshIsNeeded
        _imagePaths = new List<(string, string)>
        {
            ("16", "path1.png"),
            ("32", "path2.png"),
 ("48", "path3.png"),
            ("128", "path4.png"),
   ("256", "path5.png")
        };

        // Try to load test image if available
        string benchmarkDir = Path.GetDirectoryName(typeof(PreviewStackBenchmarks).Assembly.Location) ?? "";
        _testImagePath = Path.Combine(benchmarkDir, "Perf.png");

        if (File.Exists(_testImagePath))
   {
      try
   {
         _testImage = new MagickImage(_testImagePath);
                _hasTestImage = true;
   Console.WriteLine($"? Loaded test image: {_testImagePath} ({_testImage.Width}x{_testImage.Height})");
         }
            catch (Exception ex)
     {
     Console.WriteLine($"? Failed to load test image: {ex.Message}");
       _hasTestImage = false;
   }
  }
        else
        {
            Console.WriteLine($"? Test image not found at: {_testImagePath}");
   Console.WriteLine("  Image processing benchmarks will be skipped.");
         Console.WriteLine("  Add a Perf.png file to the benchmark directory to enable them.");
        _hasTestImage = false;
}
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _testImage?.Dispose();
    }

    #region FilterSelectedSizes Benchmarks

    [Benchmark(Baseline = true, Description = "LINQ Where().Select()")]
    public List<int> FilterSelectedSizes_LINQ()
    {
     return _iconSizes
            .Where(s => s.IsSelected == true)
            .Select(s => s.SideLength)
    .ToList();
    }

    [Benchmark(Description = "For loop with List")]
    public List<int> FilterSelectedSizes_ForLoop()
    {
 List<int> result = new(_iconSizes.Count);
        for (int i = 0; i < _iconSizes.Count; i++)
{
  if (_iconSizes[i].IsSelected)
result.Add(_iconSizes[i].SideLength);
 }
        return result;
    }

    [Benchmark(Description = "For loop with exact capacity")]
    public List<int> FilterSelectedSizes_ForLoop_PreAllocated()
 {
    List<int> result = new(capacity: 10);
        for (int i = 0; i < _iconSizes.Count; i++)
        {
       if (_iconSizes[i].IsSelected)
    result.Add(_iconSizes[i].SideLength);
        }
        return result;
    }

    #endregion

    #region Large List Benchmarks

    [Benchmark(Description = "LINQ Where().Select() - Large")]
    public List<int> FilterSelectedSizes_LINQ_Large()
    {
        return _largeIconSizeList
  .Where(s => s.IsSelected)
            .Select(s => s.SideLength)
            .ToList();
 }

    [Benchmark(Description = "For loop - Large")]
    public List<int> FilterSelectedSizes_ForLoop_Large()
    {
        List<int> result = new(_largeIconSizeList.Count);
        for (int i = 0; i < _largeIconSizeList.Count; i++)
        {
            if (_largeIconSizeList[i].IsSelected)
       result.Add(_largeIconSizeList[i].SideLength);
  }
        return result;
    }

    #endregion

    #region UpdateIsEnabled Benchmarks

    [Benchmark(Description = "Original: foreach with two assignments")]
    public void UpdateIsEnabled_Original()
    {
int smallerSide = 256;
    foreach (IconSize iconSize in _iconSizes)
        {
   iconSize.IsEnabled = true;
         if (iconSize.SideLength > smallerSide)
      iconSize.IsEnabled = false;
        }
    }

    [Benchmark(Description = "Optimized: for with single assignment")]
    public void UpdateIsEnabled_Optimized()
    {
        int smallerSide = 256;
        for (int i = 0; i < _iconSizes.Count; i++)
     {
   _iconSizes[i].IsEnabled = _iconSizes[i].SideLength <= smallerSide;
        }
    }

    [Benchmark(Description = "Original: foreach - Large")]
    public void UpdateIsEnabled_Original_Large()
    {
        int smallerSide = 256;
   foreach (IconSize iconSize in _largeIconSizeList)
        {
            iconSize.IsEnabled = true;
            if (iconSize.SideLength > smallerSide)
         iconSize.IsEnabled = false;
        }
    }

    [Benchmark(Description = "Optimized: for - Large")]
    public void UpdateIsEnabled_Optimized_Large()
    {
        int smallerSide = 256;
    for (int i = 0; i < _largeIconSizeList.Count; i++)
        {
        _largeIconSizeList[i].IsEnabled = _largeIconSizeList[i].SideLength <= smallerSide;
        }
    }

    #endregion

    #region CheckIfRefreshIsNeeded Benchmarks

    [Benchmark(Description = "Original: LINQ + All().Contains()")]
    public bool CheckIfRefreshIsNeeded_Original()
    {
    if (_imagePaths.Count < 1)
 return true;

List<int> selectedSideLengths = _iconSizes
      .Where(i => i.IsSelected)
    .Select(i => i.SideLength)
            .ToList();

        List<int> generatedSideLengths = new();

        foreach ((string sideLength, string path) pair in _imagePaths)
if (int.TryParse(pair.sideLength, out int sideLength))
    generatedSideLengths.Add(sideLength);

        if (selectedSideLengths.Count != generatedSideLengths.Count)
          return true;

        return !generatedSideLengths.All(selectedSideLengths.Contains);
    }

    [Benchmark(Description = "Optimized: For loops + HashSet")]
  public bool CheckIfRefreshIsNeeded_Optimized()
    {
        if (_imagePaths.Count < 1)
            return true;

        List<int> selectedSideLengths = new(_iconSizes.Count);
        for (int i = 0; i < _iconSizes.Count; i++)
        {
 if (_iconSizes[i].IsSelected)
   selectedSideLengths.Add(_iconSizes[i].SideLength);
      }

        List<int> generatedSideLengths = new(_imagePaths.Count);
        for (int i = 0; i < _imagePaths.Count; i++)
        {
 if (int.TryParse(_imagePaths[i].Item1, out int sideLength))
         generatedSideLengths.Add(sideLength);
  }

    if (selectedSideLengths.Count != generatedSideLengths.Count)
 return true;

        HashSet<int> generatedSet = new(generatedSideLengths);
        for (int i = 0; i < selectedSideLengths.Count; i++)
        {
  if (!generatedSet.Contains(selectedSideLengths[i]))
         return true;
        }

        return false;
    }

    #endregion

    #region ChooseTheseSizes Benchmarks

    [Benchmark(Description = "Original: LINQ + collection expression")]
    public List<IconSize> ChooseTheseSizes_Original()
    {
        List<IconSize> selectedSizes = _iconSizes
    .Where(x => x.IsSelected && x.IsEnabled && x.SideLength <= 512)
  .ToList();

     List<IconSize> result = new();
        result.AddRange(selectedSizes);
        return result;
    }

 [Benchmark(Description = "Optimized: Direct foreach")]
    public List<IconSize> ChooseTheseSizes_Optimized()
    {
        List<IconSize> result = new();

     foreach (IconSize size in _iconSizes)
 {
          if (size.IsSelected && size.IsEnabled && size.SideLength <= 512)
                result.Add(size);
        }

    return result;
    }

    #endregion

    #region String Operations Benchmarks

    [Benchmark(Description = "Original: Split() for parsing")]
    public string ParseImageName_Original()
    {
        string fileName = "904466899Image16.png";
     string justFileName = Path.GetFileNameWithoutExtension(fileName);
    string sideLength = justFileName.Split("Image")[1];
 return sideLength;
    }

    [Benchmark(Description = "Optimized: IndexOf() + Substring()")]
    public string ParseImageName_Optimized()
    {
        string fileName = "904466899Image16.png";
        string justFileName = Path.GetFileNameWithoutExtension(fileName);
   int imageIndex = justFileName.IndexOf("Image", StringComparison.Ordinal);
 if (imageIndex < 0)
            return string.Empty;

        string sideLength = justFileName.Substring(imageIndex + 5);
        return sideLength;
    }

    [Benchmark(Description = "Alternative: Span-based")]
    public string ParseImageName_Span()
 {
      string fileName = "904466899Image16.png";
        string justFileName = Path.GetFileNameWithoutExtension(fileName);
        ReadOnlySpan<char> span = justFileName.AsSpan();
     int imageIndex = span.IndexOf("Image".AsSpan(), StringComparison.Ordinal);
   if (imageIndex < 0)
            return string.Empty;

        return span.Slice(imageIndex + 5).ToString();
    }

    #endregion

    #region Image Processing Benchmarks (requires Perf.png)

    [Benchmark(Description = "Image: Get dimensions")]
  public (int width, int height) GetImageDimensions()
    {
  if (!_hasTestImage || _testImage == null)
            return (0, 0);

        return ((int)_testImage.Width, (int)_testImage.Height);
    }

    [Benchmark(Description = "Image: Find smaller side - LINQ")]
    public int FindSmallerSide_LINQ()
    {
  if (!_hasTestImage || _testImage == null)
            return 0;

   var dimensions = new[] { (int)_testImage.Width, (int)_testImage.Height };
  return dimensions.Min();
    }

    [Benchmark(Description = "Image: Find smaller side - Math.Min")]
    public int FindSmallerSide_MathMin()
    {
if (!_hasTestImage || _testImage == null)
       return 0;

        return Math.Min((int)_testImage.Width, (int)_testImage.Height);
    }

    [Benchmark(Description = "Image: Clone and resize to 256x256")]
    public MagickImage? ResizeImage_256()
    {
        if (!_hasTestImage || _testImage == null)
            return null;

        MagickImage clone = (MagickImage)_testImage.Clone();
        clone.Resize(256, 256);
        return clone;
    }

    [Benchmark(Description = "Image: Clone and resize to 64x64")]
    public MagickImage? ResizeImage_64()
    {
        if (!_hasTestImage || _testImage == null)
            return null;

        MagickImage clone = (MagickImage)_testImage.Clone();
  clone.Resize(64, 64);
        return clone;
    }

[Benchmark(Description = "Image: Scale and sharpen (typical workflow)")]
    public MagickImage? ScaleAndSharpen()
    {
        if (!_hasTestImage || _testImage == null)
         return null;

     MagickImage clone = (MagickImage)_testImage.Clone();
        MagickGeometry geometry = new(128, 128)
        {
     IgnoreAspectRatio = false
        };
 clone.Scale(geometry);
        clone.Sharpen();
        return clone;
    }

    #endregion

    #region Collection Add Benchmarks

    [Benchmark(Description = "Original: foreach with deconstruct")]
    public List<string> AddToList_Original()
    {
        List<string> result = [];
        foreach ((_, string path) in _imagePaths)
            result.Add(path);
        return result;
    }

    [Benchmark(Description = "Optimized: for with indexed access")]
    public List<string> AddToList_Optimized()
    {
    List<string> result = [];
   for (int i = 0; i < _imagePaths.Count; i++)
   result.Add(_imagePaths[i].Item2);
        return result;
 }

    #endregion
}

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("??????????????????????????????????????????????????????????????????");
    Console.WriteLine("?         PreviewStack Performance Benchmarks        ?");
  Console.WriteLine("??????????????????????????????????????????????????????????????????");
Console.WriteLine();
        Console.WriteLine("?? Tip: Add a 'Perf.png' file to the benchmark directory");
      Console.WriteLine("   to enable image processing benchmarks.");
     Console.WriteLine();

        var summary = BenchmarkRunner.Run<PreviewStackBenchmarks>();

  Console.WriteLine();
        Console.WriteLine("??????????????????????????????????????????????????????????????????");
        Console.WriteLine("?         Benchmark Summary          ?");
     Console.WriteLine("??????????????????????????????????????????????????????????????????");
        Console.WriteLine();
        Console.WriteLine("Check the generated report for detailed results.");
      Console.WriteLine();
     Console.WriteLine("?? Key Metrics to Look For:");
        Console.WriteLine("  ? Mean execution time (lower is better)");
        Console.WriteLine("  ?? Allocated memory (lower is better)");
        Console.WriteLine("  ??  Gen0/Gen1/Gen2 collections (lower is better)");
        Console.WriteLine("  ?? Ratio vs baseline (< 1.00 = faster)");
        Console.WriteLine();
  }
}