using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using MkvRenameWizard.Models.Mkv;

namespace MkvRenameWizard.Services;

public interface IMkvFinderService
{
    Task<Dictionary<string, FrozenSet<MkvFile>>> OpenMkvFiles(TopLevel topLevel);
}