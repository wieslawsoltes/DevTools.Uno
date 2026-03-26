using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;

namespace DevTools.Uno.Diagnostics.Screenshots;

public sealed class FolderScreenshotHandler(StorageFolder folder) : IScreenshotHandler
{
    public StorageFolder Folder { get; } = folder;

    public async Task<ScreenshotCaptureResult> CaptureAsync(
        FrameworkElement element,
        string suggestedFileName,
        Window? ownerWindow = null,
        CancellationToken cancellationToken = default)
    {
        _ = ownerWindow;
        var file = await Folder.CreateFileAsync($"{suggestedFileName}.png", CreationCollisionOption.GenerateUniqueName);
        cancellationToken.ThrowIfCancellationRequested();
        await ScreenshotEncoder.SaveAsync(element, file, cancellationToken);
        return ScreenshotCaptureResult.Saved(file);
    }
}
