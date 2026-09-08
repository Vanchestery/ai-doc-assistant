using System.Reflection;

namespace AiDocAssistant.Core.Evals;

/// <summary>Load golden JSON from embedded resources (Phase 4).</summary>
public static class GoldenFixtures
{
    public static string Load(string fileName)
    {
        var assembly = typeof(GoldenFixtures).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith($".{fileName}", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource not found: {fileName}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
