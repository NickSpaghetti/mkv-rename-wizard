using Avalonia;
using Avalonia.Controls;
using MkvRenameWizard.Models.Rail;

namespace MkvRenameWizard.Views;

public static class RailGrip
{
    public static readonly AttachedProperty<RailReorderSide> SideProperty =
        AvaloniaProperty.RegisterAttached<Border, RailReorderSide>(nameof(SideProperty), typeof(RailGrip));
    
    public static void SetSide(Border border, RailReorderSide side) => border.SetValue(SideProperty, side);
    public static RailReorderSide GetSide(Border border) => border.GetValue(SideProperty);
}