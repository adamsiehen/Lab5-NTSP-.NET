using System.IO;

namespace Lab5.Shared;

/// <summary>
/// Pomocnicza klasa do odnajdywania katalogu solution i domyślnego pliku z danymi.
/// Dzięki temu GUI i worker używają tej samej logiki ścieżek.
/// </summary>
public static class ProjectPaths
{
    public static string FindSolutionRoot()
    {
        var directory = AppContext.BaseDirectory;

        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (Directory.EnumerateFiles(directory, "*.sln").Any())
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Nie znaleziono katalogu solution (.sln).");
    }

    public static string GetDefaultDataPath()
    {
        return Path.Combine(FindSolutionRoot(), "data", "contries.tsp");
    }
}