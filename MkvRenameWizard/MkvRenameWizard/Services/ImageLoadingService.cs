using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace MkvRenameWizard.Services;

public class ImageLoadingService : IImageLoadingService
{
    private readonly HttpClient _httpClient;
    public ImageLoadingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    private readonly IImageLoadingService _imageLoadingService;
    public async Task<Bitmap?> LoadBitMapAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        try
        {
            await using var imageStream = await _httpClient.GetStreamAsync(imageUrl, cancellationToken);
            await using var memoryStream = new MemoryStream();
            memoryStream.Position = 0;
            return new Bitmap(memoryStream);
        }
        catch when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}