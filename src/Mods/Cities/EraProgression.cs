using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Cities;

internal static class EraProgression
{
    internal static void Discovered(Simulation sim, TechDiscovered discovery)
    {
        string? era = sim.Content.Get<TechDef>(discovery.Tech).Era;
        if (era is null) return;
        foreach (int member in sim.State.TeamById(discovery.Team).Members)
        {
            var player = sim.State.PlayerById(member);
            var state = player.Extensions.Get<CityPlayerState>(sim.Content) ?? new();
            var current = sim.Content.Get<EraDef>(state.CurrentEra ?? sim.Settings.StartingEra);
            if (sim.Content.Get<EraDef>(era).StartPercent <= current.StartPercent) continue;
            state.CurrentEra = era;
            player.Extensions.Set(sim.Content, state);
        }
    }
}
