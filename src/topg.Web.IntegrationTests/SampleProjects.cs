using topg.Web.Client.Creator.Export;
using topg.Web.Client.Creator.Model;

namespace topg.Web.IntegrationTests;

public static class SampleProjects
{
    public static QuizProject Read(string fileName)
    {
        using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Samples", fileName));
        return ProjectFile.Read(file).Project;
    }
}
