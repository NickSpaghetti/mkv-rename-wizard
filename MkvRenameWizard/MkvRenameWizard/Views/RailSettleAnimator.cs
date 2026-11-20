using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using MkvRenameWizard.Models.Rail;

namespace MkvRenameWizard.Views;

internal static class RailSettleAnimator
{
    private static readonly BackEaseOut LinkedEasing = new();
    private static readonly CubicEaseOut IndependentEasing = new();
    private static readonly Color AccentColor = Color.FromRgb(0x48, 0xC8, 0xF6);

    public static void RunIfNeeded(Border row, RailSettleType railSettleType, int direction)
    {
        if (railSettleType == RailSettleType.None || direction == 0)
        {
            return;
        }

        _ = RunAsync(row, railSettleType, direction);
    }

    private static async Task RunAsync(Border row, RailSettleType railSettleType, int direction)
    {
        await Task.Delay(1);

        var isLinked = railSettleType == RailSettleType.Linked;
        var offset = isLinked ? 10.0 : 8.0;
        var startY = -offset * Math.Sign(direction);

        try
        {
            await AnimateSettleAsync(row, startY, isLinked);
            if (isLinked)
            {
                await AnimateLinkConnectorAsync(row);
            }
        }
        catch
        {
            // Animation can be interrupted by refresh so we will ignore it.
        }
    }

    private static async Task AnimateSettleAsync(Border row, double startY, bool isLinked)
    {
        var settleRail = row.FindControl<Border>(Constants.BorderNames.SettleRail);
        var durationMs = isLinked ? 520.0 : 480.0;
        Easing easing = isLinked ? LinkedEasing : IndependentEasing;

        var translate = new TranslateTransform(0, startY);
        var rowBrush = new SolidColorBrush(AccentColor, 0.12);
        var railBrush = settleRail is not null ? new SolidColorBrush(AccentColor, 1.0) : null;

        row.RenderTransform = translate;
        row.Background = rowBrush;
        if (settleRail != null)
        {
            settleRail.Background = railBrush;
        }

        const int steps = 24;
        var stepDelay = (int)(durationMs / steps);

        for (var i = 0; i <= steps; i++)
        {
            var t = i / (double)steps;
            var eased = easing.Ease(t);
            translate.Y = startY * (1 - eased);
            rowBrush.Opacity = Math.Max(0.0, (1 - eased) * 0.12);

            if (railBrush != null)
            {
                railBrush.Opacity = Math.Max(0.0, 1 - eased);
            }

            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
            await Task.Delay(stepDelay);
        }

        row.RenderTransform = null;
        row.Background = Brushes.Transparent;
        if (settleRail != null)
        {
            settleRail.Background = Brushes.Transparent;
        }
    }

    private static async Task AnimateLinkConnectorAsync(Border row)
    {
        var dot = row.FindControl<Ellipse>(Constants.EllipseNames.LinkDot);
        var halo = row.FindControl<Ellipse>(Constants.EllipseNames.LinkHalo);
        var lineLeft = row.FindControl<Border>(Constants.BorderNames.LinkLineLeft);
        var lineRight = row.FindControl<Border>(Constants.BorderNames.LinkLineRight);

        if (dot == null)
        {
            return;
        }

        var center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        dot.RenderTransformOrigin = center;
        if (halo != null)
        {
            halo.RenderTransformOrigin = center;
        }

        var dotScale = new ScaleTransform(1, 1);
        var haloScale = halo is not null ? new ScaleTransform(1, 1) : null;
        dot.RenderTransform = dotScale;
        if (halo != null)
        {
            halo.RenderTransform = haloScale;
        }

        const int steps = 20;
        const int stepMs = 20;

        for (var i = 0; i <= steps; i++)
        {
            var t = i / (double)steps;
            var scale = t < 0.45
                ? 1 + t / 0.45 * 0.6
                : 1.6 - (t - 0.45) / 0.55 * 0.6;

            dotScale.ScaleX = scale;
            dotScale.ScaleY = scale;

            if (halo != null && haloScale != null)
            {
                var hs = 1 + t * 1.2;
                haloScale.ScaleX = hs;
                haloScale.ScaleY = hs;
                halo.Opacity = 0.45 * (1 - t);
            }

            if (lineLeft != null && lineRight != null)
            {
                var glow = t < 0.5 ? t * 2 : (1 - t) * 2;
                var opacity = 0.5 + glow * 0.5;
                lineLeft.Opacity = opacity;
                lineRight.Opacity = opacity;
            }

            await Task.Delay(stepMs);
        }

        dot.RenderTransform = null;
        if (halo != null)
        {
            halo.RenderTransform = null;
            halo.Opacity = 0;
        }

        if (lineLeft != null && lineRight != null)
        {
            lineLeft.Opacity = 0.75;
            lineRight.Opacity = 0.75;
        }
    }
}
