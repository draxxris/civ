using Ariel.Content;
using Ariel.Content.Schema;
using Ariel.Game;
using Ariel.Modding;
using Ariel.Presentation;

namespace Ariel.TestMods.Bulletin;

/// <summary>
/// A mod that brings its own interface and nothing else: a screen whose button posts a bulletin through the mod's own
/// command, a row on the city screen, an encyclopedia category and a notification with a go-to city. It proves a mod
/// can add all of these without the client knowing it exists.
/// </summary>
public sealed class BulletinMod : IPreparableMod
{
    private static readonly ModJsonFile<BulletinDef[]> Bulletins = new("data/bulletin.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Bulletins];

    public void Register(IModContext context)
    {
        foreach (var definition in Bulletins.Read(context.Data)) context.Content<BulletinDef>().Register(definition);
        context.RegisterCommand<PostBulletinCommand>("post");
        context.RegisterStateComponent<BulletinPlayerState>("player_state");
        context.RegisterService<IGameScreen>("board", new BulletinBoard());
        context.RegisterService<ICityPanel>("city", new BulletinCityRow());
        context.RegisterService<IPediaSource>("pedia", new BulletinPedia());
        context.RegisterService<INotificationSource>("notices", new BulletinNotices());
    }
}

/// <summary>A kind of bulletin a player can post.</summary>
public sealed class BulletinDef : GameContentDef;

/// <summary>How many of each bulletin a player has posted.</summary>
public sealed class BulletinPlayerState
{
    public SortedDictionary<string, int> Posted { get; init; } = new(StringComparer.Ordinal);

    public int Total => Posted.Values.Sum();
}

public sealed record BulletinPosted(int Player, string Bulletin) : DomainEvent;

/// <summary>Post a bulletin: the player's count of it goes up by one.</summary>
public sealed class PostBulletinCommand : GameCommand
{
    public string Bulletin { get; init; } = "";

    public override string? Validate(Simulation sim)
    {
        if (!sim.State.Players.Any(p => p.Id == Player)) return "no such player";
        return sim.Content.TryGet<BulletinDef>(Bulletin, out _) ? null : $"no bulletin '{Bulletin}'";
    }

    public override void Apply(Simulation sim)
    {
        var player = sim.State.PlayerById(Player);
        var state = player.Extensions.Get<BulletinPlayerState>(sim.Content) ?? new();
        state.Posted[Bulletin] = state.Posted.GetValueOrDefault(Bulletin) + 1;
        player.Extensions.Set(sim.Content, state);
        sim.Events.Publish(new BulletinPosted(Player, Bulletin));
    }

    public static int Count(Simulation sim, int player, string? bulletin = null)
    {
        var state = sim.State.PlayerById(player).Extensions.Get<BulletinPlayerState>(sim.Content);
        if (state is null) return 0;
        return bulletin is null ? state.Total : state.Posted.GetValueOrDefault(bulletin);
    }
}

/// <summary>The board: pick a bulletin (local state <c>bulletin</c>), post it, and see what has been posted.</summary>
internal sealed class BulletinBoard : IGameScreen
{
    public const string ScreenId = "bulletin:board";
    public string Id => ScreenId;
    public int Order => 90;

    public ScreenButton? Button(ScreenContext context) => new("Bulletins", "Post bulletins to the realm");

    public ScreenView Describe(ScreenContext context)
    {
        var sim = context.Sim;
        var bulletins = sim.Content.Get<BulletinDef>().All.OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
        var chosen = bulletins.FirstOrDefault(b => b.Id == context.Local.Get("bulletin"));
        var rows = bulletins.Select(b => new TableRow(b.Id, [b.Name, PostBulletinCommand.Count(sim, context.Player, b.Id).ToString()],
            new ChangeState(context.Local.With("bulletin", b.Id)), b == chosen)).ToList();
        return new("Bulletin Board", [
            new Paragraph("intro", "Pick a bulletin, then post it.", Tone.Dim),
            new Table("posted", ["Bulletin", "Posted"], rows),
            new ActionButton("post", chosen is null ? "Post" : $"Post {chosen.Name}",
                chosen is null ? null : new SubmitCommand(new PostBulletinCommand { Player = context.Player, Bulletin = chosen.Id }),
                chosen is null ? "Pick a bulletin first" : null),
            new StatLine("total", "Posted in all", PostBulletinCommand.Count(sim, context.Player).ToString(), Tone.Accent),
        ], 480);
    }
}

/// <summary>The owner's bulletin count on every city screen.</summary>
internal sealed class BulletinCityRow : ICityPanel
{
    public string Id => "bulletin:city";
    public CityPanelSlot Slot => CityPanelSlot.Stats;

    public IReadOnlyList<ViewElement> Describe(ScreenContext context, City city) =>
        [new StatLine("posted", "Bulletins", PostBulletinCommand.Count(context.Sim, city.Owner).ToString())];
}

internal sealed class BulletinPedia : IPediaSource
{
    public string Id => "bulletin:pedia";
    public string Category => "Bulletins";
    public bool ListsUnlocks => false;
    public IEnumerable<PediaItem> Items(ContentDb content) => content.Get<BulletinDef>().All.Select(d => new PediaItem(d));
}

/// <summary>A posted bulletin, with the poster's first city to go to.</summary>
internal sealed class BulletinNotices : INotificationSource
{
    public string Id => "bulletin:notices";

    public Notice? Describe(NoticeContext context, DomainEvent e) => e is BulletinPosted p && p.Player == context.Player
        ? new("bulletin", $"Posted: {Labels.Name<BulletinDef>(context.Sim.Content, p.Bulletin)}",
            context.Sim.State.Cities.Where(c => c.Owner == p.Player).OrderBy(c => c.Id).FirstOrDefault()?.Id, Tone: Tone.Good)
        : null;
}
