namespace topg.Web.Client.Creator.Model;

public record ImageRef(string Hash, string Extension, string ContentType, string OriginalFileName)
{
    public string FileName => $"{Hash}.{Extension}";

    public string Url(string baseUrl, string folder) => $"{UrlPrefix(baseUrl, folder)}{FileName}";

    public static string UrlPrefix(string baseUrl, string folder) => $"{baseUrl.Trim().TrimEnd('/')}/{folder.Trim().Trim('/')}/";
}
