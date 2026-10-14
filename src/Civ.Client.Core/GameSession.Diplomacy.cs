using Civ.Content;
using Civ.Game;
using Schema = Civ.Content.Schema;

namespace Civ.Client;

/// <summary>One diplomatic action the local player may try. <see cref="Blocked"/> is the rules' reason, or null when legal.</summary>
public sealed record DiplomacyAction(string Kind, string Label, GameCommand Command, string? Blocked);

/// <summary>An open or active deal with a rival.</summary>
public sealed record DiplomacyDealRow(int Id, int OtherPlayer, string Status, string Summary, DiplomacyAction? Cancel, DiplomacyAction? Accept);

/// <summary>A met rival with attitude, treaties and the actions available against them.</summary>
public sealed record DiplomacyRival(
    int PlayerId, int TeamId, string Name, string Leader, string Civilization, int AttitudeScore, AttitudeTier Tier,
    IReadOnlyList<LedgerLine> Breakdown, bool AtWar, bool PeaceTreaty, bool OpenBorders, bool DefensivePact,
    string Status, IReadOnlyList<DiplomacyAction> Actions);

/// <summary>The diplomacy screen. <see cref="Available"/> is false when no ruleset registers diplomacy.</summary>
public sealed record DiplomacyScreen(bool Available, IReadOnlyList<DiplomacyRival> Rivals, IReadOnlyList<DiplomacyDealRow> Deals);

/// <summary>One row of the score table.</summary>
public sealed record ScoreRow(int PlayerId, string Name, string Civilization, bool IsLocal, PlayerScore Score);

/// <summary>Standings and the game outcome. <see cref="Status"/> is null when no ruleset registers victory rules.</summary>
public sealed record VictoryScreen(
    bool Available, VictoryStatus? Status, string? WinnerName, string? VictoryName, IReadOnlyList<string> Conditions,
    IReadOnlyList<ScoreRow> Scores);

public sealed partial class GameSession
{
    /// <summary>Diplomacy with every rival the local team has met.</summary>
    public DiplomacyScreen DescribeDiplomacy()
    {
        var service = OptionalService<IDiplomacyService>();
        if (service == null) return new(false, [], []);
        int myTeam = MyTeam.Id;
        var rivals = Sim.State.Players.Where(p => p.TeamId != myTeam && service.HasMet(Sim, myTeam, p.TeamId))
            .OrderBy(p => p.Id).Select(p => RivalOf(service, p)).ToList();
        var deals = service.Deals(Sim)
            .Where(d => d.Status is DiplomaticDealStatus.Active or DiplomaticDealStatus.Proposed && (d.Proposer == Human || d.Recipient == Human))
            .OrderBy(d => d.Id).Select(d => DealOf(service, d)).ToList();
        return new(true, rivals, deals);
    }

    private DiplomacyRival RivalOf(IDiplomacyService service, Player p)
    {
        int myTeam = MyTeam.Id;
        var attitude = service.GetAttitude(Sim, p.Id, Human);
        bool war = MyTeam.Wars.Contains(p.TeamId);
        bool treaty = !war && service.ValidateWar(Sim, Human, p.Id) != null;
        bool borders = service.HasOpenBorders(Sim, myTeam, p.TeamId), pact = service.HasDefensivePact(Sim, myTeam, p.TeamId);
        string status = war ? "At war" : treaty ? "Peace treaty" : "At peace";
        var actions = new List<DiplomacyAction>
        {
            new("declare_war", "Declare war", new DiplomaticDeclareWarCommand { Player = Human, Target = p.Id }, service.ValidateWar(Sim, Human, p.Id)),
            new("make_peace", "Make peace", new DiplomaticMakePeaceCommand { Player = Human, Target = p.Id }, service.ValidatePeace(Sim, Human, p.Id)),
            new("open_borders", "Open borders", new SignOpenBordersCommand { Player = Human, Target = p.Id },
                service.ValidateAgreement(Sim, Human, p.Id, DiplomaticAgreement.OpenBorders)),
            new("defensive_pact", "Defensive pact", new SignDefensivePactCommand { Player = Human, Target = p.Id },
                service.ValidateAgreement(Sim, Human, p.Id, DiplomaticAgreement.DefensivePact)),
        };
        string leader = Sim.Content.TryGet<Schema.LeaderDef>(p.Leader, out var l) ? l.Name : p.Leader;
        string civ = Sim.Content.TryGet<Schema.CivilizationDef>(p.Civilization, out var c) ? c.Name : p.Civilization;
        return new(p.Id, p.TeamId, p.Name, leader, civ, attitude.Score, attitude.Tier, attitude.Ledger.Lines, war, treaty, borders, pact, status, actions);
    }

    private DiplomacyDealRow DealOf(IDiplomacyService service, DiplomaticDeal deal)
    {
        int other = deal.Proposer == Human ? deal.Recipient : deal.Proposer;
        string summary = string.Join("; ", deal.Items.Select(i =>
            $"{(i.FromPlayer == Human ? "You give" : "You get")} {(i.Kind is TradeItemKind.Technology ? "" : i.Amount + " ")}{ItemName(i)}"));
        var cancel = new DiplomacyAction("cancel_deal", "Cancel deal", new CancelDealCommand { Player = Human, Deal = deal.Id },
            service.ValidateCancellation(Sim, Human, deal.Id));
        DiplomacyAction? accept = deal.Status == DiplomaticDealStatus.Proposed && deal.Recipient == Human
            ? new("accept_deal", "Accept", new ExecuteDealCommand { Player = Human, Deal = deal.Id }, service.ValidateExecution(Sim, Human, deal.Id))
            : null;
        return new(deal.Id, other, deal.Status.ToString(), summary, cancel, accept);
    }

    private string ItemName(DiplomaticTradeItem item) => item.Kind switch
    {
        TradeItemKind.Gold => "gold",
        TradeItemKind.GoldPerTurn => "gold per turn",
        _ => Sim.Content.TryGet<Schema.TechDef>(item.ContentId, out var t) ? t.Name
            : Sim.Content.TryGet<Schema.BonusDef>(item.ContentId, out var b) ? b.Name : item.ContentId,
    };

    /// <summary>Run an action from <see cref="DescribeDiplomacy"/>.</summary>
    public bool DoDiplomacy(DiplomacyAction action) => Submit(action.Command);

    /// <summary>Score table, the outcome so far, and the victory rules that are enabled.</summary>
    public VictoryScreen DescribeVictoryAndScores()
    {
        var service = OptionalService<IProjectVictoryService>();
        if (service == null) return new(false, null, null, null, [], []);
        var status = service.GetVictoryStatus(Sim);
        var scores = Sim.State.Players.OrderBy(p => p.Id).Select(p => new ScoreRow(p.Id, p.Name,
            Sim.Content.TryGet<Schema.CivilizationDef>(p.Civilization, out var c) ? c.Name : p.Civilization, p.Id == Human, service.GetScore(Sim, p.Id)))
            .OrderByDescending(r => r.Score.Total).ThenBy(r => r.PlayerId).ToList();
        string? winner = status.IsGameOver && Sim.State.Players.Any(p => p.Id == status.WinningPlayerId) ? Sim.State.PlayerById(status.WinningPlayerId).Name : null;
        string? victory = status.VictoryId is { } v && Sim.Content.TryGet<Schema.VictoryDef>(v, out var def) ? def.Name : status.VictoryId;
        var conditions = Sim.Content.Get<Schema.VictoryDef>().All.Where(d => d.Enabled).OrderBy(d => d.Priority).ThenBy(d => d.Id, StringComparer.Ordinal)
            .Select(d => d.Name).ToList();
        return new(true, status, winner, victory, conditions, scores);
    }
}
