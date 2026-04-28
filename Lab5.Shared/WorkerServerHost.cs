using System.IO;
using System.IO.Pipes;
using System.Text.Json;

namespace Lab5.Shared;

/// <summary>
/// Wspólny host workera: odbiera komendy z GUI przez Named Pipe
/// i uruchamia albo zatrzymuje właściwy silnik obliczeniowy.
/// </summary>
public static class WorkerServerHost
{
    public static async Task RunAsync(
        string[] args,
        string usageText,
        Func<RunSettings, ManualResetEventSlim, CancellationToken, Func<WorkerMessage, Task>, Task> runEngineAsync)
    {
        if (args.Length == 0)
        {
            Console.WriteLine(usageText);
            return;
        }

        var pipeName = args[0];
        using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await pipe.WaitForConnectionAsync();

        using var reader = new StreamReader(pipe);
        using var writer = new StreamWriter(pipe) { AutoFlush = true };
        var writeLock = new SemaphoreSlim(1, 1);
        var pauseGate = new ManualResetEventSlim(true);

        CancellationTokenSource? runCancellation = null;
        Task? runningTask = null;

        while (pipe.IsConnected)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            WorkerCommand? command;
            try
            {
                command = JsonSerializer.Deserialize<WorkerCommand>(line, IpcContracts.JsonOptions);
            }
            catch
            {
                continue;
            }

            if (command is null)
            {
                continue;
            }

            switch (command.Type.ToLowerInvariant())
            {
                case "start":
                    if (command.Settings is null || runningTask is not null)
                    {
                        break;
                    }

                    runCancellation = new CancellationTokenSource();
                    var settings = command.Settings;
                    runningTask = Task.Run(() => runEngineAsync(settings, pauseGate, runCancellation.Token, SendAsync));
                    _ = runningTask.ContinueWith(async task =>
                    {
                        if (task.IsFaulted)
                        {
                            await SendAsync(new WorkerMessage("error", Error: task.Exception?.GetBaseException().Message));
                        }

                        await SendAsync(new WorkerMessage("completed"));
                        runningTask = null;
                        runCancellation?.Dispose();
                        runCancellation = null;
                        pauseGate.Set();
                    }, TaskScheduler.Default);
                    break;

                case "pause":
                    pauseGate.Reset();
                    break;

                case "resume":
                    pauseGate.Set();
                    break;

                case "stop":
                    runCancellation?.Cancel();
                    pauseGate.Set();
                    break;
            }
        }

        async Task SendAsync(WorkerMessage message)
        {
            var payload = JsonSerializer.Serialize(message, IpcContracts.JsonOptions);
            await writeLock.WaitAsync();
            try
            {
                await writer.WriteLineAsync(payload);
            }
            finally
            {
                writeLock.Release();
            }
        }
    }
}