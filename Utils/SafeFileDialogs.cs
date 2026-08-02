using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace InvoicePress.Utils;

public class SafeFileDialogs
{
    public static async Task<string?> PickFolderAsync(Window window, string title)
    {
        var topLevel = TopLevel.GetTopLevel(window);
        if (topLevel == null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}