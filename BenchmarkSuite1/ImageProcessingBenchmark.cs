using BenchmarkDotNet.Attributes;
using ImageMagick;
using ImageMagick.Factories;
using System.Collections.Concurrent;

namespace Simple_Icon_File_Maker.Benchmarks;

[MemoryDiagnoser]
public class ImageProcessingBenchmark
{
    private string _testImagePath = null!;
    private string _tempDirectory = null!;
    private List<int> _iconSizes = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create a temp directory for benchmark
        _tempDirectory = Path.Combine(Path.GetTempPath(), "IconBenchmark_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);

        // Create a test image (512x512 solid color for consistent testing)
        _testImagePath = Path.Combine(_tempDirectory, "test_image.png");
        using (MagickImage image = new(MagickColors.Blue, 512, 512))
        {
            image.Write(_testImagePath);
        }

        // Standard icon sizes to test
        _iconSizes = [256, 128, 64, 32, 16];
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Benchmark(Baseline = true)]
    public async Task GenerateIconsSequential()
    {
        MagickImageFactory imgFactory = new();
        MagickGeometryFactory geoFactory = new();
        List<string> outputPaths = [];

        // Load and prepare the base image
        using MagickImage mainImage = new(_testImagePath);
        string croppedImagePath = Path.Combine(_tempDirectory, "cropped.png");

        IMagickGeometry size = geoFactory.Create((uint)Math.Max(mainImage.Width, mainImage.Height));
        size.IgnoreAspectRatio = false;
        size.FillArea = true;

        MagickColor transparent = new("#00000000");
        mainImage.Extent(size, Gravity.Center, transparent);
        await mainImage.WriteAsync(croppedImagePath, MagickFormat.Png32);

        // Process each size sequentially (current implementation)
        foreach (int sideLength in _iconSizes)
        {
            using IMagickImage<ushort> image = await imgFactory.CreateAsync(croppedImagePath);
            IMagickGeometry iconSize = geoFactory.Create((uint)sideLength, (uint)sideLength);
            iconSize.IgnoreAspectRatio = false;

            await Task.Run(() =>
   {
       image.Scale(iconSize);
       image.Sharpen();
   });

            string iconPath = Path.Combine(_tempDirectory, $"icon_{sideLength}.png");
            await image.WriteAsync(iconPath, MagickFormat.Png32);
            outputPaths.Add(iconPath);
        }

        // Cleanup
        File.Delete(croppedImagePath);
        foreach (var path in outputPaths)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Benchmark]
    public async Task GenerateIconsParallel()
    {
        MagickGeometryFactory geoFactory = new();
        List<string> outputPaths = [];

        // Load and prepare the base image
        using MagickImage mainImage = new(_testImagePath);
        string croppedImagePath = Path.Combine(_tempDirectory, "cropped_parallel.png");

        IMagickGeometry size = geoFactory.Create((uint)Math.Max(mainImage.Width, mainImage.Height));
        size.IgnoreAspectRatio = false;
        size.FillArea = true;

        MagickColor transparent = new("#00000000");
        mainImage.Extent(size, Gravity.Center, transparent);

        // Keep the base image in memory instead of writing to disk
        byte[] croppedImageBytes = mainImage.ToByteArray(MagickFormat.Png32);

        // Process all sizes in parallel
        IEnumerable<Task<string>> tasks = _iconSizes.Select(async sideLength =>
        {
            // Create a new image from the byte array (thread-safe)
            using MagickImage image = new(croppedImageBytes);
            IMagickGeometry iconSize = geoFactory.Create((uint)sideLength, (uint)sideLength);
            iconSize.IgnoreAspectRatio = false;

            // Process synchronously (ImageMagick operations are CPU-bound, not I/O-bound)
            image.Scale(iconSize);
            image.Sharpen();

            string iconPath = Path.Combine(_tempDirectory, $"icon_parallel_{sideLength}.png");
            await image.WriteAsync(iconPath, MagickFormat.Png32);
            return iconPath;
        });

        var results = await Task.WhenAll(tasks);

        // Cleanup
        foreach (var path in results)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Benchmark]
    public async Task GenerateIconsParallelOptimized()
    {
        MagickGeometryFactory geoFactory = new();

        // Load and prepare the base image
        using MagickImage mainImage = new(_testImagePath);

        IMagickGeometry size = geoFactory.Create((uint)Math.Max(mainImage.Width, mainImage.Height));
        size.IgnoreAspectRatio = false;
        size.FillArea = true;

        MagickColor transparent = new("#00000000");
        mainImage.Extent(size, Gravity.Center, transparent);

        // Keep the base image in memory
        byte[] croppedImageBytes = mainImage.ToByteArray(MagickFormat.Png32);

        // Use Parallel.ForEachAsync for better thread pool utilization
        ConcurrentBag<string> results = [];

        await Parallel.ForEachAsync(_iconSizes, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        }, async (sideLength, ct) =>
      {
          // Create a new image from the byte array
          using MagickImage image = new(croppedImageBytes);
          IMagickGeometry iconSize = geoFactory.Create((uint)sideLength, (uint)sideLength);
          iconSize.IgnoreAspectRatio = false;

          // Process image (CPU-bound)
          image.Scale(iconSize);
          image.Sharpen();

          string iconPath = Path.Combine(_tempDirectory, $"icon_optimized_{sideLength}.png");
          await image.WriteAsync(iconPath, MagickFormat.Png32);
          results.Add(iconPath);
      });

        // Cleanup
        foreach (var path in results)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}