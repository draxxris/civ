using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Xunit;

namespace Ariel.Mods.Tests;

public sealed class CalendarTests(LoadedMods mods) : IClassFixture<LoadedMods>
{
    // Calendar queries own these contracts. No earlier calendar coverage exists.
    // Literal dates were checked by compiling the native date class against effective speed records.
    // This catches legacy multiplication, endpoint, source-layer and start-month regressions.
    [Theory]
    [InlineData("blitz", 10, -4500, 600)]
    [InlineData("epic", 30, -4500, 1800)]
    [InlineData("eternity", 61, -4780, 4800)]
    [InlineData("marathon", 39, -4440, 2400)]
    [InlineData("normal", 19, -4575, 1200)]
    [InlineData("quick", 15, -4500, 900)]
    [InlineData("snail", 60, -4500, 3600)]
    public void Effective_speeds_preserve_inclusive_date_endpoints_and_continue_the_tail(string speed, int boundary, long year, int end)
    {
        var sim = Scenario(mods.Content, "foundation:gamespeed_" + speed);
        var calendar = sim.Content.Services.Get<IGameCalendar>().Single();
        Assert.Equal(-6000, calendar.GetDate(sim)!.Year);
        Assert.Equal(2, calendar.GetDate(sim)!.Month);
        Assert.Equal(year, calendar.GetDate(sim, boundary)!.Year);
        Assert.Equal(2200, calendar.GetDate(sim, end)!.Year);
        Assert.Equal(2201, calendar.GetDate(sim, end + 1)!.Year);
    }

    [Theory]
    [InlineData(18, -4650, 2, "4650 BCE")]
    [InlineData(690, 1843, 2, "1843 CE")]
    [InlineData(691, 1845, 2, "Winter, 1845 CE")]
    [InlineData(692, 1845, 8, "Summer, 1845 CE")]
    [InlineData(1070, 2034, 8, "Summer, 2034 CE")]
    [InlineData(1071, 2035, 2, "2035 CE")]
    public void Subyear_dates_and_display_precision_follow_the_active_date_increment(int turn, long year, int month, string label)
    {
        var sim = Scenario(mods.Content);
        var date = sim.Content.Services.Get<IGameCalendar>().Single().GetDate(sim, turn)!;
        Assert.Equal((year, month, 1, label), (date.Year, date.Month, date.Day, date.FormattedDate));
    }

    // Lifecycle risk: offsets or queries must not become a second clock, consume RNG or change hashes.
    [Fact]
    public void Late_start_uses_total_schedule_percentage_without_mutating_turn_or_save_state()
    {
        var sim = Scenario(mods.Content, era: "foundation:era_medieval");
        sim.State.Turn = 1;
        var calendar = sim.Content.Services.Get<IGameCalendar>().Single();
        var before = SaveGame.Save(sim);
        Assert.Equal(415, calendar.GetDate(sim)!.Year); // 1200 * 28 / 100 = 336.
        Assert.Equal(before, SaveGame.Save(sim));
        sim.State.Turn = 357; // Effective turn 692: 336 + 356.
        var restored = SaveGame.Load(mods.Content, SaveGame.Save(sim));
        Assert.Equal("Summer, 1845 CE", calendar.GetDate(restored)!.FormattedDate);
        Assert.Equal(sim.ComputeHash(), restored.ComputeHash());
    }

    // Effective-content contract: a mod override must shift every date, not just the initial label.
    // Existing calendar tests use Foundation's unmodified constants and cannot catch registration-time capture.
    [Theory]
    [InlineData(-4000, -3925L, 3845L, 4201L)]
    [InlineData(0, 75L, 7845L, 8201L)]
    [InlineData(int.MinValue, -2147483573L, -2147475803L, -2147475447L)]
    [InlineData(int.MaxValue, 2147483722L, 2147491492L, 2147491848L)]
    public void Effective_start_year_override_shifts_initial_subyear_and_tail_dates(int startYear, long firstYear, long subyearYear, long tailYear)
    {
        var original = mods.Content.Get<RuleConstantsDef>("foundation:rule_constants");
        var source = new ModSource { Manifest = new() { Id = "startyear_probe", Version = "1", Dependencies = ["foundation"] },
            Root = mods.Root, Instance = new StartYearProbe(original, startYear) };
        var content = ModLoader.Build(mods.Sources.Append(source));
        var sim = Scenario(content);
        var calendar = content.Services.Get<IGameCalendar>().Single();
        Assert.Equal(startYear, content.Get<RuleConstantsDef>(original.Id).Values.StartYear);
        Assert.Equal(startYear, calendar.GetDate(sim)!.Year);
        Assert.Equal(firstYear, calendar.GetDate(sim, 1)!.Year);
        var subyear = calendar.GetDate(sim, 692)!;
        Assert.Equal((subyearYear, 8, CalendarSeason.Summer), (subyear.Year, subyear.Month, subyear.Season));
        Assert.Equal(tailYear, calendar.GetDate(sim, 1201)!.Year);
        Assert.Equal(-6000, calendar.GetDate(Scenario(mods.Content))!.Year);
    }

    // Public custom-content boundary: missing schedules are neutral; invalid progress is rejected.
    [Fact]
    public void Custom_calendar_handles_year_zero_negative_months_missing_schedules_and_invalid_durations()
    {
        var source = new ModSource { Manifest = new() { Id = "calendar_probe", Version = "1", Dependencies = ["foundation"] },
            Root = mods.Root, Instance = new CalendarProbe() };
        var content = ModLoader.Build(mods.Sources.Append(source));
        var sim = Scenario(content, "calendar_probe:monthly");
        var calendar = content.Services.Get<IGameCalendar>().Single();
        Assert.Equal("December, 1 BCE", calendar.GetDate(sim, 71998)!.FormattedDate);
        Assert.Equal("January, 0 CE", calendar.GetDate(sim, 71999)!.FormattedDate);
        Assert.Null(calendar.GetDate(Scenario(content, "calendar_probe:empty")));

        Assert.Throws<InvalidDataException>(() => calendar.GetDate(Scenario(content, "calendar_probe:invalid")));
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.GetDate(sim, -1));
        Assert.Equal(178950970, calendar.GetDate(sim, int.MaxValue)!.Year);
    }

    private static Simulation Scenario(ContentDb content, string speed = "foundation:gamespeed_normal", string era = "foundation:era_ancient") =>
        Simulation.CreateScenario(content, GameFactory.Settings() with { GameSpeed = speed, StartingEra = era },
            new(2, 2), Enumerable.Range(0, 4).Select(_ => new Plot { Terrain = "world:grass" }).ToArray());

    private sealed class StartYearProbe(RuleConstantsDef original, int startYear) : IMod
    {
        public void Register(IModContext context) => context.Content<RuleConstantsDef>().Override(new()
        {
            Id = original.Id, Name = original.Name, Values = original.Values with { StartYear = startYear }, Raw = original.Raw,
        });
    }

    private sealed class CalendarProbe : IMod
    {
        public void Register(IModContext context)
        {
            context.Content<GameSpeedDef>().Register(new() { Id = "calendar_probe:monthly", TurnIncrements = [new(1, 1)] });
            context.Content<GameSpeedDef>().Register(new() { Id = "calendar_probe:empty" });
            context.Content<GameSpeedDef>().Register(new() { Id = "calendar_probe:invalid", TurnIncrements = [new(0, 1)] });
        }
    }
}
