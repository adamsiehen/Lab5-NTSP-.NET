using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using Lab5.Shared;

namespace Lab5.Gui;

/// <summary>
/// Odpowiada za uruchomienie procesu workera i komunikację z nim przez Named Pipes.
/// GUI nie zna szczegółów algorytmu — tylko wysyła komendy i odbiera komunikaty.
/// </summary>
public sealed class WorkerClient : IAsyncDisposable
{
    private Process? _process;
    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _listenCts;
    private Task? _listenTask;

    public bool IsRunning { get; private set; }

    public async Task StartAsync(string mode, RunSettings settings, Func<WorkerMessage, Task> onMessage, CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Worker już działa.");
        }

        var pipeName = $"lab5_ntsp_{Guid.NewGuid():N}";
        var root = ProjectPaths.FindSolutionRoot();
        var projectName = mode.Equals("ThreadPool", StringComparison.OrdinalIgnoreCase)
            ? "Lab5.Worker.ThreadPool"
            : "Lab5.Worker.Tpl";

        var projectPath = Path.Combine(root, projectName, $"{projectName}.csproj");
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" -- {pipeName}",
            WorkingDirectory = root,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        _process = Process.Start(psi) ?? throw new InvalidOperationException("Nie udało się uruchomić procesu worker.");
        _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await _pipe.ConnectAsync(10000, cancellationToken);

        _reader = new StreamReader(_pipe);
        _writer = new StreamWriter(_pipe) { AutoFlush = true };
        _listenCts = new CancellationTokenSource();
        _listenTask = ListenAsync(onMessage, _listenCts.Token);

        await SendCommandAsync(new WorkerCommand("start", settings));
        IsRunning = true;
    }

    public Task PauseAsync() => SendCommandAsync(new WorkerCommand("pause"));
    public Task ResumeAsync() => SendCommandAsync(new WorkerCommand("resume"));

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        await SendCommandAsync(new WorkerCommand("stop"));
    }

    private async Task ListenAsync(Func<WorkerMessage, Task> onMessage, CancellationToken token)
    {
        if (_reader is null)
        {
            return;
        }

        while (!token.IsCancellationRequested)
        {
            var line = await _reader.ReadLineAsync(token);
            if (line is null)
            {
                break;
            }

            WorkerMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<WorkerMessage>(line, IpcContracts.JsonOptions);
            }
            catch
            {
                continue;
            }

            if (message is null)
            {
                continue;
            }

            await onMessage(message);

            if (message.Type.Equals("completed", StringComparison.OrdinalIgnoreCase) ||
                message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
            {
                IsRunning = false;
            }
        }

        IsRunning = false;
    }

    private async Task SendCommandAsync(WorkerCommand command)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Brak połączenia z workerem.");
        }

        var payload = JsonSerializer.Serialize(command, IpcContracts.JsonOptions);
        await _writer.WriteLineAsync(payload);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _listenCts?.Cancel();
            if (_listenTask is not null)
            {
                await _listenTask;
            }
        }
        catch
        {
        }

        _writer?.Dispose();
        _reader?.Dispose();
        _pipe?.Dispose();

        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(true);
                }
            }
            catch
            {
            }
            finally
            {
                _process.Dispose();
            }
        }

        IsRunning = false;
    }
}