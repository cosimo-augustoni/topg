using System.Security.Cryptography;

namespace topg.Web.Client.Creator.Images;

/// <summary>
/// Content-hash naming for images: <c>{first 16 hex chars of SHA-256}.{ext}</c>. The same bytes always get
/// the same name, so re-uploading an image never changes its CDN URL.
/// </summary>
public static class ImageNaming
{
    public const int HashLength = 16;

    private static readonly Dictionary<string, string> ExtensionsByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = "png",
        ["image/jpeg"] = "jpg",
        ["image/jpg"] = "jpg",
        ["image/webp"] = "webp",
        ["image/gif"] = "gif",
    };

    private static readonly Dictionary<string, string> ContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["png"] = "image/png",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["webp"] = "image/webp",
        ["gif"] = "image/gif",
    };

    public const string Accept = ".png,.jpg,.jpeg,.webp,.gif";

    public static string ComputeHash(ReadOnlySpan<byte> content) =>
        Convert.ToHexStringLower(SHA256.HashData(content))[..HashLength];

    /// <summary>
    /// Resolves the normalized extension and content type. The browser's content type wins; the file extension is
    /// the fallback because some systems report an empty type. Returns false for unsupported files.
    /// </summary>
    public static bool TryResolveType(string? contentType, string? fileName, out string extension, out string normalizedContentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType) && ExtensionsByContentType.TryGetValue(contentType.Trim(), out var ext))
        {
            extension = ext;
            normalizedContentType = ContentTypesByExtension[ext];
            return true;
        }

        var fileExtension = Path.GetExtension(fileName ?? "").TrimStart('.');
        if (string.IsNullOrWhiteSpace(contentType) && ContentTypesByExtension.TryGetValue(fileExtension, out var type))
        {
            normalizedContentType = type;
            extension = ExtensionsByContentType[type];
            return true;
        }

        extension = "";
        normalizedContentType = "";
        return false;
    }

    /// <summary>
    /// Detects the type from the file signature. The browser derives its content type from the file name alone, so
    /// a renamed or fake file would otherwise be stored and exported under a type it doesn't have.
    /// </summary>
    public static bool TryDetectType(ReadOnlySpan<byte> content, out string extension, out string contentType)
    {
        extension = content switch
        {
            [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, ..] => "png",
            [0xFF, 0xD8, 0xFF, ..] => "jpg",
            [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'7' or (byte)'9', (byte)'a', ..] => "gif",
            [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => "webp",
            _ => "",
        };
        contentType = extension == "" ? "" : ContentTypesByExtension[extension];
        return extension != "";
    }
}
