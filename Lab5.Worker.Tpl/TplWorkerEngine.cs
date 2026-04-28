using System.Diagnostics;
using Lab5.Shared;

namespace Lab5.Worker.Tpl;

internal static class TplWorkerEngine
{
    public static async Task RunAsync(
        RunSettings settings,
        ManualResetEventSlim pauseGate,
        CancellationToken token,
        Func<WorkerMessage, Task> send)
    {
        var cities = TspLoader.Load(settings.DataFilePath, settings.CityCount);
        var globalBest = new GlobalBestStore();
        var processed = new ProcessedCounter();
        var population = WorkerEngineSupport.CreateInitialPopulation(settings, cities, globalBest);
        var populationSize = Math.Max(4, settings.Workers * 2);

        await WorkerEngineSupport.PublishBestAsync(globalBest, "init", 0, processed.Value, send);

        for (var epoch = 1; epoch <= settings.Epochs; epoch++)
        {
            token.ThrowIfCancellationRequested();
            pauseGate.Wait(token);

            var pmxCandidates = await RunPmxPhaseAsync(settings, cities, population, globalBest, pauseGate, token, epoch, processed, send);
            population = pmxCandidates.OrderBy(candidate => candidate.Length).Take(populationSize).ToList();

            var optCandidates = await RunThreeOptPhaseAsync(settings, cities, population, globalBest, pauseGate, token, epoch, processed, send);
            population = optCandidates.OrderBy(candidate => candidate.Length).Take(populationSize).ToList();
        }
    }

    private static async Task<List<RouteCandidate>> RunPmxPhaseAsync(
        RunSettings settings,
        IReadOnlyList<City> cities,
        List<RouteCandidate> population,
        GlobalBestStore globalBest,
        ManualResetEventSlim pauseGate,
        CancellationToken token,
        int epoch,
        ProcessedCounter processed,
        Func<WorkerMessage, Task> send)
    {
        var stopwatch = Stopwatch.StartNew();
        var workerCount = Math.Max(1, settings.Workers);
        var barrier = new Barrier(workerCount);
        var candidateBuckets = new List<RouteCandidate>[workerCount];

        var tasks = Enumerable.Range(0, workerCount).Select(workerId => Task.Run(async () =>
        {
            var random = new Random(Environment.TickCount + workerId * 997 + epoch);
            candidateBuckets[workerId] = new List<RouteCandidate>();

            while (stopwatch.Elapsed < TimeSpan.FromSeconds(settings.PmxSeconds) && !token.IsCancellationRequested)
            {
                pauseGate.Wait(token);

                var parentA = population[random.Next(population.Count)].Order;
                var parentB = population[random.Next(population.Count)].Order;
                var (childA, childB) = GeneticOperators.Pmx(parentA, parentB, random);

                var lengthA = RouteMath.CycleLength(childA, cities);
                var lengthB = RouteMath.CycleLength(childB, cities);
                var bestCandidate = lengthA <= lengthB
                    ? new RouteCandidate(childA, lengthA, workerId)
                    : new RouteCandidate(childB, lengthB, workerId);

                candidateBuckets[workerId].Add(bestCandidate);
                var processedNow = processed.Increment();

                if (globalBest.TryUpdate(bestCandidate))
                {
                    await send(new WorkerMessage("best", "pmx", epoch, processedNow, bestCandidate.Length, workerId, RouteMath.Clone(bestCandidate.Order)));
                }
            }

            barrier.SignalAndWait(token);
        }, token)).ToArray();

        await Task.WhenAll(tasks);
        barrier.Dispose();

        var allCandidates = candidateBuckets.SelectMany(bucket => bucket);
        var survivors = WorkerEngineSupport.SelectBestCandidates(allCandidates, settings.Workers);
        await WorkerEngineSupport.PublishBestAsync(globalBest, "pmx", epoch, processed.Value, send);
        return survivors;
    }

    private static async Task<List<RouteCandidate>> RunThreeOptPhaseAsync(
        RunSettings settings,
        IReadOnlyList<City> cities,
        List<RouteCandidate> population,
        GlobalBestStore globalBest,
        ManualResetEventSlim pauseGate,
        CancellationToken token,
        int epoch,
        ProcessedCounter processed,
        Func<WorkerMessage, Task> send)
    {
        var stopwatch = Stopwatch.StartNew();
        var workerCount = Math.Max(1, settings.Workers);
        var barrier = new Barrier(workerCount);
        var candidateBuckets = new List<RouteCandidate>[workerCount];

        var tasks = Enumerable.Range(0, workerCount).Select(workerId => Task.Run(async () =>
        {
            var random = new Random(Environment.TickCount + workerId * 137 + epoch * 23);
            candidateBuckets[workerId] = new List<RouteCandidate>();

            while (stopwatch.Elapsed < TimeSpan.FromSeconds(settings.OptSeconds) && !token.IsCancellationRequested)
            {
                pauseGate.Wait(token);
                var source = population[random.Next(population.Count)].Order;
                var improved = GeneticOperators.ThreeOptImproveLimited(source, cities, 150);
                var improvedLength = RouteMath.CycleLength(improved, cities);
                var candidate = new RouteCandidate(improved, improvedLength, workerId);
                candidateBuckets[workerId].Add(candidate);
                var processedNow = processed.Increment();

                if (globalBest.TryUpdate(candidate))
                {
                    await send(new WorkerMessage("best", "3opt", epoch, processedNow, improvedLength, workerId, RouteMath.Clone(improved)));
                }
            }

            barrier.SignalAndWait(token);
        }, token)).ToArray();

        await Task.WhenAll(tasks);
        barrier.Dispose();

        var allCandidates = candidateBuckets.SelectMany(bucket => bucket);
        var survivors = WorkerEngineSupport.SelectBestCandidates(allCandidates, settings.Workers);
        await WorkerEngineSupport.PublishBestAsync(globalBest, "3opt", epoch, processed.Value, send);
        return survivors;
    }
}