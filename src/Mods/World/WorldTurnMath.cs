using Civ.Game;
using S = Civ.Content.Schema;

namespace Civ.Mods.World;

internal static class WorldTurnMath
{
    // Keep intermediate products wide; clamp only at the RNG's integer boundary.
    internal static int Range(Int128 odds) => (int)Int128.Clamp(odds, 1, int.MaxValue);

    internal static int Raw(Simulation sim, string key, int fallback) =>
        sim.Content.Get<S.RuleConstantsDef>().All.Single().Raw.TryGetValue(key, out var value)
            && int.TryParse(value, out int parsed) ? parsed : fallback;
}
