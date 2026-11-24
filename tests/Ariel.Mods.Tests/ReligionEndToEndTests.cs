using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Mods.Religion;
using Ariel.Rules;
using Xunit;
using Xunit.Abstractions;

namespace Ariel.Mods.Tests;

/// <summary>Religion in a played game, end to end, with nothing scripted: computer players research a faith's founding
/// technology and build its founding wonder, which founds the faith in that city; missionaries carry it by mission and
/// it spreads on its own between connected cities, many times over; and it shows in the cities' effects and in the leaders' attitudes. The game then
/// continues identically from a save.</summary>
public sealed class ReligionEndToEndTests(ITestOutputHelper output)
{
    private const int MaxTurns = 260;
    /// <summary>Natural spreads the game must see: at a mean wait of 33 turns per connected faithless city, far more than
    /// the single one the distance-scaled rule used to give.</summary>
    private const int NaturalSpreads = 15;

    [Fact]
    [Trait("Category", "E2E")]
    public void Ai_players_found_spread_and_feel_faiths_and_the_game_replays_from_a_save()
    {
        var content = new LoadedMods().Content;
        var sim = GameFactory.CreateAi(content, seed: 7, players: 6, era: "foundation:era_medieval");
        var religion = sim.Service<IReligionService>()!;
        var founded = new List<ReligionFounded>();
        int missions = 0, natural = 0;
        bool effects = false, attitudes = false;
        // A turn's processing (production, natural spread) publishes into the turn that has just ended, the players'
        // commands into the one that began: read both, once each.
        var seen = new HashSet<DomainEvent>(ReferenceEqualityComparer.Instance);
        while (sim.State.Turn < MaxTurns && !(founded.Count >= 2 && missions > 0 && natural >= NaturalSpreads && effects && attitudes))
        {
            sim.AdvanceTurn();
            var events = sim.Events.PreviousTurn.Concat(sim.Events.Published).Where(seen.Add).ToArray();
            foreach (var found in events.OfType<ReligionFounded>())
            {
                // Founded by building the faith's wonder, which its founding technology made available.
                var def = content.Get<ReligionDef>(found.Religion);
                Assert.Contains(GameFactory.FoundingWonder(sim, found.Religion), sim.State.CityById(found.City).Buildings);
                Assert.True(sim.KnowsTech(found.Player, def.FoundingTech!), $"{found.Religion} founded without {def.FoundingTech}");
                founded.Add(found);
            }
            var carried = events.OfType<MissionResolved>().Where(m => m.Mission == ReligionMissions.Spread && m.Success)
                .Select(m => (m.City, m.Subject)).ToHashSet();
            var foundings = events.OfType<ReligionFounded>().Select(f => (f.City, f.Religion)).ToHashSet();
            foreach (var spread in events.OfType<ReligionSpread>())
            {
                if (carried.Contains((spread.City, spread.Religion))) missions++;
                else if (!foundings.Contains((spread.City, spread.Religion))) natural++;
            }
            // The faith's own effects (its state religion happiness, a building's state religion yields), not a temple's
            // ordinary building happiness.
            effects |= sim.State.Cities.Any(city => religion.Present(sim, city.Id).Count > 0
                && ModifierSink.Collect(new(sim, ModifierScope.City, city.Owner, city)).Ledger.Lines
                    .Any(l => l.Amount != 0 && (religion.Present(sim, city.Id).Contains(l.Source) || l.Source.EndsWith(":state_religion", StringComparison.Ordinal))));
            var diplomacy = sim.Service<IDiplomacyService>()!;
            var players = sim.State.Players.Where(p => p.Ai == "ai:standard").Select(p => p.Id).ToArray();
            attitudes |= players.Any(a => players.Any(b => a != b
                && diplomacy.GetAttitude(sim, a, b).Ledger.Lines.Any(l => l.Source is "religion:same_religion" or "religion:different_religion" && l.Amount != 0)));
        }
        output.WriteLine($"turn {sim.State.Turn}: founded {string.Join(", ", founded.Select(f => f.Religion))}; {missions} missions, {natural} natural spreads;"
            + $" state religions {string.Join(", ", sim.State.Players.Select(p => religion.StateReligion(sim, p.Id) ?? "-"))}; effects {effects}, attitudes {attitudes}");
        Assert.True(founded.Count >= 2, $"by turn {sim.State.Turn} only {founded.Count} faiths were founded");
        Assert.True(missions > 0, "no missionary carried a faith");
        Assert.True(natural >= NaturalSpreads, $"only {natural} natural spreads by turn {sim.State.Turn}");
        Assert.True(effects, "no city felt its faith");
        Assert.True(attitudes, "no leader regarded another's faith");

        var resumed = SaveGame.Load(content, SaveGame.Save(sim));
        for (int turn = 0; turn < 3; turn++) { sim.AdvanceTurn(); resumed.AdvanceTurn(); }
        Assert.Equal(sim.ComputeHash(), resumed.ComputeHash());
    }
}
