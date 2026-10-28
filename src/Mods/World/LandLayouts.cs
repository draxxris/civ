namespace Ariel.Mods.World;

internal enum WorldLayout { Continental, InlandSea, KingOfTheHill, NewWorld, TwinContinents, GreatPlains }

/// <summary>Integer-only land silhouettes. All layouts share climate, relief and placement rules.</summary>
internal static class LandLayouts
{
    internal static bool[] Create(HexGrid grid, int[] elevation, int waterPercent, WorldLayout layout)
    {
        var land = new bool[grid.Count];
        if (layout == WorldLayout.Continental)
        {
            foreach (var tile in grid.Tiles.OrderByDescending(t => elevation[t.Value]).ThenBy(t => t.Value)
                .Take(grid.Count * (100 - waterPercent) / 100)) land[tile.Value] = true;
            return land;
        }
        foreach (var tile in grid.Tiles)
        {
            var (col, row) = grid.OffsetOf(tile);
            // Normalize each axis independently: circles become ellipses on rectangular maps.
            int x = Coordinate(col, grid.Spec.Width), y = Coordinate(row, grid.Spec.Height);
            int noise = Math.Clamp(elevation[tile.Value] - 500, -500, 500);
            land[tile.Value] = layout switch
            {
                WorldLayout.InlandSea => InlandSea(x, y, noise),
                WorldLayout.KingOfTheHill => Wheel(x, y, noise),
                WorldLayout.NewWorld => Ellipse(x + 500, y, 390 + noise / 20, 880)
                    || Ellipse(x - 540, y, 290 + noise / 20, 760),
                WorldLayout.TwinContinents => Ellipse(Math.Abs(x) - 500, y, 370 + noise / 20, 860),
                WorldLayout.GreatPlains => Plains(x, y, noise),
                _ => throw new ArgumentOutOfRangeException(nameof(layout)),
            };
        }
        return land;
    }

    private static int Coordinate(int value, int length) =>
        length == 1 ? 0 : (int)(2000L * value / (length - 1) - 1000);

    private static bool Ellipse(int x, int y, int width, int height) =>
        (long)x * x * height * height + (long)y * y * width * width <= (long)width * width * height * height;

    private static bool Plains(int x, int y, int noise)
    {
        int shore = 100 + noise / 25;
        return !Ellipse(x + 500, y + 300, shore, 140)
            && !Ellipse(x - 400, y - 300, shore, 120)
            && !Ellipse(x - 100, y + 550, shore, 90);
    }

    private static bool InlandSea(int x, int y, int noise)
    {
        int shore = 650 + noise / 8;
        return x * x + y * y >= shore * shore;
    }

    private static bool Wheel(int x, int y, int noise)
    {
        int distanceSquared = x * x + y * y;
        int shore = 930 + noise / 20;
        if (distanceSquared > shore * shore) return false;
        // A solid hub and outer rim connect eight broad spokes, with water between them.
        if (distanceSquared <= 230 * 230 || distanceSquared >= 780 * 780) return true;
        return Math.Abs(x) <= 75 || Math.Abs(y) <= 75 || Math.Abs(Math.Abs(x) - Math.Abs(y)) <= 100;
    }
}
