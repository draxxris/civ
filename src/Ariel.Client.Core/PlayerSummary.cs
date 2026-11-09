using Ariel.Content;
using Ariel.Game;
using Schema = Ariel.Content.Schema;

namespace Ariel.Client;

public enum RateKind { Gold, Research, Culture, Espionage }

public sealed record ResearchProgress(string Id, string Name, long Progress, long Cost, int? Turns);

/// <summary>The numbers in the top bar: treasury, income, slider positions, research, turn and era.</summary>
public sealed record PlayerSummary(
    long Gold, long Net, long Income, long Expenses, CommerceVector Rates, long ResearchPerTurn, ResearchProgress? Research,
    int Turn, CalendarDate? CalendarDate, string Era, int Cities, int Units);

public sealed record TechChoice(string Id, string Name, string Era, long Cost, int? Turns, string Help);

public sealed partial class GameSession
{
    public PlayerSummary Summary()
    {
        var economy = Service<IPlayerEconomyService>();
        var result = economy.Calculate(Sim, LocalPlayer);
        long research = ResearchBaseRate(result.CommerceHundredths.Research);
        return new(Me.Treasury, result.NetGold, result.Income, result.Expenses, economy.Rates(Sim, LocalPlayer), research,
            CurrentResearch(research), Sim.State.Turn, OptionalService<IGameCalendar>()?.GetDate(Sim), EraName(),
            Sim.State.Cities.Count(c => c.Owner == LocalPlayer), Sim.State.Units.Count(u => u.Owner == LocalPlayer));
    }

    public string EraName()
    {
        var id = Me.Extensions.Get<CityPlayerState>(Sim.Content)?.CurrentEra ?? Sim.Settings.StartingEra;
        return Sim.Content.TryGet<Schema.EraDef>(id, out var era) ? era.Name : id;
    }

    private ResearchProgress? CurrentResearch(long perTurn)
    {
        if (MyTeam.Researching is not { } id) return null;
        long cost = Service<IResearchService>().CalculateCost(Sim, MyTeam.Id, id).Cost;
        long progress = MyTeam.ResearchProgress;
        return new(id, Sim.Content.Get<Schema.TechDef>(id).Name, progress, cost, ResearchTurns(id, cost - progress, perTurn));
    }

    public static int? TurnsToFinish(long remaining, long perTurn) =>
        perTurn <= 0 ? null : (int)Math.Max(1, (remaining + perTurn - 1) / perTurn);

    public ResearchSelectionView? ResearchSelection() => OptionalService<IResearchSelectionPresentation>()?.Describe(Sim, LocalPlayer);
    public TechTreeView? DescribeTechTree() => OptionalService<ITechTreePresentation>()?.Describe(Sim, LocalPlayer);

    /// <summary>Technologies the team can select now, cheapest first, or in offered order for cards.</summary>
    public IReadOnlyList<TechChoice> ResearchChoices()
    {
        var research = Service<IResearchService>();
        long perTurn = ResearchBaseRate(Service<IPlayerEconomyService>().Calculate(Sim, LocalPlayer).CommerceHundredths.Research);
        var choices = new List<TechChoice>();
        foreach (var tech in Sim.Content.Get<Schema.TechDef>().All)
        {
            if (!research.ValidateSelection(Sim, LocalPlayer, tech.Id).Success) continue;
            long cost = research.CalculateCost(Sim, MyTeam.Id, tech.Id).Cost;
            var era = tech.Era is { } e && Sim.Content.TryGet<Schema.EraDef>(e, out var def) ? def.Name : "";
            choices.Add(new(tech.Id, tech.Name, era, cost, ResearchTurns(tech.Id, cost - MyTeam.Research.GetValueOrDefault(tech.Id), perTurn), tech.Help));
        }
        if (ResearchSelection() is { Layout: ResearchChoiceLayout.Cards } selection)
            return selection.Offered.Select(id => choices.FirstOrDefault(c => c.Id == id)).OfType<TechChoice>().ToList();
        return choices.OrderBy(c => c.Cost).ThenBy(c => c.Name, StringComparer.Ordinal).ToList();
    }

    private long ResearchBaseRate(long commerceHundredths)
    {
        if (GovernmentStatus.IsAnarchy(Sim, LocalPlayer) || !Sim.State.Cities.Any(c => c.Owner == LocalPlayer)) return 0;
        return Math.Max(0, Sim.Content.Get<Schema.RuleConstantsDef>().All.Single().Values.BaseResearchRate + commerceHundredths / 100);
    }

    private int? ResearchTurns(string tech, long remaining, long baseRate) =>
        TurnsToFinish(remaining, baseRate * Service<IResearchService>().RateModifier(Sim, MyTeam.Id, tech) / 100);

    public int FreeResearchCredits => MyTeam.Extensions.Get<ResearchTeamState>(Sim.Content)?.FreeTechs ?? 0;
    public bool Research(string tech, bool free = false) => Submit(new SetTeamResearchCommand { Player = LocalPlayer, Tech = tech, FreeTech = free });

    /// <summary>Move <paramref name="delta"/> percent of commerce into <paramref name="to"/>, paid from gold (or from research when raising gold).</summary>
    public bool ShiftRate(RateKind to, int delta)
    {
        var rates = Service<IPlayerEconomyService>().Rates(Sim, LocalPlayer);
        var parts = new[] { rates.Gold, rates.Research, rates.Culture, rates.Espionage };
        int donor = to == RateKind.Gold ? (int)RateKind.Research : (int)RateKind.Gold;
        parts[(int)to] += delta;
        parts[donor] -= delta;
        return Submit(new SetCommerceRatesCommand { Player = LocalPlayer, Rates = new(parts[0], parts[1], parts[2], parts[3]) });
    }
}
