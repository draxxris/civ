using Xunit;

namespace Civ.Tests;

public class HexTests
{
    [Fact]
    public void NeighborsAreAtDistanceOne()
    {
        var h = new Hex(3, -2);
        Assert.Equal(6, h.Neighbors().Count());
        Assert.All(h.Neighbors(), n => Assert.Equal(1, h.DistanceTo(n)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 7)]
    [InlineData(2, 19)]
    [InlineData(5, 91)]
    public void RangeSize(int radius, int expected) =>
        Assert.Equal(expected, Hex.Range(new Hex(2, 2), radius).Count());

    [Fact]
    public void RingHasSixRHexesAllAtDistanceR()
    {
        var ring = Hex.Ring(new Hex(1, 1), 3).ToList();
        Assert.Equal(18, ring.Count);
        Assert.All(ring, h => Assert.Equal(3, h.DistanceTo(new Hex(1, 1))));
        Assert.Equal(18, ring.Distinct().Count());
    }

    [Fact]
    public void PixelRoundTrip()
    {
        foreach (var h in Hex.Range(new Hex(0, 0), 6))
        {
            var (x, y) = h.ToPixel(10);
            Assert.Equal(h, Hex.FromPixel(x, y, 10));
        }
    }

    [Fact]
    public void RngIsDeterministic()
    {
        var a = new DeterministicRng(42);
        var b = new DeterministicRng(42);
        for (int i = 0; i < 100; i++) Assert.Equal(a.NextUInt64(), b.NextUInt64());
    }
}
