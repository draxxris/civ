using Civ.Client.Terrain;
using Xunit;

namespace Civ.Mods.Tests;

public sealed class FoliageGradeTests
{
    [Fact]
    public void Vivid_leaves_become_muted_greens_while_pale_needles_and_cutout_alpha_stay_unchanged()
    {
        // Source leaf atlas colours: birch yellow, maple red, bush lime; then a snowy needle and a grey stem.
        byte[] pixels =
        [
            230, 197, 0, 255, 204, 87, 62, 200, 124, 205, 0, 0,
            188, 202, 175, 255, 120, 118, 116, 90,
        ];
        var graded = pixels.ToArray();
        FoliageGrade.Apply(graded);

        for (int i = 0; i < 3; i++)
        {
            var (r, g, b) = (graded[i * 4], graded[i * 4 + 1], graded[i * 4 + 2]);
            double saturation = (Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b))) / (double)Math.Max(r, Math.Max(g, b));
            Assert.InRange(saturation, 0, 0.6);
            Assert.True(g >= r * 0.9 && g > b, $"pixel {i} graded to ({r}, {g}, {b}) is not an olive or green");
        }
        Assert.Equal(pixels[12..], graded[12..]);
        for (int i = 3; i < pixels.Length; i += 4) Assert.Equal(pixels[i], graded[i]);
    }
}
