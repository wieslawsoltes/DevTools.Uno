using System.Collections.ObjectModel;
using DevToolsUno.Diagnostics.ViewModels;
using Windows.ApplicationModel;
using Windows.Storage;

namespace DevToolsUno.Diagnostics.Internal;

internal static class AssetInspector
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp", ".svg",
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".xml", ".xaml", ".axaml", ".yml", ".yaml", ".csv", ".tsv",
        ".html", ".htm", ".css", ".js", ".ts", ".resw", ".resjson", ".props", ".targets", ".csproj", ".sln",
    };

    private static readonly HashSet<string> FontExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf", ".otf", ".woff", ".woff2",
    };

    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".ogg", ".m4a", ".mp4", ".webm", ".mov",
    };

    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".exe", ".pdb", ".dylib", ".so", ".a", ".lib", ".deps.json", ".runtimeconfig.json", ".db", ".cache",
    };

    public static async Task<AssetFolderNode> BuildFolderTreeAsync()
    {
        var rootFolder = Package.Current.InstalledLocation;
        var rootName = string.IsNullOrWhiteSpace(Package.Current.DisplayName)
            ? "Application Package"
            : Package.Current.DisplayName;

        var root = new AssetFolderNode
        {
            Name = rootName,
            RelativePath = string.Empty,
        };

        await PopulateFolderAsync(rootFolder, root).ConfigureAwait(false);
        root.UpdateCountsRecursive();
        return root;
    }

    public static IReadOnlyList<AssetEntryViewModel> GetAssets(
        AssetFolderNode folder,
        FilterViewModel filter,
        bool recursive,
        string sortBy,
        bool sortDescending)
    {
        var items = new List<AssetEntryViewModel>();
        CollectAssets(folder, recursive, items);

        IEnumerable<AssetEntryViewModel> query = items.Where(x =>
            filter.Filter(x.Name) ||
            filter.Filter(x.RelativePath) ||
            filter.Filter(x.Type));

        query = sortBy switch
        {
            "Type" => sortDescending
                ? query.OrderByDescending(x => x.Type, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(x => x.Type, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            "Size" => sortDescending
                ? query.OrderByDescending(x => x.SizeBytes).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(x => x.SizeBytes).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            _ => sortDescending
                ? query.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                : query.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase),
        };

        return query.ToArray();
    }

    private static async Task PopulateFolderAsync(StorageFolder folder, AssetFolderNode node)
    {
        IReadOnlyList<StorageFile> files;
        IReadOnlyList<StorageFolder> folders;

        try
        {
            files = await folder.GetFilesAsync();
            folders = await folder.GetFoldersAsync();
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            if (!ShouldInclude(file.Name, node.RelativePath))
            {
                continue;
            }

            node.DirectAssets.Add(await CreateEntryAsync(file, node.RelativePath).ConfigureAwait(false));
        }

        foreach (var childFolder in folders.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var childRelativePath = string.IsNullOrEmpty(node.RelativePath)
                ? childFolder.Name
                : $"{node.RelativePath}/{childFolder.Name}";

            var child = new AssetFolderNode
            {
                Name = childFolder.Name,
                RelativePath = childRelativePath,
            };

            await PopulateFolderAsync(childFolder, child).ConfigureAwait(false);
            child.UpdateCountsRecursive();
            if (child.TotalAssetCount > 0)
            {
                node.AddChild(child);
            }
        }
    }

    private static async Task<AssetEntryViewModel> CreateEntryAsync(StorageFile file, string folderPath)
    {
        ulong size = 0;
        try
        {
            size = (await file.GetBasicPropertiesAsync()).Size;
        }
        catch
        {
        }

        var relativePath = string.IsNullOrEmpty(folderPath)
            ? file.Name
            : $"{folderPath}/{file.Name}";

        var extension = Path.GetExtension(file.Name);
        return new AssetEntryViewModel
        {
            Name = file.Name,
            RelativePath = relativePath,
            FolderPath = folderPath,
            Type = GetAssetType(extension),
            Extension = extension,
            AssetUri = new Uri($"ms-appx:///{EncodeRelativePath(relativePath)}"),
            SizeBytes = size,
        };
    }

    private static void CollectAssets(AssetFolderNode folder, bool recursive, ICollection<AssetEntryViewModel> target)
    {
        foreach (var asset in folder.DirectAssets)
        {
            target.Add(asset);
        }

        if (!recursive)
        {
            return;
        }

        foreach (var child in folder.Children)
        {
            CollectAssets(child, recursive: true, target);
        }
    }

    private static string GetAssetType(string extension)
    {
        if (ImageExtensions.Contains(extension))
        {
            return string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase) ? "Vector Image" : "Image";
        }

        if (FontExtensions.Contains(extension))
        {
            return "Font";
        }

        if (TextExtensions.Contains(extension))
        {
            return "Text";
        }

        if (MediaExtensions.Contains(extension))
        {
            return "Media";
        }

        return "Binary";
    }

    private static bool ShouldInclude(string fileName, string relativeFolder)
    {
        var extension = Path.GetExtension(fileName);
        if (IgnoredExtensions.Contains(fileName) || IgnoredExtensions.Contains(extension))
        {
            return false;
        }

        return ImageExtensions.Contains(extension) ||
               FontExtensions.Contains(extension) ||
               TextExtensions.Contains(extension) ||
               MediaExtensions.Contains(extension) ||
               relativeFolder.Contains("Assets", StringComparison.OrdinalIgnoreCase) ||
               relativeFolder.Contains("Themes", StringComparison.OrdinalIgnoreCase);
    }

    private static string EncodeRelativePath(string relativePath)
    {
        return string.Join(
            "/",
            relativePath
                .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
    }
}
