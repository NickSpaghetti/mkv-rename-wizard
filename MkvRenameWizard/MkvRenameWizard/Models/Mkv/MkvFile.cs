using Avalonia.Controls.Shapes;

namespace MkvRenameWizard.Models.Mkv;

public class MkvFile(string root, string fullPath, bool isIncluded)
{
    public string Root { get; set; } = root;
    public string FullPath { get; set; } = fullPath;
    public bool IsIncluded { get; set; } = isIncluded;
}