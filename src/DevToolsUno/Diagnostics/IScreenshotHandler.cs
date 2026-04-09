using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DevToolsUno.Diagnostics;

public interface IScreenshotHandler
{
    Task<ScreenshotCaptureResult> CaptureAsync(
        FrameworkElement element,
        string suggestedFileName,
        Window? ownerWindow = null,
        CancellationToken cancellationToken = default);
}
