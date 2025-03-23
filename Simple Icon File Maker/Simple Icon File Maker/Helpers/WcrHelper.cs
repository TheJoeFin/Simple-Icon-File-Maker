using Microsoft.Graphics.Imaging;
using Microsoft.Windows.Management.Deployment;
using Windows.Graphics;
using Windows.Graphics.Imaging;

namespace Simple_Icon_File_Maker.Helpers;

public class WcrHelper
{
    public async Task<SoftwareBitmap> ExtractImage(SoftwareBitmap softwareBitmap, PointInt32[] selectionPoints)
    {
        if (!ImageObjectExtractor.IsAvailable())
        {
            var result = await ImageObjectExtractor.MakeAvailableAsync();
            if (result.Status != PackageDeploymentStatus.CompletedSuccess)
            {
                throw result.ExtendedError;
            }
        }

        ImageObjectExtractor imageObjectExtractor = await ImageObjectExtractor.CreateWithSoftwareBitmapAsync(softwareBitmap);

        ImageObjectExtractorHint hint = new(
                        includeRects: null,
                        includePoints: selectionPoints,
                        excludePoints: null
                    );

        SoftwareBitmap finalImage = imageObjectExtractor.GetSoftwareBitmapObjectMask(hint);

        return finalImage;
    }
}
