using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace MkvRenameWizard.Services;

public class AvaloniaClipBoardService : IClipboardService
{
    public Task SetTextAsync(string text)
    {
        var window = (Application.Current?.ApplicationLifetime) as IClassicDesktopStyleApplicationLifetime;
        var mainWindow = window?.MainWindow;
        var clipboard = mainWindow?.Clipboard;
        return clipboard?.SetValueAsync(DataFormat.Text, text) ?? Task.CompletedTask;
    }
}