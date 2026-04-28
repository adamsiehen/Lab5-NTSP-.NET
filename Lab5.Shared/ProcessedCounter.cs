using System.Threading;

namespace Lab5.Shared;

/// <summary>
/// Prosty, bezpieczny licznik liczby przetworzonych kandydatów.
/// </summary>
public sealed class ProcessedCounter
{
    private long _value;

    public long Increment() => Interlocked.Increment(ref _value);

    public long Value => Interlocked.Read(ref _value);
}
