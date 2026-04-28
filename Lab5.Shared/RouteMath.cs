namespace Lab5.Shared;

public static class RouteMath
{
    public static int[] RandomPermutation(int length, Random random)
    {
        var arr = Enumerable.Range(0, length).ToArray();
        for (var i = arr.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return arr;
    }

    public static double CycleLength(int[] route, IReadOnlyList<City> cities)
    {
        var sum = 0d;
        for (var i = 0; i < route.Length; i++)
        {
            var a = cities[route[i]];
            var b = cities[route[(i + 1) % route.Length]];
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            sum += Math.Sqrt(dx * dx + dy * dy);
        }
        return sum;
    }

    public static int[] Clone(int[] route)
    {
        var copy = new int[route.Length];
        Array.Copy(route, copy, route.Length);
        return copy;
    }

    public static void ReverseSlice(int[] route, int left, int right)
    {
        while (left < right)
        {
            (route[left], route[right]) = (route[right], route[left]);
            left++;
            right--;
        }
    }
}