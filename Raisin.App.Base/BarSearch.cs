namespace Raisin.App.Base;

public static class BarSearch
{
    public static int LowerBound<T>(IReadOnlyList<T> items, DateTime target, Func<T, DateTime> selector)
    {
        int lo = 0, hi = items.Count;
        while (lo < hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (selector(items[mid]) < target)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }

    public static int LastLe<T>(IReadOnlyList<T> items, DateTime time, Func<T, DateTime> selector)
    {
        if (items.Count == 0) return -1;
        int lo = LowerBound(items, time, selector);
        if (lo < items.Count && selector(items[lo]) == time)
        {
            while (lo + 1 < items.Count && selector(items[lo + 1]) == time)
                lo++;
            return lo;
        }
        return lo - 1;
    }

    public static int Closest<T>(IReadOnlyList<T> items, DateTime target, Func<T, DateTime> selector)
    {
        if (items.Count == 0) return 0;
        int lo = LowerBound(items, target, selector);
        if (lo >= items.Count) return items.Count - 1;
        if (lo == 0) return 0;
        long diffLo = Math.Abs(selector(items[lo]).Ticks - target.Ticks);
        long diffPrev = Math.Abs(selector(items[lo - 1]).Ticks - target.Ticks);
        return diffPrev <= diffLo ? lo - 1 : lo;
    }
}
