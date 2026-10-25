using Xunit;

namespace Civ.Tests;

public class HexTests
{
    [Fact]
    public void PixelRoundTrip()
    {
        foreach (var h in Hex.Range(new Hex(0, 0), 6))
        {
            var (x, y) = h.ToPixel(10);
            Assert.Equal(h, Hex.FromPixel(x, y, 10));
        }
    }
}
