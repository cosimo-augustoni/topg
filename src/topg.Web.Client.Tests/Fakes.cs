using Microsoft.AspNetCore.Components.Forms;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Storage;
using topg.Web.Client.Shared;

namespace topg.Web.Client.Tests;

public class InMemoryCreatorStorage : ICreatorStorage
{
    public Dictionary<Guid, string> Projects { get; } = [];
    public Dictionary<string, (StoredImage Metadata, byte[] Content)> Images { get; } = [];
    public Dictionary<string, string> Settings { get; } = [];
    public int SaveCount { get; private set; }
    public Exception? FailSavesWith { get; set; }

    public Task<IReadOnlyList<string>> ListProjectJsonAsync() => Task.FromResult<IReadOnlyList<string>>(Projects.Values.ToList());

    public Task<string?> GetProjectJsonAsync(Guid id) => Task.FromResult(Projects.GetValueOrDefault(id));

    public Task SaveProjectJsonAsync(Guid id, string json)
    {
        if (FailSavesWith is not null)
        {
            throw FailSavesWith;
        }

        SaveCount++;
        Projects[id] = json;
        return Task.CompletedTask;
    }

    public Task DeleteProjectAsync(Guid id)
    {
        Projects.Remove(id);
        return Task.CompletedTask;
    }

    public Task<StoredImage> PutImageAsync(string hash, string extension, string contentType, byte[] content)
    {
        if (!Images.TryGetValue(hash, out var image))
        {
            image = (new StoredImage(hash, extension, contentType, content.Length, null, null, DateTimeOffset.UtcNow), content);
            Images[hash] = image;
        }

        return Task.FromResult(image.Metadata);
    }

    public Task<StoredImage?> GetImageAsync(string hash) =>
        Task.FromResult(Images.TryGetValue(hash, out var image) ? image.Metadata : null);

    public Task<IReadOnlyList<StoredImage>> ListImagesAsync() =>
        Task.FromResult<IReadOnlyList<StoredImage>>(Images.Values.Select(i => i.Metadata).ToList());

    public Task<byte[]?> GetImageBytesAsync(string hash) =>
        Task.FromResult(Images.TryGetValue(hash, out var image) ? image.Content : null);

    public Task DeleteImagesAsync(IReadOnlyCollection<string> hashes)
    {
        foreach (var hash in hashes)
        {
            Images.Remove(hash);
        }

        return Task.CompletedTask;
    }

    public Task<string?> CreateObjectUrlAsync(string hash) => Task.FromResult<string?>($"blob:{hash}");

    public Task RevokeObjectUrlAsync(string url) => Task.CompletedTask;

    public Task<string?> GetSettingAsync(string key) => Task.FromResult(Settings.GetValueOrDefault(key));

    public Task SetSettingAsync(string key, string value)
    {
        Settings[key] = value;
        return Task.CompletedTask;
    }

    public Task<StorageEstimate> GetStorageEstimateAsync() => Task.FromResult(new StorageEstimate(0, 1000, false, true));

    public Task<bool> RequestPersistenceAsync() => Task.FromResult(true);
}

public class FakeBrowserFile(string name, string contentType, byte[] content) : IBrowserFile
{
    public string Name => name;
    public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
    public long Size => content.Length;
    public string ContentType => contentType;

    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
    {
        if (Size > maxAllowedSize)
        {
            throw new IOException("File too large.");
        }

        return new MemoryStream(content);
    }
}

public static class TestProjects
{
    public static ImageRef Image(string hash = "0123456789abcdef") => new(hash, "png", "image/png", "cat.png");

    public static TextQuestionDraft Text(int points, string text = "Question?", string answer = "Answer") =>
        new() { Points = points, QuestionText = text, CorrectAnswer = answer };

    public static ImageQuestionDraft ImageQuestion(int points, ImageRef? image = null, ImageRef? answerImage = null) =>
        new()
        {
            Points = points,
            QuestionText = "What is this?",
            QuestionImage = image ?? Image(),
            AnswerText = "A cat",
            AnswerImage = answerImage,
            ImageSize = ImageSize.Large,
            AnswerType = AnswerType.Text,
        };

    public static CategoryDraft Category(string name, params QuestionDraft[] questions) =>
        new() { Name = name, Questions = [.. questions] };

    public static QuizProject Valid()
    {
        var project = QuizProject.Create("Pub Quiz", "https://cdn.example.com/quiz");
        project.Boards.Add(new BoardDraft
        {
            Categories =
            [
                Category("History", Text(100), Text(200)),
                Category("Animals", ImageQuestion(100), Text(200)),
            ],
        });
        return project;
    }
}
