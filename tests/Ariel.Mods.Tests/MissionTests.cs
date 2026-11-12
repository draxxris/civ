using Ariel.Client;
using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Mods.Tests;

/// <summary>The base game's mission support, driven by a mission a probe mod registers: the shared checks, the roll,
/// the unit's fate, the common event and the panel offer.</summary>
public sealed class MissionTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    private const string Survey = "mission_probe:survey";

    private Simulation Scenario(int? chance, MissionUnitFate fate, out Unit unit, out City city)
    {
        var probe = new ModSource
        {
            Root = mods.Root, Manifest = new() { Id = "mission_probe", Version = "1", Dependencies = ["military"] },
            Instance = new Probe(chance, fate),
        };
        var sim = GameFactory.Scenario(ModLoader.Build(mods.Sources.Append(probe)));
        city = new City { Id = sim.State.NextCityId++, Owner = 0, Position = sim.State.Grid.HexOf(new(40)), Population = 2 };
        sim.State.AddCity(city);
        unit = new Unit { Id = sim.State.NextUnitId++, Owner = 0, Type = "military:warrior", Position = city.Position, MovesLeft = 60 };
        sim.State.AddUnit(unit);
        return sim;
    }

    private static PerformMissionCommand Order(Unit unit, string subject = "north", int? player = null) =>
        new() { Player = player ?? unit.Owner, Unit = unit.Id, Mission = Survey, Subject = subject };

    [Fact]
    public void The_base_game_checks_the_unit_before_the_mission_is_asked()
    {
        var sim = Scenario(null, MissionUnitFate.Spent, out var unit, out var city);
        Assert.Null(Order(unit).Validate(sim));
        Assert.Equal("Not your unit", Order(unit, player: 1).Validate(sim));
        Assert.Equal("Unknown mission", new PerformMissionCommand { Player = 0, Unit = unit.Id, Mission = "mission_probe:none" }.Validate(sim));
        Assert.Equal("The survey has no south", Order(unit, "south").Validate(sim));
        unit.MovesLeft = 0;
        Assert.Equal("No moves left", Order(unit).Validate(sim));
        unit.MovesLeft = 60;
        sim.State.MoveUnit(unit.Id, sim.State.Grid.HexOf(new(41)));
        Assert.Equal("Unit must be in a city", Order(unit).Validate(sim));
        Assert.Null(sim.Rules().Missions.Terms(sim, 0, unit.Id, Survey, "north"));
        Assert.Equal(0, city.Population - 2);
    }

    [Theory]
    [InlineData(MissionUnitFate.Spent, true, false)]
    [InlineData(MissionUnitFate.Spent, false, false)]
    [InlineData(MissionUnitFate.SpentOnFailure, true, true)]
    [InlineData(MissionUnitFate.SpentOnFailure, false, false)]
    [InlineData(MissionUnitFate.Kept, false, true)]
    public void A_mission_rolls_once_resolves_and_settles_the_unit_by_its_fate(MissionUnitFate fate, bool succeed, bool survives)
    {
        var sim = Scenario(succeed ? 100 : 0, fate, out var unit, out var city);
        var rng = sim.Rng.State;
        Assert.True(sim.TryApply(Order(unit), out var error), error);
        Assert.NotEqual(rng, sim.Rng.State);
        Assert.Equal(succeed ? 3 : 2, city.Population);
        Assert.Equal(survives, sim.State.FindUnit(unit.Id) is not null);
        var resolved = Assert.Single(sim.Events.Published.OfType<MissionResolved>());
        Assert.Equal(new MissionResolved(Survey, "north", unit.Id, city.Id, 0, succeed), resolved);
    }

    [Fact]
    public void A_mission_without_a_chance_always_succeeds_and_rolls_nothing()
    {
        var sim = Scenario(null, MissionUnitFate.Spent, out var unit, out var city);
        var rng = sim.Rng.State;
        Assert.True(sim.TryApply(Order(unit), out var error), error);
        Assert.Equal(rng, sim.Rng.State);
        Assert.Equal(3, city.Population);
    }

    [Fact]
    public void The_unit_panel_offers_each_legal_subject_with_its_terms()
    {
        var sim = Scenario(40, MissionUnitFate.Spent, out var unit, out _);
        var session = new GameSession(sim);
        var offered = session.DescribeUnit(unit)!.Actions.Where(a => a.Id.StartsWith(Survey, StringComparison.Ordinal)).ToList();
        var action = Assert.Single(offered);
        Assert.Equal(Survey + ":north", action.Id);
        Assert.Equal("Survey north", action.Label);
        Assert.Equal("5 gold, 40% chance", action.Tooltip);
    }

    private sealed class Probe(int? chance, MissionUnitFate fate) : IMod
    {
        public void Register(IModContext context) => context.RegisterService<IMission>("survey", new SurveyMission(chance, fate));
    }

    /// <summary>Grows the city on success. Only "north" is legal; "south" is listed but rejected.</summary>
    private sealed class SurveyMission(int? chance, MissionUnitFate fate) : IMission
    {
        public string Id => Survey;
        public IEnumerable<string> Subjects(Simulation sim, Unit unit, City city) => ["north", "south"];
        public string Label(Simulation sim, string subject) => "Survey " + subject;
        public string? Validate(Simulation sim, MissionOrder order) => order.Subject == "north" ? null : "The survey has no " + order.Subject;
        public MissionTerms Terms(Simulation sim, MissionOrder order) => new(chance, "5 gold", fate);
        public void Resolve(Simulation sim, MissionOrder order, bool success)
        {
            if (success) order.City.Population++;
        }
    }
}
