using System.Threading;

namespace Lab5.Shared;

/// <summary>
/// Wspólny magazyn najlepszego rozwiązania w obrębie procesu.
/// Używa ReaderWriterLockSlim, aby spełnić wymaganie czytelnicy/pisarze.
/// </summary>
public sealed class GlobalBestStore
{
    private readonly ReaderWriterLockSlim _rw = new(LockRecursionPolicy.NoRecursion);
    private RouteCandidate? _best;

    public RouteCandidate? Read()
    {
        _rw.EnterReadLock();
        try
        {
            return _best is null ? null : new RouteCandidate(RouteMath.Clone(_best.Order), _best.Length, _best.ProducerId);
        }
        finally
        {
            _rw.ExitReadLock();
        }
    }

    public bool TryUpdate(RouteCandidate candidate)
    {
        _rw.EnterUpgradeableReadLock();
        try
        {
            if (_best is not null && _best.Length <= candidate.Length)
            {
                return false;
            }

            _rw.EnterWriteLock();
            try
            {
                _best = new RouteCandidate(RouteMath.Clone(candidate.Order), candidate.Length, candidate.ProducerId);
                return true;
            }
            finally
            {
                _rw.ExitWriteLock();
            }
        }
        finally
        {
            _rw.ExitUpgradeableReadLock();
        }
    }
}