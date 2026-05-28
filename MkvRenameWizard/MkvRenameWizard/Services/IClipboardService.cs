using System.Threading.Tasks;

namespace MkvRenameWizard.Services;

public interface IClipboardService
{
    public Task SetTextAsync(string text);
}