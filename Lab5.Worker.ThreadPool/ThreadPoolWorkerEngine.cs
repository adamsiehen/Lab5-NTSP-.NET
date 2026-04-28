using System.Collections.Concurrent;
using System.Diagnostics;
using Lab5.Shared;

namespace Lab5.Worker.ThreadPool;

internal static class ThreadPoolWorkerEngine
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

    private static Task<List<RouteCandidate>> RunPmxPhaseAsync(
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
        var completion = new TaskCompletionSource<List<RouteCandidate>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workerCount = Math.Max(1, settings.Workers);
        var candidates = new ConcurrentBag<RouteCandidate>();
        var barrier = new Barrier(workerCount);
        var finishedWorkers = new CountdownEvent(workerCount);
        var stopwatch = Stopwatch.StartNew();

        for (var workerId = 0; workerId < workerCount; workerId++)
        {
            var localWorkerId = workerId;
            System.Threading.ThreadPool.QueueUserWorkItem(async _ =>
            {
                try
                {
                    var random = new Random(Environment.TickCount + localWorkerId * 997 + epoch);
                    while (stopwatch.Elapsed < TimeSpan.FromSeconds(settings.PmxSeconds) && !token.IsCancellationRequested)
                    {
                        pauseGate.Wait(token);
                        var parentA = population[random.Next(population.Count)].Order;
                        var parentB = population[random.Next(population.Count)].Order;
                        var (childA, childB) = GeneticOperators.Pmx(parentA, parentB, random);
                        var lengthA = RouteMath.CycleLength(childA, cities);
                        var lengthB = RouteMath.CycleLength(childB, cities);
                        var candidate = lengthA <= lengthB
                            ? new RouteCandidate(childA, lengthA, localWorkerId)
                            : new RouteCandidate(childB, lengthB, localWorkerId);

                        candidates.Add(candidate);
                        var processedNow = processed.Increment();

                        if (globalBest.TryUpdate(candidate))
                        {
                            await send(new WorkerMessage("best", "pmx", epoch, processedNow, candidate.Length, localWorkerId, RouteMath.Clone(candidate.Order)));
                        }
                    }

                    barrier.SignalAndWait(token);
                }
                finally
                {
                    finishedWorkers.Signal();
                }
            });
        }

        System.Threading.ThreadPool.QueueUserWorkItem(async _ =>
        {
            try
            {
                finishedWorkers.Wait(token);
                barrier.Dispose();
                await WorkerEngineSupport.PublishBestAsync(globalBest, "pmx", epoch, processed.Value, send);
                completion.TrySetResult(WorkerEngineSupport.SelectBestCandidates(candidates, settings.Workers));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                finishedWorkers.Dispose();
            }
        });

        return completion.Task;
    }

    private static Task<List<RouteCandidate>> RunThreeOptPhaseAsync(
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
        var completion = new TaskCompletionSource<List<RouteCandidate>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workerCount = Math.Max(1, settings.Workers);
        var candidates = new ConcurrentBag<RouteCandidate>();
        var barrier = new Barrier(workerCount);
        var finishedWorkers = new CountdownEvent(workerCount);
        var stopwatch = Stopwatch.StartNew();

        for (var workerId = 0; workerId < workerCount; workerId++)
        {
            var localWorkerId = workerId;
            System.Threading.ThreadPool.QueueUserWorkItem(async _ =>
            {
                try
                {
                    var random = new Random(Environment.TickCount + localWorkerId * 137 + epoch * 23);
                    while (stopwatch.Elapsed < TimeSpan.FromSeconds(settings.OptSeconds) && !token.IsCancellationRequested)
                    {
                        pauseGate.Wait(token);
                        var source = population[random.Next(population.Count)].Order;
                        var improved = GeneticOperators.ThreeOptImproveLimited(source, cities, 150);
                        var improvedLength = RouteMath.CycleLength(improved, cities);
                        var candidate = new RouteCandidate(improved, improvedLength, localWorkerId);
                        candidates.Add(candidate);
                        var processedNow = processed.Increment();

                        if (globalBest.TryUpdate(candidate))
                        {
                            await send(new WorkerMessage("best", "3opt", epoch, processedNow, candidate.Length, localWorkerId, RouteMath.Clone(candidate.Order)));
                        }
                    }

                    barrier.SignalAndWait(token);
                }
                finally
                {
                    finishedWorkers.Signal();
                }
            });
        }

        System.Threading.ThreadPool.QueueUserWorkItem(async _ =>
        {
            try
            {
                finishedWorkers.Wait(token);
                barrier.Dispose();
                await WorkerEngineSupport.PublishBestAsync(globalBest, "3opt", epoch, processed.Value, send);
                completion.TrySetResult(WorkerEngineSupport.SelectBestCandidates(candidates, settings.Workers));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                finishedWorkers.Dispose();
            }
        });

        return completion.Task;
    }
}