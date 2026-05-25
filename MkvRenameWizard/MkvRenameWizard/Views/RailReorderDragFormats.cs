using System;
using Avalonia.Input;
using MkvRenameWizard.Models.Rail;

namespace MkvRenameWizard.Views;

internal static class RailReorderDragFormats
{
    private const string Prefix = Constants.TransformsMetadata.RailReorderDragPrefix;

    public static bool TryGet(IDataTransfer transfer, out RailReorderDragData? outdragData)
    {
        outdragData = null;
        var text = transfer.TryGetText();
        if(string.IsNullOrEmpty(text) || !text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var raw = text[Prefix.Length..];
        var parts = raw.Split('|');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!Enum.TryParse<RailReorderSide>(parts[0], out var railReorderSide))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var sourceIndex))
        {
            return false;
        }
        if (sourceIndex < 0)
        {
            return false;
        }

        if (!bool.TryParse(parts[2], out var isMoveLinked))
        {
            return false;
        }

        outdragData = new RailReorderDragData(railReorderSide, sourceIndex, isMoveLinked);
        return true;
    }

    public static DataTransfer CreateTransfer(RailReorderDragData reorderDragData)
    {
        var raw = $"{reorderDragData.ReorderSide}|{reorderDragData.SourceIndex}|{reorderDragData.IsMoveLinked}";
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.CreateText($"{Prefix}{raw}"));
        return transfer;
    }
}