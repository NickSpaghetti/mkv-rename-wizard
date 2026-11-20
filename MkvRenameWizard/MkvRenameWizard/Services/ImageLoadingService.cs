using System;
using System.Collections.Frozen;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace MkvRenameWizard.Services;

public class ImageLoadingService : IImageLoadingService
{
    private const int DefaultDecodeWidthPx = 240;
    private const long MaxImageBytes = 10 * 1024 * 1024;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageLoadingService> _logger;

    public ImageLoadingService(IHttpClientFactory httpClient, ILogger<ImageLoadingService> logger)
    {
        _httpClientFactory = httpClient;
        _logger = logger;
    }

    private static readonly FrozenSet<string> AllowedImageHosts = new[]
    {
        "tvmaze.com",
        "www.tvmaze.com",
        "static.tvmaze.com",
        "www.static.tvmaze.com",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public async Task<Bitmap?> LoadBitMapAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri))
        {
            _logger.LogError("Invalid image url: {imageUrl}", imageUrl);
            return null;
        }

        if (!IsTrustedImageDomain(imageUri))
        {
            var trustedDomainsLabel = string.Join(", ", AllowedImageHosts);
            _logger.LogWarning(
                "Blocked unsafe or untrusted image download attempt to host: '{Host}' (URL: {Url}). \n" +
                "Trusted domains are restricted to: [{TrustedDomains}]",
                imageUri.Host, imageUrl, trustedDomainsLabel);
            return null;
        }

        try
        {
            _logger.LogInformation("Downloading show artwork from trusted host: {Host}", imageUri.Host);
            var client = _httpClientFactory.CreateClient();

            using var response = await client.GetAsync(imageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType == null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Aborted image processing: Server returned non-image Content-Type: {ContentType}", contentType);
                return null;
            }

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength > MaxImageBytes)
            {
                _logger.LogWarning("Aborted image processing: Image size {Size} exceeds limit", contentLength);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var initialCapacity = contentLength is > 0 and <= MaxImageBytes ? (int)contentLength.Value : 32 * 1024;
            using var memoryStream = new MemoryStream(initialCapacity);
            await stream.CopyToAsync(memoryStream, cancellationToken);

            if (!memoryStream.TryGetBuffer(out var buffer) || !HasValidImageHeader(buffer.AsSpan()))
            {
                _logger.LogWarning("Aborted image processing: File header could not be verified as a valid image.");
                return null;
            }

            memoryStream.Position = 0;
            return Bitmap.DecodeToWidth(memoryStream, DefaultDecodeWidthPx);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Image download operation was cancelled by the UI thread for URL: {Url}", imageUrl);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred while downloading artwork from {Url}", imageUrl);
            return null;
        }
    }

    private static bool HasValidImageHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < 4) return false;

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return true;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return true;

        // WebP: RIFF....WEBP
        if (header.Length >= 12 &&
            header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
            header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
            return true;

        // GIF: GIF87a / GIF89a
        if (header.Length >= 6 &&
            header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'8' &&
            (header[4] == (byte)'7' || header[4] == (byte)'9') && header[5] == (byte)'a')
            return true;

        // BMP: BM
        if (header[0] == (byte)'B' && header[1] == (byte)'M')
            return true;

        return false;
    }

    private static bool IsTrustedImageDomain(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return AllowedImageHosts.Contains(uri.Host);
    }
}
