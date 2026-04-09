using Windows.Storage;

namespace DevToolsUno.Diagnostics;

public readonly record struct ScreenshotCaptureResult(StorageFile? File, bool IsCanceled)
{
    public bool Succeeded => File is not null && !IsCanceled;

    public static ScreenshotCaptureResult Saved(StorageFile file)
        => new(file, false);

    public static ScreenshotCaptureResult Canceled()
        => new(null, true);
}
