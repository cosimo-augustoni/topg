using System.Text.Json;
using System.Text.Json.Nodes;
using topg.Web.Client.Creator;
using topg.Web.Client.Creator.Images;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Settings;
using topg.Web.Client.Creator.Storage;
using topg.Web.Client.Shared;
using static topg.Web.Client.Tests.TestProjects;

namespace topg.Web.Client.Tests;

public class SlugTests
{
    [Theory]
    [InlineData("Pub Quiz Sept", "pub-quiz-sept")]
    [InlineData("  Pub  Quiz – Sept. 2026!  ", "pub-quiz-sept-2026")]
    [InlineData("Grüezi Zürich", "grueezi-zuerich")]
    [InlineData("Straße", "strasse")]
    [InlineData("Café Crème", "cafe-creme")]
    [InlineData("already-a-slug", "already-a-slug")]
    [InlineData("---", Slug.Fallback)]
    [InlineData("", Slug.Fallback)]
    [InlineData(null, Slug.Fallback)]
    public void FromName_creates_valid_slugs(string? name, string expected)
    {
        var slug = Slug.FromName(name);

        Assert.Equal(expected, slug);
        Assert.True(Slug.IsValid(slug));
    }

    [Fact]
    public void Create_derives_folder_from_name()
    {
        var project = QuizProject.Create(" Pub Quiz ", " https://cdn.example.com ");

        Assert.Equal("Pub Quiz", project.Name);
        Assert.Equal("pub-quiz", project.Folder);
        Assert.Equal("https://cdn.example.com", project.BaseUrl);
    }
}

public class ImageNamingTests
{
    [Fact]
    public void Hash_is_first_16_lowercase_hex_chars_of_sha256()
    {
        // SHA-256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
        Assert.Equal("ba7816bf8f01cfea", ImageNaming.ComputeHash("abc"u8));
    }

    [Theory]
    [InlineData("image/png", "a.png", "png", "image/png")]
    [InlineData("image/jpeg", "a.jpeg", "jpg", "image/jpeg")]
    [InlineData("IMAGE/JPG", "a.jpg", "jpg", "image/jpeg")]
    [InlineData("image/webp", "a.webp", "webp", "image/webp")]
    [InlineData("image/gif", "a.gif", "gif", "image/gif")]
    [InlineData("", "photo.JPEG", "jpg", "image/jpeg")]
    [InlineData(null, "photo.png", "png", "image/png")]
    [InlineData("image/png", "wrong-extension.gif", "png", "image/png")]
    public void Supported_types_are_normalized(string? contentType, string fileName, string extension, string normalizedType)
    {
        Assert.True(ImageNaming.TryResolveType(contentType, fileName, out var ext, out var type));
        Assert.Equal(extension, ext);
        Assert.Equal(normalizedType, type);
    }

    [Theory]
    [InlineData("image/svg+xml", "a.svg")]
    [InlineData("application/pdf", "a.png")]
    [InlineData("", "a.bmp")]
    [InlineData("", "noextension")]
    public void Unsupported_types_are_rejected(string contentType, string fileName)
    {
        Assert.False(ImageNaming.TryResolveType(contentType, fileName, out _, out _));
    }

    [Theory]
    [InlineData("https://cdn.example.com", "pub-quiz")]
    [InlineData("https://cdn.example.com/", "pub-quiz")]
    [InlineData(" https://cdn.example.com// ", "/pub-quiz/")]
    public void Url_follows_pattern_without_double_slashes(string baseUrl, string folder)
    {
        var image = Image("3f9a1c0b2d4e5f60");

        Assert.Equal("https://cdn.example.com/pub-quiz/3f9a1c0b2d4e5f60.png", image.Url(baseUrl, folder));
    }
}

public class CreatorJsonTests
{
    [Fact]
    public void Project_round_trips_with_all_question_types()
    {
        var project = Valid();
        var image = (ImageQuestionDraft)project.Boards[0].Categories[1].Questions[0];
        image.AnswerImage = Image("fedcba9876543210");
        project.LastExportedAt = DateTimeOffset.Parse("2026-09-01T10:00:00+02:00");

        var copy = CreatorJson.Deserialize<QuizProject>(CreatorJson.Serialize(project));

        Assert.Equal(CreatorJson.Serialize(project), CreatorJson.Serialize(copy));
        var copiedImage = Assert.IsType<ImageQuestionDraft>(copy.Boards[0].Categories[1].Questions[0]);
        Assert.Equal(image.QuestionImage, copiedImage.QuestionImage);
        Assert.Equal(image.AnswerImage, copiedImage.AnswerImage);
        Assert.Equal(ImageSize.Large, copiedImage.ImageSize);
        Assert.Equal(AnswerType.Text, copiedImage.AnswerType);
        Assert.IsType<TextQuestionDraft>(copy.Boards[0].Categories[0].Questions[0]);
        Assert.Equal(project.LastExportedAt, copy.LastExportedAt);
    }

    [Fact]
    public void Questions_have_a_type_discriminator_and_enums_are_strings()
    {
        var json = JsonNode.Parse(CreatorJson.Serialize(Valid()))!;
        var questions = json["boards"]![0]!["categories"]![1]!["questions"]!;

        Assert.Equal("image", (string?)questions[0]!["type"]);
        Assert.Equal("text", (string?)questions[1]!["type"]);
        Assert.Equal("Large", (string?)questions[0]!["imageSize"]);
        Assert.Equal(1, (int?)json["schemaVersion"]);
    }

    [Fact]
    public void Discriminator_does_not_have_to_be_the_first_property()
    {
        const string json = """{ "points": 300, "questionText": "Q", "correctAnswer": "A", "type": "text" }""";

        var question = Assert.IsType<TextQuestionDraft>(CreatorJson.Deserialize<QuestionDraft>(json));
        Assert.Equal(300, question.Points);
        Assert.Equal("A", question.CorrectAnswer);
    }

    [Fact]
    public void Unknown_question_type_fails_instead_of_losing_data()
    {
        const string json = """{ "type": "sound", "points": 100 }""";

        Assert.ThrowsAny<JsonException>(() => CreatorJson.Deserialize<QuestionDraft>(json));
    }

    [Fact]
    public void Clone_is_a_deep_copy()
    {
        var project = Valid();
        var clone = CreatorJson.Clone(project);

        clone.Boards[0].Categories[0].Name = "Changed";

        Assert.Equal("History", project.Boards[0].Categories[0].Name);
    }
}

public class CreatorSettingsTests
{
    [Theory]
    [InlineData("100, 200, 300", new[] { 100, 200, 300 })]
    [InlineData("100;200 300", new[] { 100, 200, 300 })]
    [InlineData(" 250 ", new[] { 250 })]
    public void TryParsePoints_accepts_lists(string text, int[] expected)
    {
        Assert.Null(CreatorSettings.TryParsePoints(text, out var points));
        Assert.Equal(expected, points);
    }

    [Theory]
    [InlineData("", "Enter at least one value.")]
    [InlineData("100, abc", "\"abc\" is not a positive whole number.")]
    [InlineData("100, 0", "\"0\" is not a positive whole number.")]
    [InlineData("-5", "\"-5\" is not a positive whole number.")]
    public void TryParsePoints_rejects_invalid_input(string text, string error)
    {
        Assert.Equal(error, CreatorSettings.TryParsePoints(text, out _));
    }

    [Fact]
    public async Task Settings_are_saved_and_loaded_from_storage()
    {
        var storage = new InMemoryCreatorStorage();
        await new CreatorSettingsService(storage).SaveAsync(new CreatorSettings { DefaultBaseUrl = "https://cdn", DefaultPoints = [10, 20], Theme = ThemeMode.System });

        var service = new CreatorSettingsService(storage);
        await service.EnsureLoadedAsync();

        Assert.Equal("https://cdn", service.Current.DefaultBaseUrl);
        Assert.Equal([10, 20], service.Current.DefaultPoints);
        Assert.Equal(ThemeMode.System, service.Current.Theme);
    }

    [Fact]
    public async Task A_change_made_while_the_previous_save_is_still_writing_keeps_both_changes()
    {
        // Regression: blur on "Default points" followed by a click on the theme lost the points.
        var storage = new SlowSettingsStorage();
        var service = new CreatorSettingsService(storage);

        var first = service.SaveAsync(service.Current with { DefaultPoints = [200, 400] });
        var second = service.SaveAsync(service.Current with { Theme = ThemeMode.Light });
        storage.Release();
        await Task.WhenAll(first, second);

        var reloaded = new CreatorSettingsService(storage);
        await reloaded.EnsureLoadedAsync();
        Assert.Equal([200, 400], reloaded.Current.DefaultPoints);
        Assert.Equal(ThemeMode.Light, reloaded.Current.Theme);
    }

    private class SlowSettingsStorage : InMemoryCreatorStorage, ICreatorStorage
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _release.TrySetResult();

        async Task ICreatorStorage.SetSettingAsync(string key, string value)
        {
            await _release.Task;
            await SetSettingAsync(key, value);
        }
    }

    [Fact]
    public async Task Corrupt_settings_fall_back_to_defaults_and_report_an_error()
    {
        var storage = new InMemoryCreatorStorage();
        storage.Settings["creator-settings"] = "{ not json";
        var service = new CreatorSettingsService(storage);

        await service.EnsureLoadedAsync();

        Assert.Equal(new CreatorSettings().Theme, service.Current.Theme);
        Assert.NotNull(service.LoadError);
    }
}

public class CreatorLocationTests
{
    private static readonly Guid Id = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Theory]
    [InlineData("create", null, null)]
    [InlineData("create/settings", null, null)]
    [InlineData("create/11111111-2222-3333-4444-555555555555", "11111111-2222-3333-4444-555555555555", CreatorSection.Boards)]
    [InlineData("create/11111111-2222-3333-4444-555555555555/board/2?x=1", "11111111-2222-3333-4444-555555555555", CreatorSection.Boards)]
    [InlineData("create/11111111-2222-3333-4444-555555555555/quiz", "11111111-2222-3333-4444-555555555555", CreatorSection.QuizSettings)]
    [InlineData("create/11111111-2222-3333-4444-555555555555/export#top", "11111111-2222-3333-4444-555555555555", CreatorSection.Export)]
    [InlineData("host", null, null)]
    public void Parse_extracts_project_and_section(string path, string? projectId, CreatorSection? section)
    {
        var location = CreatorLocation.Parse(path);

        Assert.Equal(projectId is null ? null : Guid.Parse(projectId), location.ProjectId);
        Assert.Equal(section, location.Section);
    }

    [Fact]
    public void Routes_match_ux2()
    {
        Assert.Equal($"/create/{Id}/board/3", CreatorLocation.Board(Id, 3));
        Assert.Equal($"/create/{Id}/quiz", CreatorLocation.ForSection(Id, CreatorSection.QuizSettings));
        Assert.Equal($"/create/{Id}/export", CreatorLocation.ForSection(Id, CreatorSection.Export));
    }
}
