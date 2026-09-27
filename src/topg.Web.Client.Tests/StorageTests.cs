using topg.Web.Client.Creator.Images;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Storage;
using static topg.Web.Client.Tests.TestProjects;

namespace topg.Web.Client.Tests;

public class ProjectStoreTests
{
    private readonly InMemoryCreatorStorage _storage = new();
    private readonly ProjectStore _store;

    public ProjectStoreTests() => _store = new ProjectStore(_storage);

    private async Task<StoredImage> StoreImage(string hash) => await _storage.PutImageAsync(hash, "png", "image/png", [1, 2, 3]);

    private static QuizProject ProjectUsing(params string[] hashes)
    {
        var project = Valid();
        project.Boards[0].Categories[1].Questions = hashes.Select((h, i) => (QuestionDraft)ImageQuestion(100 * (i + 1), Image(h))).ToList();
        return project;
    }

    [Fact]
    public async Task Saved_project_can_be_loaded_and_is_listed()
    {
        var project = Valid();
        var before = project.UpdatedAt;
        await Task.Delay(5);

        await _store.SaveAsync(project);
        var loaded = await _store.GetAsync(project.Id);
        var summary = Assert.Single(await _store.ListAsync());

        Assert.NotNull(loaded);
        Assert.Equal(project.Name, loaded.Name);
        Assert.True(project.UpdatedAt > before);
        Assert.Equal((project.Id, "Pub Quiz", 1, 4, project.UpdatedAt, false), (summary.Id, summary.Name, summary.BoardCount, summary.QuestionCount, summary.UpdatedAt, summary.IsCorrupt));
        Assert.Empty(summary.Issues);
    }

    [Fact]
    public async Task Summary_contains_validation_issues()
    {
        var project = Valid();
        project.Name = "";
        await _store.SaveAsync(project);

        var summary = Assert.Single(await _store.ListAsync());

        Assert.Equal("project.name.empty", Assert.Single(summary.Issues).Code);
    }

    [Fact]
    public async Task List_is_sorted_by_last_change_and_marks_unreadable_projects()
    {
        var older = Valid();
        var newer = Valid();
        await _store.SaveAsync(older);
        await Task.Delay(5);
        await _store.SaveAsync(newer);
        var corruptId = Guid.NewGuid();
        _storage.Projects[corruptId] = $$"""{ "id": "{{corruptId}}", "boards": "not a list" }""";

        var list = await _store.ListAsync();

        Assert.Equal([newer.Id, older.Id, corruptId], list.Select(p => p.Id));
        Assert.True(list[2].IsCorrupt);
    }

    [Fact]
    public async Task Deleting_a_project_removes_only_images_no_other_project_uses()
    {
        await StoreImage("aaaaaaaaaaaaaaaa");
        await StoreImage("bbbbbbbbbbbbbbbb");
        await StoreImage("cccccccccccccccc");
        var deleted = ProjectUsing("aaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbb");
        var kept = ProjectUsing("bbbbbbbbbbbbbbbb");
        await _store.SaveAsync(deleted);
        await _store.SaveAsync(kept);

        await _store.DeleteAsync(deleted.Id);

        Assert.Null(await _store.GetAsync(deleted.Id));
        Assert.Equal(["bbbbbbbbbbbbbbbb", "cccccccccccccccc"], _storage.Images.Keys.Order());
    }

    [Fact]
    public async Task Deleting_keeps_images_when_another_project_is_unreadable()
    {
        await StoreImage("aaaaaaaaaaaaaaaa");
        var project = ProjectUsing("aaaaaaaaaaaaaaaa");
        await _store.SaveAsync(project);
        _storage.Projects[Guid.NewGuid()] = "{ broken";

        await _store.DeleteAsync(project.Id);

        Assert.Contains("aaaaaaaaaaaaaaaa", _storage.Images.Keys);
    }

    [Fact]
    public async Task Unused_images_are_found_and_deleted()
    {
        await StoreImage("aaaaaaaaaaaaaaaa");
        await StoreImage("bbbbbbbbbbbbbbbb");
        await _store.SaveAsync(ProjectUsing("aaaaaaaaaaaaaaaa"));

        var unused = await _store.FindUnusedImagesAsync();
        Assert.Equal("bbbbbbbbbbbbbbbb", Assert.Single(unused.Images).Hash);
        Assert.Equal(3, unused.TotalSize);

        await _store.DeleteUnusedImagesAsync();
        Assert.Equal(["aaaaaaaaaaaaaaaa"], _storage.Images.Keys);
    }

    [Fact]
    public async Task Unused_images_cannot_be_determined_with_an_unreadable_project()
    {
        _storage.Projects[Guid.NewGuid()] = "{ broken";

        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.FindUnusedImagesAsync());
    }
}

public class ProjectAutosaveTests
{
    private readonly InMemoryCreatorStorage _storage = new();

    private ProjectAutosave CreateAutosave(TimeSpan delay) => new(new ProjectStore(_storage), delay);

    private static async Task WaitUntil(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < timeout, "Timed out.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Rapid_changes_are_saved_once_after_the_delay()
    {
        await using var autosave = CreateAutosave(TimeSpan.FromMilliseconds(100));
        var project = Valid();

        for (var i = 0; i < 5; i++)
        {
            project.Name = $"Name {i}";
            autosave.ScheduleSave(project);
            Assert.Equal(SaveStatus.Saving, autosave.Status);
        }

        await WaitUntil(() => autosave.Status == SaveStatus.Saved);

        Assert.Equal(1, _storage.SaveCount);
        Assert.Contains("Name 4", _storage.Projects[project.Id]);
        Assert.NotNull(autosave.LastSavedAt);
        Assert.False(autosave.HasPendingChanges);
    }

    [Fact]
    public async Task SaveNow_writes_immediately()
    {
        await using var autosave = CreateAutosave(TimeSpan.FromMinutes(1));
        var project = Valid();

        autosave.ScheduleSave(project);
        await autosave.SaveNowAsync();

        Assert.Equal(1, _storage.SaveCount);
        Assert.Equal(SaveStatus.Saved, autosave.Status);
    }

    [Fact]
    public async Task Failed_save_reports_the_error_and_can_be_retried()
    {
        await using var autosave = CreateAutosave(TimeSpan.FromMinutes(1));
        var project = Valid();
        _storage.FailSavesWith = new InvalidOperationException("Quota exceeded");

        autosave.ScheduleSave(project);
        await autosave.SaveNowAsync();

        Assert.Equal(SaveStatus.Failed, autosave.Status);
        Assert.Equal("Quota exceeded", autosave.Error);
        Assert.True(autosave.HasPendingChanges);

        _storage.FailSavesWith = null;
        await autosave.SaveNowAsync();

        Assert.Equal(SaveStatus.Saved, autosave.Status);
        Assert.Null(autosave.Error);
    }

    [Fact]
    public async Task Dispose_flushes_pending_changes()
    {
        var autosave = CreateAutosave(TimeSpan.FromMinutes(1));
        autosave.ScheduleSave(Valid());

        await autosave.DisposeAsync();

        Assert.Equal(1, _storage.SaveCount);
    }
}

public class ImageIntakeTests
{
    private readonly InMemoryCreatorStorage _storage = new();

    [Fact]
    public async Task Same_file_twice_is_stored_once_with_the_same_name()
    {
        var intake = new ImageIntake(_storage);
        var content = new byte[] { 137, 80, 78, 71, 1, 2, 3 };

        var first = await intake.ImportAsync(new FakeBrowserFile("cat.png", "image/png", content));
        var second = await intake.ImportAsync(new FakeBrowserFile("copy of cat.PNG", "image/png", content));

        Assert.Single(_storage.Images);
        Assert.Equal(first.FileName, second.FileName);
        Assert.Equal($"{ImageNaming.ComputeHash(content)}.png", first.FileName);
        Assert.Equal("cat.png", first.OriginalFileName);
        Assert.Equal(content, _storage.Images[first.Hash].Content);
    }

    [Fact]
    public async Task Jpeg_gets_jpg_extension()
    {
        var image = await new ImageIntake(_storage).ImportAsync(new FakeBrowserFile("photo.jpeg", "image/jpeg", [1, 2]));

        Assert.EndsWith(".jpg", image.FileName);
        Assert.Equal("image/jpeg", image.ContentType);
    }

    [Fact]
    public async Task Unsupported_type_is_rejected_with_a_readable_message()
    {
        var error = await Assert.ThrowsAsync<ImageRejectedException>(() =>
            new ImageIntake(_storage).ImportAsync(new FakeBrowserFile("drawing.svg", "image/svg+xml", [1])));

        Assert.Equal("\"drawing.svg\" is not a supported image. Use PNG, JPG, WEBP or GIF.", error.Message);
        Assert.Empty(_storage.Images);
    }

    [Fact]
    public async Task Too_large_file_is_rejected()
    {
        var intake = new ImageIntake(_storage) { MaxFileSize = 2 * 1024 };

        var error = await Assert.ThrowsAsync<ImageRejectedException>(() =>
            intake.ImportAsync(new FakeBrowserFile("big.png", "image/png", new byte[3 * 1024])));

        Assert.Equal("\"big.png\" is 3 KB. The maximum is 2 KB.", error.Message);
        Assert.Empty(_storage.Images);
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(412 * 1024, "412 KB")]
    [InlineData(1536 * 1024, "1.5 MB")]
    [InlineData(10 * 1024 * 1024, "10 MB")]
    [InlineData(11878L * 1024 * 1024, "11.6 GB")]
    public void FormatSize_is_culture_independent(long bytes, string expected)
    {
        Assert.Equal(expected, ImageIntake.FormatSize(bytes));
    }
}
