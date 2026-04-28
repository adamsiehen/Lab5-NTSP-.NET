using System.Text.Json;

namespace Lab5.Shared;

public static class IpcContracts
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

public sealed record WorkerCommand(string Type, RunSettings? Settings = null);

public sealed record WorkerMessage(
    string Type,
    string? Phase = null,
    int Epoch = 0,
    long Processed = 0,
    double BestLength = 0,
    int BestThreadId = -1,
    int[]? BestRoutePreview = null,
    string? Error = null
);