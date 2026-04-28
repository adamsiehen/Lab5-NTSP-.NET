namespace Lab5.Shared;

/// <summary>
/// Wspólne operacje dla obu wariantów workera.
/// Różnica między projektami dotyczy tylko sposobu uruchamiania faz równoległych.
/// </summary>
public static class WorkerEngineSupport
{
    public static List<RouteCandidate> CreateInitialPopulation(
        RunSettings settings,
        IReadOnlyList<City> cities,
        GlobalBestStore globalBest)
    {
        var randomSeed = Environment.TickCount;
        var populationSize = Math.Max(4, settings.Workers * 2);
        var population = new List<RouteCandidate>(populationSize);

        for (var index = 0; index < populationSize; index++)
        {
            var random = new Random(randomSeed + index * 31);
            var route = RouteMath.RandomPermutation(cities.Count, random);
            var length = RouteMath.CycleLength(route, cities);
            var candidate = new RouteCandidate(route, length, index % Math.Max(1, settings.Workers));
            population.Add(candidate);
            globalBest.TryUpdate(candidate);
        }

        return population;
    }

    public static List<RouteCandidate> SelectBestCandidates(IEnumerable<RouteCandidate> candidates, int workers)
    {
        return candidates
            .OrderBy(candidate => candidate.Length)
            .Take(Math.Max(4, workers * 2))
            .ToList();
    }

    public static Task PublishBestAsync(
        GlobalBestStore store,
        string phase,
        int epoch,
        long processed,
        Func<WorkerMessage, Task> send)
    {
        var best = store.Read();
        if (best is null)
        {
            return Task.CompletedTask;
        }

        return send(new WorkerMessage("status", phase, epoch, processed, best.Length, best.ProducerId, RouteMath.Clone(best.Order)));
    }
}