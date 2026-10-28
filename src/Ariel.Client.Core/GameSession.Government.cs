using Ariel.Game;
using Schema = Ariel.Content.Schema;

namespace Ariel.Client;

public sealed record CivicChoice(string Id, string Name, bool Active, IReadOnlyList<string> Blockers, string Help);
public sealed record CivicGroup(string Id, string Name, string? Selected, IReadOnlyList<CivicChoice> Choices);
public sealed record GovernmentSummary(int AnarchyTurns, int CooldownTurns, IReadOnlyList<CivicGroup> Groups);
public sealed record RevolutionPreview(int AnarchyTurns, int CooldownTurns, string? Blocked);

public sealed partial class GameSession
{
    /// <summary>Options and eligibility against the complete proposed cabinet, without changing player state.</summary>
    public GovernmentSummary Government(IReadOnlyList<string>? proposed = null)
    {
        var cabinet = new SortedDictionary<string, string>(Me.Civics, StringComparer.Ordinal);
        foreach (var id in proposed ?? []) cabinet[Sim.Content.Get<Schema.CivicDef>(id).Option!] = id;
        var groups = Sim.Content.Get<Schema.CivicOptionDef>().All.Select(option => new CivicGroup(
            option.Id, option.Name, cabinet.GetValueOrDefault(option.Id), CivicChoices(option.Id, cabinet))).ToList();
        var state = Me.Extensions.Get<GovernmentPlayerState>(Sim.Content);
        return new(state?.AnarchyTurns ?? 0, state?.RevolutionTimer ?? 0, groups);
    }

    private IReadOnlyList<CivicChoice> CivicChoices(string option, IReadOnlyDictionary<string, string> cabinet)
    {
        return Sim.Content.Get<Schema.CivicDef>().All.Where(c => c.Option == option).OrderBy(c => c.Name, StringComparer.Ordinal)
            .Select(c => CivicChoiceOf(c, cabinet)).ToList();
    }

    private CivicChoice CivicChoiceOf(Schema.CivicDef civic, IReadOnlyDictionary<string, string> cabinet)
    {
        var alternative = new SortedDictionary<string, string>(cabinet.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal)
        { [civic.Option!] = civic.Id };
        var result = Sim.Content.Requirements.Evaluate(civic.Requirements, new(Sim, Human, ProposedCivics: alternative));
        return new(civic.Id, civic.Name, Me.Civics.GetValueOrDefault(civic.Option!) == civic.Id,
            result.Failures.Select(f => f.Explanation).ToList(), civic.Help);
    }

    public RevolutionPreview PreviewCivics(IReadOnlyList<string> civics)
    {
        var service = Service<IGovernmentService>();
        var result = service.Calculate(Sim, Human, civics);
        return new(result.AnarchyTurns, result.CooldownTurns, service.Validate(Sim, Human, civics));
    }

    public bool AdoptCivics(IReadOnlyList<string> civics) =>
        Submit(new SetCivicsCommand { Player = Human, Civics = civics.ToList() });
}
