namespace Lab5.Shared;

/// <summary>
/// Operatory heurystyczne używane przez algorytm NTSP:
/// - PMX do krzyżowania dwóch permutacji,
/// - uproszczony 3-opt do lokalnej poprawy cyklu.
/// </summary>
public static class GeneticOperators
{
    public static (int[] childA, int[] childB) Pmx(int[] parentA, int[] parentB, Random random)
    {
        var n = parentA.Length;
        var start = random.Next(0, n - 1);
        var end = random.Next(start + 1, n);

        var childA = BuildPmxChild(parentA, parentB, start, end);
        var childB = BuildPmxChild(parentB, parentA, start, end);
        return (childA, childB);
    }

    private static int[] BuildPmxChild(int[] source, int[] donor, int start, int end)
    {
        var n = source.Length;
        var child = Enumerable.Repeat(-1, n).ToArray();

        for (var i = start; i <= end; i++)
        {
            child[i] = source[i];
        }

        for (var i = start; i <= end; i++)
        {
            var gene = donor[i];
            if (Array.IndexOf(child, gene) >= 0)
            {
                continue;
            }

            var pos = i;
            while (true)
            {
                var mappedGene = source[pos];
                pos = Array.IndexOf(donor, mappedGene);
                if (child[pos] == -1)
                {
                    child[pos] = gene;
                    break;
                }
            }
        }

        for (var i = 0; i < n; i++)
        {
            if (child[i] == -1)
            {
                child[i] = donor[i];
            }
        }

        return child;
    }

    public static int[] ThreeOptImproveLimited(int[] route, IReadOnlyList<City> cities, int maxChecks)
    {
        var best = RouteMath.Clone(route);
        var bestLen = RouteMath.CycleLength(best, cities);
        var n = route.Length;
        var checks = 0;

        for (var i = 0; i < n - 5 && checks < maxChecks; i++)
        {
            for (var j = i + 2; j < n - 3 && checks < maxChecks; j++)
            {
                for (var k = j + 2; k < n - 1 && checks < maxChecks; k++)
                {
                    checks++;
                    EvaluateVariant(best, cities, ref bestLen, i + 1, j, reverse1: true, reverse2: false);
                    EvaluateVariant(best, cities, ref bestLen, j + 1, k, reverse1: true, reverse2: false);
                    EvaluateVariant(best, cities, ref bestLen, i + 1, k, reverse1: true, reverse2: false);
                    EvaluateVariant(best, cities, ref bestLen, i + 1, j, reverse1: true, reverse2: true, secondLeft: j + 1, secondRight: k);
                }
            }
        }

        return best;
    }

    private static void EvaluateVariant(
        int[] best,
        IReadOnlyList<City> cities,
        ref double bestLen,
        int left,
        int right,
        bool reverse1,
        bool reverse2,
        int secondLeft = -1,
        int secondRight = -1)
    {
        var candidate = RouteMath.Clone(best);
        if (reverse1)
        {
            RouteMath.ReverseSlice(candidate, left, right);
        }

        if (reverse2 && secondLeft >= 0 && secondRight > secondLeft)
        {
            RouteMath.ReverseSlice(candidate, secondLeft, secondRight);
        }

        var len = RouteMath.CycleLength(candidate, cities);
        if (len < bestLen)
        {
            Array.Copy(candidate, best, candidate.Length);
            bestLen = len;
        }
    }
}
