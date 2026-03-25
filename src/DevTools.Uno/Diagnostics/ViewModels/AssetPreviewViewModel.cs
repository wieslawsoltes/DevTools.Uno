using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using DevTools.Uno.Diagnostics.Internal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DevTools.Uno.Diagnostics.ViewModels;

internal sealed class AssetPreviewViewModel : ViewModelBase
{
    private const ulong MaxPreviewBytes = 100UL * 1024 * 1024;
    private const int MaxTextPreviewBytes = 256 * 1024;
    private const int MaxBinaryPreviewBytes = 512;

    private AssetEntryViewModel? _asset;
    private ImageSource? _previewImageSource;
    private FontFamily? _previewFontFamily;
    private string _title = "No asset selected";
    private string _assetUri = string.Empty;
    private string _metadata = string.Empty;
    private string _previewText = string.Empty;
    private string _statusText = "Select an asset to preview.";
    private string _fontPreviewText = "The quick brown fox jumps over the lazy dog. 0123456789";
    private string _exportStatus = string.Empty;
    private Visibility _imageVisibility = Visibility.Collapsed;
    private Visibility _textVisibility = Visibility.Collapsed;
    private Visibility _fontVisibility = Visibility.Collapsed;
    private Visibility _fallbackVisibility = Visibility.Visible;
    private int _version;

    public string Title
    {
        get => _title;
        private set => RaiseAndSetIfChanged(ref _title, value);
    }

    public string AssetUri
    {
        get => _assetUri;
        private set => RaiseAndSetIfChanged(ref _assetUri, value);
    }

    public string Metadata
    {
        get => _metadata;
        private set => RaiseAndSetIfChanged(ref _metadata, value);
    }

    public string PreviewText
    {
        get => _previewText;
        private set => RaiseAndSetIfChanged(ref _previewText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => RaiseAndSetIfChanged(ref _statusText, value);
    }

    public string FontPreviewText
    {
        get => _fontPreviewText;
        private set => RaiseAndSetIfChanged(ref _fontPreviewText, value);
    }

    public string ExportStatus
    {
        get => _exportStatus;
        private set => RaiseAndSetIfChanged(ref _exportStatus, value);
    }

    public ImageSource? PreviewImageSource
    {
        get => _previewImageSource;
        private set => RaiseAndSetIfChanged(ref _previewImageSource, value);
    }

    public FontFamily? PreviewFontFamily
    {
        get => _previewFontFamily;
        private set => RaiseAndSetIfChanged(ref _previewFontFamily, value);
    }

    public Visibility ImageVisibility
    {
        get => _imageVisibility;
        private set => RaiseAndSetIfChanged(ref _imageVisibility, value);
    }

    public Visibility TextVisibility
    {
        get => _textVisibility;
        private set => RaiseAndSetIfChanged(ref _textVisibility, value);
    }

    public Visibility FontVisibility
    {
        get => _fontVisibility;
        private set => RaiseAndSetIfChanged(ref _fontVisibility, value);
    }

    public Visibility FallbackVisibility
    {
        get => _fallbackVisibility;
        private set => RaiseAndSetIfChanged(ref _fallbackVisibility, value);
    }

    public async Task LoadAsync(AssetEntryViewModel? asset)
    {
        _asset = asset;
        var version = ++_version;

        ResetPreview(asset);
        if (asset is null)
        {
            return;
        }

        if (asset.SizeBytes > MaxPreviewBytes)
        {
            ShowFallback($"Preview disabled for assets larger than {AssetEntryViewModelFormatter.FormatBytes(MaxPreviewBytes)}.");
            return;
        }

        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(asset.AssetUri);
            if (version != _version)
            {
                return;
            }

            switch (asset.Type)
            {
                case "Image":
                case "Vector Image":
                    await LoadImagePreviewAsync(asset, file, version);
                    break;
                case "Font":
                    LoadFontPreview(asset);
                    break;
                case "Text":
                    await LoadTextPreviewAsync(asset, file, version);
                    break;
                default:
                    await LoadBinaryPreviewAsync(asset, file, version);
                    break;
            }
        }
        catch (Exception exception)
        {
            if (version != _version)
            {
                return;
            }

            ShowFallback(exception.Message);
        }
    }

    public async Task ExportAsync()
    {
        if (_asset is null)
        {
            ExportStatus = "Select an asset before exporting.";
            return;
        }

        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(_asset.AssetUri);
            var folder = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync("DevToolsAssetsExport", CreationCollisionOption.OpenIfExists);
            var exported = await file.CopyAsync(folder, file.Name, NameCollisionOption.GenerateUniqueName);
            ExportStatus = $"Exported to {exported.Path}";
        }
        catch (Exception exception)
        {
            ExportStatus = exception.Message;
        }
    }

    private void ResetPreview(AssetEntryViewModel? asset)
    {
        PreviewImageSource = null;
        PreviewFontFamily = null;
        PreviewText = string.Empty;
        ExportStatus = string.Empty;
        ImageVisibility = Visibility.Collapsed;
        TextVisibility = Visibility.Collapsed;
        FontVisibility = Visibility.Collapsed;
        FallbackVisibility = Visibility.Visible;
        Title = asset?.Name ?? "No asset selected";
        AssetUri = asset?.AssetUri.ToString() ?? string.Empty;
        Metadata = asset is null
            ? string.Empty
            : $"{asset.Type} · {asset.SizeText}\n{asset.RelativePath}";
        StatusText = asset is null ? "Select an asset to preview." : "Loading preview…";
    }

    private async Task LoadImagePreviewAsync(AssetEntryViewModel asset, StorageFile file, int version)
    {
        if (asset.Type == "Vector Image")
        {
            PreviewImageSource = new SvgImageSource(asset.AssetUri);
            ImageVisibility = Visibility.Visible;
            FallbackVisibility = Visibility.Collapsed;
            StatusText = "SVG preview";
            Metadata = $"{Metadata}\nSource: {asset.AssetUri}";
            return;
        }

        if (version != _version)
        {
            return;
        }

        var bitmap = new BitmapImage
        {
            UriSource = asset.AssetUri,
        };
        PreviewImageSource = bitmap;
        ImageVisibility = Visibility.Visible;
        FallbackVisibility = Visibility.Collapsed;
        StatusText = "Image preview";
        Metadata = $"{Metadata}\nSource: {asset.AssetUri}";

        var dimensions = await TryGetBitmapDimensionsAsync(bitmap);
        if (version != _version || dimensions.Width <= 0 || dimensions.Height <= 0)
        {
            return;
        }

        Metadata = $"{Metadata}\nDimensions: {dimensions.Width} × {dimensions.Height}";
    }

    private void LoadFontPreview(AssetEntryViewModel asset)
    {
        PreviewFontFamily = new FontFamily(asset.AssetUri.ToString());
        FontVisibility = Visibility.Visible;
        FallbackVisibility = Visibility.Collapsed;
        StatusText = "Font preview";
        Metadata = $"{Metadata}\nFont source: {asset.AssetUri}";
    }

    private async Task LoadTextPreviewAsync(AssetEntryViewModel asset, StorageFile file, int version)
    {
        using var stream = await file.OpenReadAsync();
        if (version != _version)
        {
            return;
        }

        var text = await ReadTextPreviewAsync(stream);
        if (version != _version)
        {
            return;
        }

        PreviewText = text;
        TextVisibility = Visibility.Visible;
        FallbackVisibility = Visibility.Collapsed;
        StatusText = "Text preview";
        Metadata = $"{Metadata}\nCharacters: {text.Length}";
    }

    private async Task LoadBinaryPreviewAsync(AssetEntryViewModel asset, StorageFile file, int version)
    {
        using var stream = await file.OpenReadAsync();
        if (version != _version)
        {
            return;
        }

        var bytes = await ReadBytesAsync(stream, MaxBinaryPreviewBytes);
        if (version != _version)
        {
            return;
        }

        PreviewText = FormatBinaryPreview(bytes);
        TextVisibility = Visibility.Visible;
        FallbackVisibility = Visibility.Collapsed;
        StatusText = asset.Type == "Media" ? "Media file metadata only" : "Binary preview";
        Metadata = $"{Metadata}\nPreview bytes: {bytes.Length}";
    }

    private void ShowFallback(string message)
    {
        StatusText = message;
        FallbackVisibility = Visibility.Visible;
        ImageVisibility = Visibility.Collapsed;
        TextVisibility = Visibility.Collapsed;
        FontVisibility = Visibility.Collapsed;
    }

    private static async Task<(int Width, int Height)> TryGetBitmapDimensionsAsync(BitmapImage bitmap)
    {
        if (bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
        {
            return (bitmap.PixelWidth, bitmap.PixelHeight);
        }

        var completion = new TaskCompletionSource<(int Width, int Height)>(TaskCreationOptions.RunContinuationsAsynchronously);

        void DetachHandlers()
        {
            bitmap.ImageOpened -= OnImageOpened;
            bitmap.ImageFailed -= OnImageFailed;
        }

        void OnImageOpened(object sender, RoutedEventArgs e)
        {
            DetachHandlers();
            completion.TrySetResult((bitmap.PixelWidth, bitmap.PixelHeight));
        }

        void OnImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            DetachHandlers();
            completion.TrySetResult(default);
        }

        bitmap.ImageOpened += OnImageOpened;
        bitmap.ImageFailed += OnImageFailed;

        var completedTask = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        if (completedTask == completion.Task)
        {
            return await completion.Task;
        }

        DetachHandlers();
        return default;
    }

    private static async Task<string> ReadTextPreviewAsync(IRandomAccessStream stream)
    {
        using var managed = stream.AsStreamForRead();
        using var memory = new MemoryStream();
        var buffer = new byte[Math.Min(MaxTextPreviewBytes, (int)Math.Max(4096, stream.Size))];
        var remaining = MaxTextPreviewBytes;

        while (remaining > 0)
        {
            var read = await managed.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)));
            if (read == 0)
            {
                break;
            }

            await memory.WriteAsync(buffer.AsMemory(0, read));
            remaining -= read;
        }

        var bytes = memory.ToArray();
        var encoding = DetectEncoding(bytes);
        var text = encoding.GetString(bytes);
        if (stream.Size > (ulong)bytes.Length)
        {
            text += "\n\n[preview truncated]";
        }

        return text;
    }

    private static async Task<byte[]> ReadBytesAsync(IRandomAccessStream stream, int maxBytes)
    {
        using var managed = stream.AsStreamForRead();
        var buffer = new byte[Math.Min(maxBytes, (int)Math.Min(int.MaxValue, stream.Size))];
        var read = await managed.ReadAsync(buffer.AsMemory(0, buffer.Length));
        return buffer.Take(read).ToArray();
    }

    private static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode;
        }

        return Encoding.UTF8;
    }

    private static string FormatBinaryPreview(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return "(empty)";
        }

        var builder = new StringBuilder();
        for (var index = 0; index < bytes.Length; index += 16)
        {
            var slice = bytes.Skip(index).Take(16).ToArray();
            builder.Append($"{index:X4}: ");
            builder.Append(string.Join(" ", slice.Select(x => x.ToString("X2"))));
            if (slice.Length < 16)
            {
                builder.Append(' ', (16 - slice.Length) * 3);
            }

            builder.Append("  ");
            builder.Append(new string(slice.Select(x => x is >= 32 and < 127 ? (char)x : '.').ToArray()));
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}

internal static class AssetEntryViewModelFormatter
{
    public static string FormatBytes(ulong value)
    {
        const double kilo = 1024d;
        const double mega = kilo * 1024d;
        return value >= (ulong)mega ? $"{value / mega:0.##} MB" : $"{value / kilo:0.##} KB";
    }
}
