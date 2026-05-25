namespace MkvRenameWizard;

public static class Constants
{
    public static class Metadata
    {
        public const string AppName = "MkvRenameWizard";
    }

    public static class TransformsMetadata
    {
        public const string RailReorderDragPrefix = "mkv-reorder";
    }

    public static class AxamlClasses
    {
        public const string DraggingClass = "dragging";
        public const string BorderMatchRow = "matchRow";
        public const string BorderRailPanelCell = "railPanelCell";
    }

    public static class BorderNames
    {
        public const string SettleRail = nameof(SettleRail);
        public const string LinkLineLeft = nameof(LinkLineLeft);
        public const string LinkLineRight = nameof(LinkLineRight);
    }

    public static class GridNames
    {
        public const string LinkConnector = nameof(LinkConnector);
    }
    
    public static class EllipseNames
    {
        public const string LinkDot = nameof(LinkDot);
        public const string LinkHalo = nameof(LinkHalo);
    }

}