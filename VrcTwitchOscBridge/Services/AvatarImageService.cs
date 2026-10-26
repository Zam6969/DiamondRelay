using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VrcTwitchOscBridge.Services;

/// <summary>
/// Resolves avatar images from custom local icons, VRChat API thumbnails, or a built-in placeholder.
/// Images are cached locally to avoid repeated downloads.
/// </summary>
public sealed class AvatarImageService
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();
    private const int MaxThumbnailBytes = 8 * 1024 * 1024;
    private readonly HttpClient httpClient;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(CreateThumbnailHttpClientHandler()) { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DiamondRelay/desktop");
        return client;
    }

    internal static HttpClientHandler CreateThumbnailHttpClientHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false
    };

    private readonly string iconFolder;
    private readonly string cacheFolder;
    private readonly ConcurrentDictionary<string, ImageSource> imageCache = new(StringComparer.Ordinal);

    private string? vrChatAuthCookie;

    /// <summary>
    /// Sets the VRChat auth cookie used for authenticated thumbnail downloads.
    /// Call when VRChat connects (pass the cookie) or disconnects (pass null).
    /// </summary>
    public void SetVrChatAuthCookie(string? cookie)
    {
        vrChatAuthCookie = string.IsNullOrWhiteSpace(cookie) ? null : cookie.Trim();
    }

    public AvatarImageService()
        : this(SharedHttpClient,
            Path.Combine(AppDataPaths.ThemeAssetsFolder, "AvatarIcons"),
            Path.Combine(AppDataPaths.ThemeAssetsFolder, "AvatarIcons", "Cache"))
    {
    }

    // Injected clients remain caller-owned. Production uses the restricted shared handler.
    internal AvatarImageService(HttpClient httpClient, string iconFolder, string cacheFolder)
    {
        this.httpClient = httpClient;
        this.iconFolder = iconFolder;
        this.cacheFolder = cacheFolder;

        if (!string.IsNullOrWhiteSpace(iconFolder) && !Directory.Exists(iconFolder))
        {
            Directory.CreateDirectory(iconFolder);
        }
        if (!string.IsNullOrWhiteSpace(cacheFolder) && !Directory.Exists(cacheFolder))
        {
            Directory.CreateDirectory(cacheFolder);
        }
    }

    /// <summary>
    /// Gets the image source for an avatar. Tries custom icon first, then VRChat thumbnail, then placeholder.
    /// </summary>
    public ImageSource? GetAvatarImage(
        string avatarId,
        string? customIconPath,
        string? vrchatThumbnailUrl)
    {
        var cacheKey = BuildMemoryCacheKey(avatarId, customIconPath, vrchatThumbnailUrl);
        if (imageCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var image = LoadCustomIcon(customIconPath)
            ?? LoadVrChatThumbnail(avatarId, vrchatThumbnailUrl)
            ?? GetPlaceholderImage();

        imageCache[cacheKey] = image;
        return image;
    }

    /// <summary>
    /// Gets the custom icon for an avatar synchronously, without loading VRChat thumbnails.
    /// Returns null if no custom icon is available.
    /// </summary>
    public ImageSource? GetCustomIconOnly(string? customIconPath)
    {
        return LoadCustomIcon(customIconPath);
    }

    /// <summary>
    /// Gets the image source for an avatar asynchronously. Tries custom icon first, then VRChat thumbnail, then placeholder.
    /// </summary>
    public async Task<ImageSource?> GetAvatarImageAsync(
        string avatarId,
        string? customIconPath,
        string? vrchatThumbnailUrl,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildMemoryCacheKey(avatarId, customIconPath, vrchatThumbnailUrl);
        if (imageCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var image = LoadCustomIcon(customIconPath)
            ?? await LoadVrChatThumbnailAsync(avatarId, vrchatThumbnailUrl, cancellationToken)
            ?? GetPlaceholderImage();

        imageCache[cacheKey] = image;
        return image;
    }

    private string BuildMemoryCacheKey(string avatarId, string? customIconPath, string? vrchatThumbnailUrl)
    {
        var customIconKey = ResolveCustomIconPath(customIconPath) ?? string.Empty;
        var thumbnailKey = string.IsNullOrWhiteSpace(vrchatThumbnailUrl) ? string.Empty : vrchatThumbnailUrl.Trim();
        return string.Join("|", avatarId.Trim(), customIconKey, thumbnailKey);
    }

    /// <summary>
    /// Clears the in-memory image cache. Called when avatar list is refreshed.
    /// </summary>
    public void ClearCache()
    {
        imageCache.Clear();
    }

    /// <summary>
    /// Deletes all cached thumbnail files from disk so they are re-downloaded on next load.
    /// </summary>
    public void ClearDiskCache()
    {
        if (string.IsNullOrWhiteSpace(cacheFolder) || !Directory.Exists(cacheFolder))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(cacheFolder))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // File may be locked; skip and continue
            }
        }
    }

    /// <summary>
    /// Saves a custom icon file for an avatar and returns the relative path.
    /// </summary>
    public string? SaveCustomIcon(string avatarId, string sourceFilePath)
    {
        if (!IsSafeAvatarId(avatarId) || string.IsNullOrWhiteSpace(iconFolder) || !File.Exists(sourceFilePath))
        {
            return null;
        }

        var extension = Path.GetExtension(sourceFilePath);
        var fileName = $"{avatarId}{extension}";
        var destPath = Path.Combine(iconFolder, fileName);

        File.Copy(sourceFilePath, destPath, overwrite: true);
        return Path.Combine("AvatarIcons", fileName);
    }

    /// <summary>
    /// Gets the full path to a custom icon from its relative path.
    /// </summary>
    public string? ResolveCustomIconPath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(iconFolder))
        {
            return null;
        }

        var fullPath = Path.Combine(iconFolder, Path.GetFileName(relativePath));
        return File.Exists(fullPath) ? fullPath : null;
    }

    private ImageSource? LoadCustomIcon(string? customIconPath)
    {
        var fullPath = ResolveCustomIconPath(customIconPath);
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(fullPath);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private ImageSource? LoadVrChatThumbnail(string avatarId, string? thumbnailUrl)
    {
        if (!IsSafeAvatarId(avatarId) || string.IsNullOrWhiteSpace(thumbnailUrl) || string.IsNullOrWhiteSpace(cacheFolder))
        {
            return null;
        }

        var cachePath = Path.Combine(cacheFolder, $"{avatarId}.jpg");
        if (File.Exists(cachePath))
        {
            return LoadImageFromFile(cachePath);
        }

        try
        {
            using var response = SendThumbnailRequestAsync(thumbnailUrl, CancellationToken.None).GetAwaiter().GetResult();
            if (response is null || !response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = ReadThumbnailBytesAsync(response, CancellationToken.None).GetAwaiter().GetResult();
            File.WriteAllBytes(cachePath, bytes);
            return LoadImageFromFile(cachePath);
        }
        catch
        {
            return null;
        }
    }

    private async Task<ImageSource?> LoadVrChatThumbnailAsync(string avatarId, string? thumbnailUrl, CancellationToken cancellationToken)
    {
        if (!IsSafeAvatarId(avatarId) || string.IsNullOrWhiteSpace(thumbnailUrl) || string.IsNullOrWhiteSpace(cacheFolder))
        {
            return null;
        }

        var cachePath = Path.Combine(cacheFolder, $"{avatarId}.jpg");
        if (File.Exists(cachePath))
        {
            return LoadImageFromFile(cachePath);
        }

        try
        {
            using var response = await SendThumbnailRequestAsync(thumbnailUrl, cancellationToken).ConfigureAwait(false);
            if (response is null || !response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[AvatarImageService] HTTP {(int)response.StatusCode} for {thumbnailUrl}");
                return null;
            }

            var bytes = await ReadThumbnailBytesAsync(response, cancellationToken).ConfigureAwait(false);
            File.WriteAllBytes(cachePath, bytes);
            return LoadImageFromFile(cachePath);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsSafeAvatarId(string? avatarId) =>
        !string.IsNullOrWhiteSpace(avatarId) && avatarId.Length <= 128
        && avatarId.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private async Task<HttpResponseMessage?> SendThumbnailRequestAsync(string thumbnailUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out var uri)) return null;
        for (var redirect = 0; redirect <= 4; redirect++)
        {
            if (uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)) return null;
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            // The VRChat session belongs only to the exact HTTPS API origin, never a CDN or imported URL.
            if (uri.IsDefaultPort && string.Equals(uri.Host, "api.vrchat.cloud", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(vrChatAuthCookie))
            {
                request.Headers.TryAddWithoutValidation("Cookie", $"auth={vrChatAuthCookie}");
            }

            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is not (HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect
                or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
                return response;

            var location = response.Headers.Location;
            response.Dispose();
            if (location is null || !Uri.TryCreate(uri, location, out var next)) return null;
            uri = next;
        }
        return null;
    }

    private static async Task<byte[]> ReadThumbnailBytesAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaxThumbnailBytes)
            throw new InvalidDataException("Avatar thumbnail exceeded the download limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (bytes.Length + count > MaxThumbnailBytes)
                throw new InvalidDataException("Avatar thumbnail exceeded the download limit.");
            bytes.Write(buffer, 0, count);
        }
        return bytes.ToArray();
    }

    private static ImageSource? LoadImageFromFile(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the built-in placeholder image used when no custom icon or thumbnail is available.
    /// </summary>
    public ImageSource GetPlaceholderImage() => GetPlaceholderImageCore();

    private static ImageSource GetPlaceholderImageCore()
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(40, 25, 60)),
                new Pen(new SolidColorBrush(Color.FromRgb(80, 50, 120)), 1),
                new Rect(0, 0, 120, 120));

            context.DrawEllipse(
                new SolidColorBrush(Color.FromRgb(120, 90, 160)),
                null,
                new Point(60, 45), 20, 20);
            context.DrawEllipse(
                new SolidColorBrush(Color.FromRgb(120, 90, 160)),
                null,
                new Point(60, 95), 35, 25);
        }

        drawing.Freeze();
        var visual = new DrawingImage(drawing);
        visual.Freeze();
        return visual;
    }
}
