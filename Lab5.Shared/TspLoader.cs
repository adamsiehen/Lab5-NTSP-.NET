using System.Globalization;
using System.IO;

namespace Lab5.Shared;

/// <summary>
/// Wczytuje plik TSP i zwraca ograniczoną liczbę miast z sekcji NODE_COORD_SECTION.
/// </summary>
public static class TspLoader
{
    public static IReadOnlyList<City> Load(string path, int maxCities)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Nie znaleziono pliku danych: {path}");
        }

        var result = new List<City>(Math.Max(4, maxCities));
        var inSection = false;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (!inSection)
            {
                if (line.StartsWith("NODE_COORD_SECTION", StringComparison.OrdinalIgnoreCase))
                {
                    inSection = true;
                }
                continue;
            }

            if (line.StartsWith("EOF", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                continue;
            }

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                continue;
            }

            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
            {
                continue;
            }

            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                continue;
            }

            result.Add(new City(id, x, y));
            if (result.Count >= maxCities)
            {
                break;
            }
        }

        if (result.Count < 4)
        {
            throw new InvalidOperationException("Za mało miast w danych wejściowych.");
        }

        return result;
    }
}