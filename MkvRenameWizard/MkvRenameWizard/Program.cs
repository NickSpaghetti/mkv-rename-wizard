using System;
using Avalonia;
using Avalonia.Dialogs;
using ReactiveUI.Avalonia;

namespace MkvRenameWizard;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(static _ => { })
            .RegisterReactiveUIViewsFromEntryAssembly();

        // On unsandboxed Linux Wayland sessions, Avalonia runs under XWayland and sends an x11:<XID>
        // parent handle to xdg-desktop-portal-gtk, which cannot parent a native Wayland portal dialog
        // (leaving it unparented under bars like Waybar). Use Avalonia's in-process managed dialog
        // parented to MainWindow instead, while preserving portal dialogs for Flatpak/Snap and native
        // dialogs on Windows, macOS, and X11.
        if (OperatingSystem.IsLinux() && IsUnsandboxedWaylandSession())
        {
            builder.UseManagedSystemDialogs();
        }

        return builder;
    }

    private static bool IsUnsandboxedWaylandSession()
    {
        var isWayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
        var isSandboxed = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID"))
                          || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SNAP"));

        return isWayland && !isSandboxed;
    }
}
