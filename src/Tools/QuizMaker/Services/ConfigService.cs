using Microsoft.EntityFrameworkCore;
using QuizMaker.Data;

namespace QuizMaker.Services;

public class ConfigService
{
    private readonly QuizDbContext _db;

    public ConfigService(QuizDbContext db)
    {
        _db = db;
    }

    public async Task<string> GetBaseUrlAsync()
    {
        var m = await _db.Metadata.FindAsync("BaseUrl");
        return m?.Value ?? "http://localhost/media";
    }

    public async Task SetBaseUrlAsync(string url)
    {
        var m = await _db.Metadata.FindAsync("BaseUrl");
        if (m == null)
        {
            _db.Metadata.Add(new Data.Entities.MetadataEntity { Key = "BaseUrl", Value = url });
        }
        else
        {
            m.Value = url;
        }
        await _db.SaveChangesAsync();
    }
}
