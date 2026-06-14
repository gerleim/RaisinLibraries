namespace Raisin.Core;

public static class DoubleExtensions
{
    public const double DefaultEpsilon = 1e-7;

    public static bool ApproxEquals(this double a, double b, double epsilon = DefaultEpsilon) =>
        Math.Abs(a - b) < epsilon;
}
