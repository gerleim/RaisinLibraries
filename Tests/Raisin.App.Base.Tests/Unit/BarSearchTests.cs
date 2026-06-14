using FluentAssertions;
using Raisin.App.Base;
using Xunit;

namespace Raisin.App.Base.Tests.Unit;

[Trait("Category", "Unit")]
public class BarSearchTests
{
    private record Bar(DateTime Time);
    private static readonly Func<Bar, DateTime> Sel = b => b.Time;
    private static readonly DateTime T = new(2024, 1, 2, 9, 30, 0);

    // --- LowerBound ---

    [Fact]
    public void LowerBound_empty_list_returns_zero()
    {
        BarSearch.LowerBound(Array.Empty<Bar>(), T, Sel).Should().Be(0);
    }

    [Fact]
    public void LowerBound_exact_match_returns_that_index()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(1)), new Bar(T.AddMinutes(2)) };
        BarSearch.LowerBound(bars, T.AddMinutes(1), Sel).Should().Be(1);
    }

    [Fact]
    public void LowerBound_between_bars_returns_next()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(2)), new Bar(T.AddMinutes(4)) };
        BarSearch.LowerBound(bars, T.AddMinutes(1), Sel).Should().Be(1);
    }

    [Fact]
    public void LowerBound_before_all_returns_zero()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(1)) };
        BarSearch.LowerBound(bars, T.AddMinutes(-10), Sel).Should().Be(0);
    }

    [Fact]
    public void LowerBound_after_all_returns_count()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(1)) };
        BarSearch.LowerBound(bars, T.AddMinutes(10), Sel).Should().Be(2);
    }

    [Fact]
    public void LowerBound_duplicate_timestamps_returns_first()
    {
        var bars = new[] { new Bar(T), new Bar(T), new Bar(T.AddMinutes(1)) };
        BarSearch.LowerBound(bars, T, Sel).Should().Be(0);
    }

    // --- LastLe ---

    [Fact]
    public void LastLe_empty_list_returns_minus_one()
    {
        BarSearch.LastLe(Array.Empty<Bar>(), T, Sel).Should().Be(-1);
    }

    [Fact]
    public void LastLe_exact_match_returns_that_index()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(1)), new Bar(T.AddMinutes(2)) };
        BarSearch.LastLe(bars, T.AddMinutes(1), Sel).Should().Be(1);
    }

    [Fact]
    public void LastLe_between_bars_returns_earlier()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(2)), new Bar(T.AddMinutes(4)) };
        BarSearch.LastLe(bars, T.AddMinutes(3), Sel).Should().Be(1);
    }

    [Fact]
    public void LastLe_before_all_returns_minus_one()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(1)) };
        BarSearch.LastLe(bars, T.AddMinutes(-1), Sel).Should().Be(-1);
    }

    [Fact]
    public void LastLe_after_all_returns_last_index()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(1)), new Bar(T.AddMinutes(2)) };
        BarSearch.LastLe(bars, T.AddMinutes(10), Sel).Should().Be(2);
    }

    [Fact]
    public void LastLe_duplicate_timestamps_returns_last_matching()
    {
        var bars = new[] { new Bar(T), new Bar(T), new Bar(T.AddMinutes(1)) };
        BarSearch.LastLe(bars, T, Sel).Should().Be(1);
    }

    // --- Closest ---

    [Fact]
    public void Closest_empty_list_returns_zero()
    {
        BarSearch.Closest(Array.Empty<Bar>(), T, Sel).Should().Be(0);
    }

    [Fact]
    public void Closest_single_bar_returns_zero()
    {
        var bars = new[] { new Bar(T) };
        BarSearch.Closest(bars, T.AddMinutes(5), Sel).Should().Be(0);
    }

    [Fact]
    public void Closest_exact_match_returns_that_index()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(1)), new Bar(T.AddMinutes(2)) };
        BarSearch.Closest(bars, T.AddMinutes(1), Sel).Should().Be(1);
    }

    [Fact]
    public void Closest_nearer_to_earlier_bar()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(10)) };
        BarSearch.Closest(bars, T.AddMinutes(3), Sel).Should().Be(0);
    }

    [Fact]
    public void Closest_nearer_to_later_bar()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(10)) };
        BarSearch.Closest(bars, T.AddMinutes(8), Sel).Should().Be(1);
    }

    [Fact]
    public void Closest_equidistant_returns_earlier()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(10)) };
        BarSearch.Closest(bars, T.AddMinutes(5), Sel).Should().Be(0);
    }

    [Fact]
    public void Closest_before_all_returns_first()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(1)) };
        BarSearch.Closest(bars, T.AddMinutes(-100), Sel).Should().Be(0);
    }

    [Fact]
    public void Closest_after_all_returns_last()
    {
        var bars = new[] { new Bar(T), new Bar(T.AddMinutes(1)) };
        BarSearch.Closest(bars, T.AddMinutes(100), Sel).Should().Be(1);
    }
}
