using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace MkvRenameWizard.Services;

public interface IImageLoadingService
{
    Task<Bitmap?> LoadBitMapAsync(string imageUrl, CancellationToken cancellationToken);
}