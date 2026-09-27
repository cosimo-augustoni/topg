using System.Runtime.CompilerServices;

namespace topg.Web.Client.Tests;

public static class Golden
{
    public static void AssertMatches(string fileName, string actual, [CallerFilePath] string callerFile = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(callerFile)!, "Golden", fileName);
        var normalized = actual.ReplaceLineEndings("\n");

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var existed = File.Exists(path);
            File.WriteAllText(path, normalized);
            Assert.True(existed, $"Golden file {fileName} was created – review it and run the test again.");
            return;
        }

        var expected = File.ReadAllText(path).ReplaceLineEndings("\n");
        Assert.Equal(expected, normalized);
    }
}
