using Ariel.Composition;
using Ariel.Content;
using Ariel.Game;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The base game's one store of timed modifier effects, on the base modules alone.</summary>
public sealed class TimedEffectsTests
{
    private static readonly Lazy<ContentDb> Content = new(() =>
        GameContent.Build(Path.Combine(AppContext.BaseDirectory, GameContent.RulesFolder), []));

    private static Simulation Scenario()
    {
        var sim = GameFactory.Scenario(Content.Value);
        GameFactory.NeutralPolitics(sim);
        return sim;
    }
    private static City AddCity(Simulation sim, int owner, int tile)
    {
        var city = new City { Id = sim.State.NextCityId++, Owner = owner, Position = sim.State.Grid.HexOf(new TileId(tile)), Population = 3 };
        sim.State.AddCity(city);
        return city;
    }
    private static ModifierEffect Happiness(long amount) => new() { Scope = ModifierScope.City, Key = "cities:happiness", Amount = amount };
    private static Ledger CityLedger(Simulation sim, City city) => ModifierSink.Collect(new(sim, ModifierScope.City, city.Owner, city)).Ledger;

    [Fact]
    public void Effects_apply_to_their_holder_by_scope_until_the_expiry_turn()
    {
        var sim = Scenario();
        var store = sim.Rules().TimedEffects;
        var home = AddCity(sim, 0, 14); var other = AddCity(sim, 0, 20); var rival = AddCity(sim, 1, 57);
        int turn = sim.State.Turn;
        var held = store.Add(sim, new TimedEffect { Source = "probe:city", City = home.Id, ExpiresTurn = turn + 2, Effects = [Happiness(2)] });
        store.Add(sim, new TimedEffect { Source = "probe:empire", Player = 0, Effects = [Happiness(1)] });
        store.Add(sim, new TimedEffect
        {
            Source = "probe:plot", Plot = 15, ExpiresTurn = turn + 1,
            Effects = [new() { Scope = ModifierScope.Plot, Key = "yield.flat.food", Amount = -1 }, Happiness(5)],
        });

        Assert.Equal(1, held.Id);
        Assert.Equal(3, CityLedger(sim, home).Total("cities:happiness"));
        Assert.Equal(2, Assert.Single(CityLedger(sim, home).Lines, l => l.Source == "probe:city").Amount);
        Assert.Equal(1, CityLedger(sim, other).Total("cities:happiness"));
        Assert.Equal(0, CityLedger(sim, rival).Total("cities:happiness"));
        Assert.Equal(-1, ModifierSink.Collect(new(sim, ModifierScope.Plot, 0, Plot: new TileId(15))).FlatYield.Food);
        Assert.Equal(2, held.TurnsRemaining(turn));

        sim.State.Turn = turn + 1;
        Assert.Equal(0, ModifierSink.Collect(new(sim, ModifierScope.Plot, 0, Plot: new TileId(15))).FlatYield.Food);
        Assert.Equal(3, CityLedger(sim, home).Total("cities:happiness"));
        sim.State.Turn = turn + 2;
        Assert.Equal(1, CityLedger(sim, home).Total("cities:happiness"));
        Assert.Equal(["probe:empire"], store.Active(sim).Select(e => e.Source));
        Assert.Null(store.Find(sim, "probe:city", city: home.Id));
        Assert.NotNull(store.Find(sim, held.Id)); // inactive, but kept until the expiry system runs

        sim.Content.TurnSystems.Get("cities:timed_effects").Run(sim);
        Assert.Null(store.Find(sim, held.Id));
        Assert.Single(sim.State.Extensions.Get<TimedEffectState>(sim.Content)!.Effects);
    }

    [Fact]
    public void Set_replaces_the_same_source_and_holder_and_remove_takes_one_by_id()
    {
        var sim = Scenario();
        var store = sim.Rules().TimedEffects;
        var city = AddCity(sim, 0, 14);
        store.Set(sim, new TimedEffect { Source = "probe:unrest", City = city.Id, ExpiresTurn = 5, Effects = [Happiness(-1)] });
        var other = store.Set(sim, new TimedEffect { Source = "probe:other", City = city.Id, Effects = [Happiness(1)] });
        var replaced = store.Set(sim, new TimedEffect { Source = "probe:unrest", City = city.Id, ExpiresTurn = 9, Effects = [Happiness(-4)] });
        Assert.Equal(3, replaced.Id);
        Assert.Equal([other.Id, replaced.Id], store.Active(sim).Select(e => e.Id));
        Assert.Equal(-3, CityLedger(sim, city).Total("cities:happiness"));
        Assert.True(store.Remove(sim, other.Id));
        Assert.False(store.Remove(sim, other.Id));
        Assert.Equal(-4, CityLedger(sim, city).Total("cities:happiness"));
    }

    [Fact]
    public void A_city_effect_ends_when_the_city_is_captured_or_razed()
    {
        var sim = Scenario();
        var store = sim.Rules().TimedEffects;
        var city = AddCity(sim, 0, 14); var kept = AddCity(sim, 0, 20);
        store.Add(sim, new TimedEffect { Source = "probe:city", City = city.Id, Effects = [Happiness(2)] });
        store.Add(sim, new TimedEffect { Source = "probe:empire", Player = 0, Effects = [Happiness(1)] });
        store.Add(sim, new TimedEffect { Source = "probe:kept", City = kept.Id, Effects = [Happiness(1)] });
        sim.State.SetWar(0, 1, true);
        sim.Rules().CityCapture.Capture(sim, city.Id, 1);
        Assert.Equal(["probe:empire", "probe:kept"], store.Active(sim).Select(e => e.Source));
        Assert.Equal(0, CityLedger(sim, sim.State.CityById(city.Id)).Total("cities:happiness"));
        sim.Events.Publish(new CityRazed(kept.Id, 0, kept.Position));
        Assert.Equal(["probe:empire"], store.Active(sim).Select(e => e.Source));
    }

    [Fact]
    public void Saved_effects_reload_with_identical_hashes_and_keep_applying()
    {
        var sim = Scenario();
        var store = sim.Rules().TimedEffects;
        var city = AddCity(sim, 0, 14);
        store.Add(sim, new TimedEffect { Source = "probe:city", Player = 0, City = city.Id, Plot = 14, ExpiresTurn = sim.State.Turn + 3,
            Effects = [Happiness(2), new() { Scope = ModifierScope.Plot, Key = "yield.flat.commerce", Amount = 1 }] });
        store.Add(sim, new TimedEffect { Source = "probe:empire", Player = 0, Effects = [Happiness(1)] });

        var loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim));
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(store.Active(sim), loaded.Rules().TimedEffects.Active(loaded), new EffectComparer());
        Assert.Equal(3, CityLedger(loaded, loaded.State.CityById(city.Id)).Total("cities:happiness"));
        var next = loaded.Rules().TimedEffects.Add(loaded, new TimedEffect { Source = "probe:later", Player = 0 });
        Assert.Equal(3, next.Id);

        for (int i = 0; i < 4; i++) { sim.AdvanceTurn(); loaded = SaveGame.Load(sim.Content, SaveGame.Save(sim)); }
        Assert.Equal(sim.ComputeHash(), loaded.ComputeHash());
        Assert.Equal(["probe:empire"], loaded.Rules().TimedEffects.Active(loaded).Select(e => e.Source));
    }

    private sealed class EffectComparer : IEqualityComparer<TimedEffect>
    {
        public bool Equals(TimedEffect? a, TimedEffect? b) => a is not null && b is not null
            && (a with { Effects = [] }) == (b with { Effects = [] })
            && a.Effects.Select(e => (e.Scope, e.Key, e.Target, e.Amount)).SequenceEqual(b.Effects.Select(e => (e.Scope, e.Key, e.Target, e.Amount)));
        public int GetHashCode(TimedEffect effect) => effect.Id;
    }
}
