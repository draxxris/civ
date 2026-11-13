using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Presentation;

namespace Ariel.Mods.Espionage;

/// <summary>
/// The espionage advisor: every known rival with its points and weight, the intelligence the points unlock against the
/// selected rival (read-only snapshots, never zero-filled when locked), and the selected spy's missions. Local state:
/// <c>rival</c> (team id), <c>city</c> (an inspected rival city) and <c>tech</c> (the technology to steal).
/// </summary>
internal sealed class EspionageScreen : IGameScreen
{
    public const string ScreenId = "espionage:screen";
    public string Id => ScreenId;
    public int Order => 20;

    public ScreenButton? Button(ScreenContext context) => context.Sim.Service<IEspionageService>() is null ? null
        : new("Espionage", "Rival intelligence, spending and counter-espionage");

    public ScreenView Describe(ScreenContext context)
    {
        var sim = context.Sim;
        var service = sim.Service<IEspionageService>();
        if (service is null) return new("Espionage", [new Paragraph("off", "Espionage is not part of this ruleset.", Tone.Dim)]);
        var rivals = service.KnownRivals(sim, context.Player);
        int? selected = context.Local.Int("rival") is int r && rivals.Contains(r) ? r : null;

        var list = new List<ViewElement>();
        foreach (int team in rivals) list.AddRange(RivalRow(context, service, team, selected == team));
        if (rivals.Count == 0) list.Add(new Paragraph("none", "You have not met any living rival.", Tone.Dim));
        // Held points are real points waiting for a target: say so rather than showing a silent zero.
        long held = service.Held(sim, context.Player);
        if (held > 0) list.Add(new Paragraph("held", $"{held} points held: set a weight to spend them.", Tone.Accent, TextSize.Small));

        var details = selected is int rival
            ? Intelligence(context, service, rival)
            : [new Paragraph("pick", "Select a rival to inspect intelligence.", Tone.Dim)];
        return new("Espionage", [new Columns("split", [new Column(3, list), new Column(7, details)]), SpyMissions(context, service)]);
    }

    private static IEnumerable<ViewElement> RivalRow(ScreenContext context, IEspionageService service, int team, bool selected)
    {
        var sim = context.Sim;
        string name = Labels.Team(sim, sim.State.TeamById(team), context.Player);
        int weight = service.Weight(sim, context.Player, team);
        long points = service.Points(sim, context.MyTeam.Id, team);
        yield return new ActionButton($"rival:{team}", name,
            new ChangeState(context.Local.With("rival", team).Without("city")), Tooltip: name, Selected: selected);
        yield return new Paragraph($"points:{team}", $"{points} points", Tone.Dim, TextSize.Small);
        ActionButton Step(string id, string label, int to, string tip) => new($"{id}:{team}", label,
            new SubmitCommand(new SetEspionageWeightCommand { Player = context.Player, TargetTeam = team, Weight = Math.Max(0, to) }),
            to < weight && weight == 0 ? "The weight is already zero" : null, tip);
        // The weight range is wide, so both single-point and bulk steps are offered. The weight reads on its own line,
        // so the four steps keep one line in the narrow rival column.
        yield return new Paragraph($"weight:{team}", $"Weight {weight}", Tone.Normal, TextSize.Small);
        yield return new Row($"steps:{team}", [
            Step("lower10", "−10", weight - 10, "Lower weight by ten"),
            Step("lower", "−", weight - 1, "Lower weight"),
            Step("raise", "+", weight + 1, "Raise weight"),
            Step("raise10", "+10", weight + 10, "Raise weight by ten"),
        ]);
    }

    private static List<ViewElement> Intelligence(ScreenContext context, IEspionageService service, int targetTeam)
    {
        var sim = context.Sim;
        int me = context.MyTeam.Id;
        var rival = sim.State.TeamById(targetTeam);
        // One bulk query gives every tier's threshold and unlock state from a single investment calculation.
        var permissions = service.PassiveStatus(sim, me, targetTeam);
        bool Allowed(PassiveEspionageKind kind) => permissions.Single(p => p.Kind == kind).Unlocked;
        long spending = service.Spending(sim, context.Player, Budget(sim, context.Player)).GetValueOrDefault(targetTeam);
        var items = new List<ViewElement>
        {
            new Heading("name", Labels.Team(sim, rival, context.Player)),
            new Paragraph("summary", $"{service.Points(sim, me, targetTeam)} points · weight {service.Weight(sim, context.Player, targetTeam)} · "
                + $"spending forecast {spending} points/turn", Tone.Accent),
            Protection("ours", "Our protection against this rival", "Their missions against us", service.CounterEspionage(sim, me, targetTeam), sim.State.Turn),
            Protection("theirs", "Rival protection against us", "Our missions against them", service.CounterEspionage(sim, targetTeam, me), sim.State.Turn),
        };
        foreach (var permission in permissions)
            items.Add(new Paragraph($"permission:{permission.Kind}", $"{PassiveLabel(permission.Kind)}: "
                + (permission.Unlocked ? "unlocked" : "locked") + $" · requires {permission.Threshold} points",
                permission.Unlocked ? Tone.Good : Tone.Dim, TextSize.Small));
        items.Add(Demographics(context, rival, Allowed(PassiveEspionageKind.Demographics)));
        items.Add(Research(sim, rival, Allowed(PassiveEspionageKind.InspectResearch)));
        items.Add(Cities(context, rival, Allowed(PassiveEspionageKind.CityVisibility), Allowed(PassiveEspionageKind.InvestigateCity)));
        return items;
    }

    /// <summary>Points per turn from the espionage slider: the mod's own source when one answers, else the economy.</summary>
    private static long Budget(Simulation sim, int player) => sim.Service<IPlayerEspionageSource>()?.EspionageHundredths(sim, player)
        ?? sim.Service<IPlayerEconomyService>()?.Calculate(sim, player).CommerceHundredths.Espionage ?? 0;

    internal static string PassiveLabel(PassiveEspionageKind kind) => kind switch
    {
        PassiveEspionageKind.Demographics => "Demographics",
        PassiveEspionageKind.CityVisibility => "City visibility",
        PassiveEspionageKind.InspectResearch => "Inspect research",
        PassiveEspionageKind.InvestigateCity => "Investigate city",
        _ => kind.ToString(),
    };

    private static Paragraph Protection(string id, string title, string direction, CounterEspionageEffect? effect, int turn)
    {
        string status = effect is null ? "inactive"
            : $"{Math.Max(0, effect.ExpiresTurn - turn)} turns remaining · {direction} cost {effect.CostPercent}% of normal";
        return new($"protection:{id}", $"{title}: {status}", effect is null ? Tone.Dim : Tone.Accent, TextSize.Small);
    }

    private static Section Demographics(ScreenContext context, Team rival, bool unlocked)
    {
        var sim = context.Sim;
        if (!unlocked)
            return new("demographics", "Demographics and treasury",
                [new Paragraph("locked", "Locked — unlock Demographics to see statistics and each player's gold reserves.", Tone.Dim)]);
        var cities = sim.State.Cities.Where(c => rival.Members.Contains(c.Owner)).ToArray();
        int units = sim.State.Units.Count(u => rival.Members.Contains(u.Owner));
        var items = new List<ViewElement>
        {
            new Paragraph("totals", $"{cities.Length} cities · population {cities.Sum(c => (long)c.Population)} · {units} units"),
        };
        foreach (int member in rival.Members)
            items.Add(new Paragraph($"treasury:{member}",
                $"{Labels.Player(sim, member, context.Player)} (player {member}): {sim.State.PlayerById(member).Treasury} gold reserves"));
        return new("demographics", "Demographics and treasury", items);
    }

    private static Section Research(Simulation sim, Team rival, bool unlocked)
    {
        var research = sim.Service<IResearchService>();
        if (!unlocked || research is null)
            return new("research", "Research", [new Paragraph("locked", "Locked or unavailable — requires Inspect research.", Tone.Dim)]);
        var queue = rival.Extensions.Get<ResearchTeamState>(sim.Content)?.Queue ?? [];
        string current = rival.Researching is { } id
            ? $"{Labels.Name<TechDef>(sim.Content, id)}: {rival.ResearchProgress}/{research.CalculateCost(sim, rival.Id, id).Cost} research"
            : "No current research.";
        string order = queue.Count == 0 ? "Research queue is empty."
            : "Research queue: " + string.Join(" → ", queue.Select(t => Labels.Name<TechDef>(sim.Content, t)));
        return new("research", "Research", [new Paragraph("current", current), new Paragraph("queue", order)]);
    }

    private static Section Cities(ScreenContext context, Team rival, bool visible, bool investigate)
    {
        const string Title = "Cities · read-only";
        if (!visible) return new("cities", Title, [new Paragraph("locked", "Locked — unlock City visibility to see rival cities.", Tone.Dim)]);
        var sim = context.Sim;
        var cities = sim.State.Cities.Where(c => rival.Members.Contains(c.Owner)).OrderBy(c => c.Id).ToList();
        var chosen = cities.FirstOrDefault(c => c.Id == context.Local.Int("city"));
        var items = new List<ViewElement>
        {
            new Picker("city", cities.Select(c => new PickerOption($"city:{c.Id}", $"{c.Name} (player {c.Owner})",
                new ChangeState(context.Local.With("city", c.Id)), c == chosen)).ToList(),
                cities.Count == 0 ? "No rival cities." : "Select a city"),
        };
        if (chosen is null) return new("cities", Title, items);
        items.Add(new Paragraph("chosen", $"{chosen.Name} · {chosen.Position}", Tone.Accent));
        items.AddRange(CityDetails(sim, chosen, investigate));
        return new("cities", Title, items);
    }

    // The own city screen's description holds the player's choices and controls, not rival intelligence, so the
    // figures are read here.
    private static IEnumerable<ViewElement> CityDetails(Simulation sim, City city, bool investigate)
    {
        var yields = sim.Service<ICityYieldService>();
        var wellbeing = sim.Service<ICityWellbeingService>();
        if (!investigate || yields is null || wellbeing is null)
        {
            yield return new Paragraph("details:locked",
                "Locked or unavailable — Investigate city is required for statistics and production queues.", Tone.Dim);
            yield break;
        }
        var output = yields.Calculate(sim, city.Id).Yields;
        var health = wellbeing.Calculate(sim, city.Id);
        yield return new Paragraph("details", $"Population {city.Population} · stored food {city.FoodStored}\n"
            + $"Food {output.Food} · production {output.Production} · commerce {output.Commerce}\n"
            + $"Happy {health.Happy} / unhappy {health.Unhappy} · healthy {health.Healthy} / unhealthy {health.Unhealthy}");
        yield return new Heading("queue", "Production queue · read-only");
        if (city.ProductionQueue.Count == 0) yield return new Paragraph("queue:empty", "Production queue is empty.", Tone.Dim);
        var kinds = sim.Content.Services.Get<IProductionKind>();
        for (int i = 0; i < city.ProductionQueue.Count; i++)
        {
            var order = city.ProductionQueue[i];
            var kind = kinds.Single(k => k.Id == order.Kind);
            var calc = kind.Calculate(sim, city.Id, order.ContentId);
            string progress = kind.IsContinuous ? "continuous" : $"{order.Progress}/{calc.Cost}";
            long left = calc.Cost - order.Progress;
            string turns = kind.IsContinuous || calc.Production <= 0 ? "" : $" · {Math.Max(1, (left + calc.Production - 1) / calc.Production)} turns";
            yield return new Paragraph($"queue:{i}", $"{i + 1}. {kind.Definition(sim.Content, order.ContentId).Name} · {progress}{turns}");
        }
    }

    /// <summary>The selected spy's legal missions where it stands, and the technology theft, which needs a target.</summary>
    private static Section SpyMissions(ScreenContext context, IEspionageService service)
    {
        var sim = context.Sim;
        var items = new List<ViewElement>
        {
            new Paragraph("note", "Spy missions use the selected unit at its location, not the inspected city.", Tone.Dim, TextSize.Small),
        };
        var unit = context.Unit is int id ? sim.State.FindUnit(id) : null;
        var buttons = new List<ViewElement>();
        if (unit is not null && unit.Owner == context.Player)
            foreach (var source in sim.Rules().UnitActions)
                foreach (var offer in source.Offers(sim, context.Player, unit))
                    if (offer.Id.StartsWith("espionage:", StringComparison.Ordinal) && offer.Command.Validate(sim) is null)
                        buttons.Add(new ActionButton(offer.Id, offer.Label, new SubmitCommand(offer.Command), Tooltip: offer.Tooltip));
        if (buttons.Count == 0)
            buttons.Add(new Paragraph("none", "Select a spy in a rival city with moves and enough points for a legal mission.", Tone.Dim, TextSize.Small));
        items.Add(new Row("missions", buttons));
        if (unit is not null && unit.Owner == context.Player && sim.State.CityAt(unit.Position) is { } city)
            items.AddRange(TechTheft(context, service, unit, city));
        return new("spy", null, items);
    }

    private static IEnumerable<ViewElement> TechTheft(ScreenContext context, IEspionageService service, Unit unit, City city)
    {
        var sim = context.Sim;
        var techs = service.StealableTechnologies(sim, context.Player, city.Id)
            .OrderBy(t => service.MissionCost(sim, context.Player, city.Id, EspionageMissionKind.StealTech, t)).ThenBy(t => t, StringComparer.Ordinal)
            .ToList();
        if (techs.Count == 0) yield break;
        string chosen = techs.Contains(context.Local.Get("tech") ?? "") ? context.Local.Get("tech")! : techs[0];
        yield return new Row("steal", [
            new Picker("tech", techs.Select(t => new PickerOption(t, Labels.Name<TechDef>(sim.Content, t),
                new ChangeState(context.Local.With("tech", t)), t == chosen)).ToList()),
            new ActionButton("steal_tech", "Steal technology",
                new SubmitCommand(EspionageMissions.Order(context.Player, unit.Id, EspionageMissionKind.StealTech, chosen)),
                Tooltip: "Steal a technology this rival knows"),
        ]);
    }
}

/// <summary>Tells the player how their spies' missions went, and about missions run in their cities.</summary>
internal sealed class EspionageNotices : INotificationSource
{
    public string Id => "espionage:notices";

    public Notice? Describe(NoticeContext context, DomainEvent e) => e switch
    {
        EspionageMissionResult m when m.Unit >= 0 && (context.WasMine(m.Unit) || context.OwnsCity(m.City)) =>
            new("espionage", $"Spy mission {Labels.Humanize(m.Mission.ToString())} in {context.CityName(m.City)} {(m.Success ? "succeeded" : "failed")}", m.City),
        _ => null,
    };
}
