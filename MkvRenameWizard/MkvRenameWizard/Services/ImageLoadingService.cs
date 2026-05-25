using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using FileTypeChecker;
using FileTypeChecker.Abstracts;
using FileTypeChecker.Extensions;
using Microsoft.Extensions.Logging;

namespace MkvRenameWizard.Services;

public class ImageLoadingService : IImageLoadingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageLoadingService> _logger;
    public ImageLoadingService(IHttpClientFactory httpClient,  ILogger<ImageLoadingService> logger)
    {
        _httpClientFactory = httpClient;
        _logger = logger;
    }
    
    private static readonly HashSet<string> AllowedImageHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "tvmaze.com",
        "www.tvmaze.com",
        "static.tvmaze.com",
        "www.static.tvmaze.com",
    };
    
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

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            
            if (await FileTypeValidator.IsTypeRecognizableAsync(memoryStream, cancellationToken))
            {
                memoryStream.Position = 0;
                if (!await memoryStream.IsImageAsync(cancellationToken))
                {
                    memoryStream.Position = 0;
                    var actualType = await FileTypeValidator.GetFileTypeAsync(memoryStream, cancellationToken);
                    _logger.LogWarning("Security Alert: File disguised as image! Actual type: {Name} ({Extension})", 
                        actualType.Name, actualType.Extension);
                    return null;
                }
            }
            else
            {
                _logger.LogWarning("Aborted image processing: File content structure could not be verified as a valid file type.");
                return null;
            }
            
            memoryStream.Position = 0; 

            return new Bitmap(memoryStream);
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

    private bool IsTrustedImageDomain(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }
        
        return AllowedImageHosts.Contains(uri.Host);
    }
}