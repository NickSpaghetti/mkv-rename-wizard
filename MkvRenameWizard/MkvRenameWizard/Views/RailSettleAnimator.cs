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
        var duration = isLinked ? TimeSpan.FromMilliseconds(520) : TimeSpan.FromSeconds(480);
        Easing easing = isLinked ? new BackEaseOut() : new CubicEaseOut();
        duration = TimeSpan.FromMilliseconds(isLinked ? 520 : 480);
        easing = new BackEaseOut();
        
        row.RenderTransform = new TranslateTransform(0,startY);
        settleRail?.Background = Brushes.Transparent;
        const int steps = 24;
        var stepDelay = duration.TotalMilliseconds / steps;

        for (var i = 0; i <= steps; i++)
        {
            var t =  i / (double)steps;
            var eased = easing.Ease(t);
            var y = startY * (1 - eased);
            row.RenderTransform = new TranslateTransform(0, y);
            
            var tintAlpha = Math.Max(0.0,(1 - eased) * 0.12);
            //Same colors as AccentBrush from App.axmal.
            row.Background = new SolidColorBrush(Color.FromArgb((byte)(tintAlpha * 255), 0x48, 0xC8, 0xF6));

            if (settleRail != null)
            {
                var railAlpha = Math.Max(0.0,1 - eased);
                //Same colors as AccentBrush from App.axmal.
                settleRail.Background = new SolidColorBrush(Color.FromArgb((byte)(railAlpha * 255), 0x48, 0xC8, 0xF6));
            }
            
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay((int)stepDelay);
        }
        row.RenderTransform = null;
        row.Background = Brushes.Transparent;
        settleRail?.Background = Brushes.Transparent;
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
        halo?.RenderTransformOrigin = center;

        const int steps = 20;
        const int stepMs = 20;

        for (var i = 0; i <= steps; i++)
        {
            var t = i / (double)steps;
            double scale;
            if (t < 0.45)
            {
                scale = 1 + t / 0.45 * 0.6;
            }
            else
            {
                scale = 1.6 - (t - 0.45) / 0.55 * 0.6;
            }
            
            dot.RenderTransform = new ScaleTransform(scale, scale);

            if (halo != null)
            {
                var haloScale = 1 + t * 1.2;
                halo.RenderTransform = new ScaleTransform(haloScale, haloScale);
                halo.Opacity = 0.45 * (1 - t);
            }

            if (lineLeft != null && lineRight != null)
            {
                var glow = t < 0.5 ? t * 2 : (1 - t) * 2;
                lineLeft.Opacity = 0.5 + glow * 0.5;
                lineRight.Opacity = 0.5 + glow * 0.5;
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