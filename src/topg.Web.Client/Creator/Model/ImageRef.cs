namespace topg.Web.Client.Creator.Model;

/// <summary>
/// Reference from a question to an image in the image store. The bytes are stored once per <see cref="Hash"/>.
/// </summary>
/// <param name="Hash">First 16 hex characters of the SHA-256 of the file content.</param>
/// <param name="Extension">Normalized lowercase extension without dot (<c>png</c>, <c>jpg</c>, <c>webp</c>, <c>gif</c>).</param>
public record ImageRef(string Hash, string Extension, string ContentType, string OriginalFileName)
{
    /// <summary>File name on the CDN and in the export ZIP.</summary>
    public string FileName => $"{Hash}.{Extension}";

    /// <summary>Final URL the game loads the image from: <c>{baseUrl}/{folder}/{hash}.{ext}</c>.</summary>
    public string Url(string baseUrl, string folder) => $"{UrlPrefix(baseUrl, folder)}{FileName}";

    /// <summary><c>{baseUrl}/{folder}/</c>, without doubled slashes.</summary>
    public static string UrlPrefix(string baseUrl, string folder) => $"{baseUrl.Trim().TrimEnd('/')}/{folder.Trim().Trim('/')}/";
}
