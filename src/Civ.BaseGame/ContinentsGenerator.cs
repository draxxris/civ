using Civ.Content;

namespace Civ.BaseGame;

public sealed class ContinentsGenerator : IMapGenerator
{
    public string Id => "base:continents";

    public IReadOnlyDictionary<Hex, string> Generate(int radius, DeterministicRng rng, ContentDb content)
    {
        var map = new Dictionary<Hex, string>();
        foreach (var hex in Hex.Range(new Hex(0, 0), radius))
        {
            int v = rng.Next(100) - hex.DistanceTo(new Hex(0, 0)) * 70 / Math.Max(1, radius);
            map[hex] = v switch
            {
                < 0 => "base:ocean",
                >= 90 => "base:mountains",
                >= 75 => "base:hills",
                >= 45 => "base:grassland",
                >= 20 => "base:plains",
                _ => "base:desert",
            };
        }
        return map;
    }
}
