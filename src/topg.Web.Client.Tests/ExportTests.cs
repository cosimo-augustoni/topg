using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using topg.Web.Client.Creator.Export;
using topg.Web.Client.Creator.Images;
using topg.Web.Client.Creator.Model;
using topg.Web.Client.Creator.Storage;
using topg.Web.Client.Shared;
using static topg.Web.Client.Tests.TestProjects;

namespace topg.Web.Client.Tests;

/// <summary>Projects with real image bytes in an in-memory store.</summary>
public static class ExportFixtures
{
    public static readonly DateTimeOffset GeneratedAt = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    public static async Task<ImageRef> StoreImage(InMemoryCreatorStorage storage, string text, string name = "cat.png")
    {
        var content = Encoding.UTF8.GetBytes(text);
        var hash = ImageNaming.ComputeHash(content);
        await storage.PutImageAsync(hash, "png", "image/png", content);
        return new ImageRef(hash, "png", "image/png", name);
    }

    /// <summary>Two boards, text and image questions, special characters – the golden file's input.</summary>
    public static async Task<QuizProject> Sample(InMemoryCreatorStorage storage)
    {
        var flag = await StoreImage(storage, "flag", "flag.png");
        var answer = await StoreImage(storage, "answer", "answer.png");

        var project = QuizProject.Create("Pub Quiz – O'Brien's \"Night\"", "https://cdn.example.com/quiz/");
        project.Folder = "pub-quiz";
        project.Boards.Add(new BoardDraft
        {
            Categories =
            [
                Category("Zürich", Text(200, "Line 1\nLine 2", "C:\\path"), Text(100, "It's 100 %?", "Yes")),
                Category("Animals", new ImageQuestionDraft
                {
                    Points = 100, QuestionText = "Which flag?", QuestionImage = flag, AnswerText = "Switzerland",
                    AnswerImage = answer, ImageSize = ImageSize.Large, AnswerType = AnswerType.Text,
                }),
            ],
        });
        project.Boards.Add(new BoardDraft
        {
            Categories =
            [
                Category("Second", new ImageQuestionDraft { Points = 300, QuestionText = "No answer image", QuestionImage = flag, AnswerText = "" }),
            ],
        });
        return project;
    }
}

public class SqlExportTests
{
    [Fact]
    public async Task Script_matches_golden_file()
    {
        var project = await ExportFixtures.Sample(new InMemoryCreatorStorage());

        Golden.AssertMatches("sample-replace.sql", SqlExport.Generate(project, ExportFixtures.GeneratedAt));
    }

    [Fact]
    public async Task Insert_only_script_matches_golden_file()
    {
        var project = await ExportFixtures.Sample(new InMemoryCreatorStorage());
        project.ReplaceExisting = false;

        Golden.AssertMatches("sample-insert-only.sql", SqlExport.Generate(project, ExportFixtures.GeneratedAt));
    }

    [Theory]
    [InlineData("plain", "'plain'")]
    [InlineData("O'Brien", "'O''Brien'")]
    [InlineData("back\\slash", "'back\\slash'")]
    [InlineData("line\nbreak", "'line\nbreak'")]
    [InlineData("nul\0char", "'nulchar'")]
    [InlineData("", "''")]
    [InlineData(null, "''")]
    public void Literal_escapes_only_quotes(string? value, string expected)
    {
        Assert.Equal(expected, SqlExport.Literal(value));
    }

    [Fact]
    public void Dollar_quote_tag_avoids_texts_containing_it()
    {
        var project = Valid();
        project.Boards[0].Categories[0].Questions[0].QuestionText = "What does $topg$ do?";

        var sql = SqlExport.Generate(project, ExportFixtures.GeneratedAt);

        Assert.Contains("DO $topg1$", sql);
        Assert.EndsWith("$topg1$;\n", sql);
    }

    [Fact]
    public void Newline_in_the_name_cannot_escape_a_comment()
    {
        var project = Valid();
        project.Name = "Quiz\nDROP TABLE \"Templates\";";

        var sql = SqlExport.Generate(project, ExportFixtures.GeneratedAt);

        // In comments the name is flattened to one line; in string literals the line break is harmless data.
        Assert.Contains("-- Quiz:      Quiz DROP TABLE \"Templates\";\n", sql);
        Assert.Contains("VALUES ('Quiz\nDROP TABLE \"Templates\";')", sql);
    }

    [Fact]
    public void Boards_are_numbered_without_gaps_in_list_order()
    {
        var project = Valid();
        project.Boards.Add(new BoardDraft { Categories = [Category("B", Text(100))] });
        project.Boards.Add(new BoardDraft { Categories = [Category("C", Text(100))] });

        var sql = SqlExport.Generate(project, ExportFixtures.GeneratedAt);

        Assert.Contains("(template_id, 0)", sql);
        Assert.Contains("(template_id, 1)", sql);
        Assert.Contains("(template_id, 2)", sql);
    }
}

public class ExportPackageTests
{
    [Fact]
    public async Task Zip_contains_sql_and_only_referenced_images_in_the_cdn_folder()
    {
        var storage = new InMemoryCreatorStorage();
        var project = await ExportFixtures.Sample(storage);
        await ExportFixtures.StoreImage(storage, "unused");

        var bytes = await ExportPackage.BuildAsync(project, storage, ExportFixtures.GeneratedAt);

        using var zip = new ZipArchive(new MemoryStream(bytes));
        var images = project.AllImages().Select(i => $"pub-quiz/{i.FileName}").Distinct().Order(StringComparer.Ordinal);
        Assert.Equal(["import.sql", .. images], zip.Entries.Select(e => e.FullName));
        using var reader = new StreamReader(zip.GetEntry("import.sql")!.Open());
        Assert.Equal(SqlExport.Generate(project, ExportFixtures.GeneratedAt), reader.ReadToEnd());
        Assert.Equal("pub-quiz-export.zip", ExportPackage.FileName(project));
    }

    [Fact]
    public async Task Every_url_in_the_sql_has_a_file_at_the_matching_path()
    {
        var storage = new InMemoryCreatorStorage();
        var project = await ExportFixtures.Sample(storage);
        var bytes = await ExportPackage.BuildAsync(project, storage, ExportFixtures.GeneratedAt);

        using var zip = new ZipArchive(new MemoryStream(bytes));
        var sql = new StreamReader(zip.GetEntry("import.sql")!.Open()).ReadToEnd();
        var prefix = "https://cdn.example.com/quiz/";
        var urls = System.Text.RegularExpressions.Regex.Matches(sql, $"'{prefix}([^']+)'").Select(m => m.Groups[1].Value).ToList();

        Assert.NotEmpty(urls);
        Assert.All(urls, path => Assert.NotNull(zip.GetEntry(path)));
    }

    [Fact]
    public async Task Missing_image_fails_with_its_name()
    {
        var storage = new InMemoryCreatorStorage();
        var project = await ExportFixtures.Sample(storage);
        storage.Images.Clear();

        var error = await Assert.ThrowsAsync<ExportException>(() => ExportPackage.BuildAsync(project, storage, ExportFixtures.GeneratedAt));
        Assert.Contains("flag.png", error.Message);
    }
}

public class ProjectFileTests
{
    private readonly InMemoryCreatorStorage _source = new();

    private static ProjectFileContent Read(byte[] bytes) => ProjectFile.Read(new MemoryStream(bytes));

    private static byte[] Zip(params (string Path, byte[] Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                using var stream = zip.CreateEntry(path).Open();
                stream.Write(content);
            }
        }

        return buffer.ToArray();
    }

    [Fact]
    public async Task Round_trip_to_another_browser_gives_an_identical_project()
    {
        var project = await ExportFixtures.Sample(_source);
        var file = await ProjectFile.CreateAsync(project, _source);

        var target = new InMemoryCreatorStorage();
        var targetStore = new ProjectStore(target);
        await ProjectFile.ImportAsync(Read(file), asCopy: false, target, targetStore);

        var imported = await targetStore.GetAsync(project.Id);
        Assert.Equal(CreatorJson.Serialize(project), CreatorJson.Serialize(imported));
        Assert.Equal(_source.Images.Keys.Order(), target.Images.Keys.Order());

        // Exports from both browsers are byte-identical (the SQL only differs in its generation time).
        Assert.Equal(file, await ProjectFile.CreateAsync(imported!, target));
        Assert.Equal(
            await ExportPackage.BuildAsync(project, _source, ExportFixtures.GeneratedAt),
            await ExportPackage.BuildAsync(imported!, target, ExportFixtures.GeneratedAt));
    }

    [Fact]
    public async Task Project_file_contains_indented_json_with_schema_version_and_images()
    {
        var project = await ExportFixtures.Sample(_source);

        using var zip = new ZipArchive(new MemoryStream(await ProjectFile.CreateAsync(project, _source)));

        Assert.Equal("project.json", zip.Entries[0].FullName);
        Assert.All(zip.Entries.Skip(1), e => Assert.StartsWith("images/", e.FullName));
        var json = new StreamReader(zip.Entries[0].Open()).ReadToEnd();
        Assert.Contains("\n  \"schemaVersion\": 1", json.ReplaceLineEndings("\n"));
        Assert.Equal("pub-quiz.topgquiz", ProjectFile.FileName(project));
    }

    [Fact]
    public async Task Import_as_copy_gets_new_ids_and_copy_name()
    {
        var project = await ExportFixtures.Sample(_source);
        var store = new ProjectStore(_source);
        await store.SaveAsync(project);

        var copy = await ProjectFile.ImportAsync(Read(await ProjectFile.CreateAsync(project, _source)), asCopy: true, _source, store);

        Assert.NotEqual(project.Id, copy.Id);
        Assert.EndsWith("(copy)", copy.Name);
        Assert.Equal(2, (await store.ListAsync()).Count);
    }

    [Fact]
    public void Not_a_zip_is_rejected()
    {
        var error = Assert.Throws<ProjectFileException>(() => Read(Encoding.UTF8.GetBytes("hello")));
        Assert.Equal("Not a topg project file.", error.Message);
    }

    [Fact]
    public void Zip_without_project_json_is_rejected()
    {
        var error = Assert.Throws<ProjectFileException>(() => Read(Zip(("readme.txt", [1]))));
        Assert.Equal("Not a topg project file.", error.Message);
    }

    [Fact]
    public void Newer_schema_is_rejected_with_the_version()
    {
        var json = JsonNode.Parse(CreatorJson.Serialize(Valid()))!;
        json["schemaVersion"] = 3;

        var error = Assert.Throws<ProjectFileException>(() => Read(Zip(("project.json", Encoding.UTF8.GetBytes(json.ToJsonString())))));
        Assert.Equal("Created by a newer version (schema 3) – please update.", error.Message);
    }

    [Fact]
    public async Task Corrupt_image_is_detected_by_its_hash()
    {
        var project = await ExportFixtures.Sample(_source);
        var image = project.AllImages().First();
        var json = Encoding.UTF8.GetBytes(CreatorJson.Serialize(project));
        var entries = project.AllImages().DistinctBy(i => i.Hash)
            .Select(i => ($"images/{i.FileName}", i.Hash == image.Hash ? new byte[] { 0 } : _source.Images[i.Hash].Content))
            .Prepend(("project.json", json))
            .ToArray();

        var error = Assert.Throws<ProjectFileException>(() => Read(Zip(entries)));
        Assert.Equal($"Image {image.FileName} is corrupt (hash mismatch).", error.Message);
    }

    [Fact]
    public async Task Missing_image_is_reported()
    {
        var project = await ExportFixtures.Sample(_source);

        var error = Assert.Throws<ProjectFileException>(() => Read(Zip(("project.json", Encoding.UTF8.GetBytes(CreatorJson.Serialize(project))))));
        Assert.Contains("is missing in the file", error.Message);
    }
}
