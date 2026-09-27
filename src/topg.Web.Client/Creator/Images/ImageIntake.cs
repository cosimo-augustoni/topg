using System.Globalization;
using Microsoft.AspNetCore.Components.Forms;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Storage;

namespace topg.Web.Client.Creator.Images;

public class ImageRejectedException(string message) : Exception(message);

public class ImageIntake(ICreatorStorage storage)
{
    public const long DefaultMaxFileSize = 10 * 1024 * 1024;

    public long MaxFileSize { get; init; } = DefaultMaxFileSize;

    public async Task<ImageRef> ImportAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        if (!ImageNaming.TryResolveType(file.ContentType, file.Name, out var extension, out var contentType))
        {
            throw new ImageRejectedException($"\"{file.Name}\" is not a supported image. Use PNG, JPG, WEBP or GIF.");
        }

        if (file.Size > MaxFileSize)
        {
            throw new ImageRejectedException($"\"{file.Name}\" is {FormatSize(file.Size)}. The maximum is {FormatSize(MaxFileSize)}.");
        }

        using var buffer = new MemoryStream((int)file.Size);
        await using (var stream = file.OpenReadStream(MaxFileSize, cancellationToken))
        {
            await stream.CopyToAsync(buffer, cancellationToken);
        }

        var content = buffer.ToArray();
        var hash = ImageNaming.ComputeHash(content);
        await storage.PutImageAsync(hash, extension, contentType, content);

        return new ImageRef(hash, extension, contentType, file.Name);
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024d * 1024 * 1024):0.#} GB"),
        >= 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024d * 1024):0.#} MB"),
        >= 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024d:0} KB"),
        _ => $"{bytes} B",
    };
}
