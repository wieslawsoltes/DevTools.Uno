using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace DevTools.Uno.Diagnostics.Screenshots;

public sealed class FileSavePickerScreenshotHandler : IScreenshotHandler
{
    public async Task<ScreenshotCaptureResult> CaptureAsync(
        FrameworkElement element,
        string suggestedFileName,
        Window? ownerWindow = null,
        CancellationToken cancellationToken = default)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            SuggestedFileName = suggestedFileName,
        };
        picker.FileTypeChoices.Add("PNG image", [".png"]);

        TryInitializeWithOwnerWindow(picker, ownerWindow);
        var file = await picker.PickSaveFileAsync();
        cancellationToken.ThrowIfCancellationRequested();
        if (file is null)
        {
            return ScreenshotCaptureResult.Canceled();
        }

        await ScreenshotEncoder.SaveAsync(element, file, cancellationToken);
        return ScreenshotCaptureResult.Saved(file);
    }

    private static void TryInitializeWithOwnerWindow(FileSavePicker picker, Window? ownerWindow)
    {
        if (ownerWindow is null || !OperatingSystem.IsWindows())
        {
            return;
        }

        var windowNativeType = Type.GetType("WinRT.Interop.WindowNative, WinRT.Runtime");
        var initializeType = Type.GetType("WinRT.Interop.InitializeWithWindow, WinRT.Runtime");
        var getHandleMethod = windowNativeType?.GetMethod("GetWindowHandle", BindingFlags.Public | BindingFlags.Static);
        var initializeMethod = initializeType?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
        if (getHandleMethod is null || initializeMethod is null)
        {
            return;
        }

        if (getHandleMethod.Invoke(null, [ownerWindow]) is { } handle)
        {
            initializeMethod.Invoke(null, [picker, handle]);
        }
    }
}
