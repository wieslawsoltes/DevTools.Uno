using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DevTools.Uno.Diagnostics.Screenshots;

internal static class ScreenshotEncoder
{
    public static async Task SaveAsync(FrameworkElement element, StorageFile file, CancellationToken cancellationToken = default)
    {
        var renderer = new RenderTargetBitmap();
        await renderer.RenderAsync(element);
        cancellationToken.ThrowIfCancellationRequested();

        if (renderer.PixelWidth <= 0 || renderer.PixelHeight <= 0)
        {
            throw new InvalidOperationException("The selected element could not be rendered to an image.");
        }

        var pixels = await renderer.GetPixelsAsync();
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            pixels,
            BitmapPixelFormat.Bgra8,
            renderer.PixelWidth,
            renderer.PixelHeight);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
    }
}
