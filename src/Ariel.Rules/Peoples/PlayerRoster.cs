using Ariel.Content;
using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.Peoples;

/// <summary>What a nation starts with, whether it is seated when the game is set up or joins a running one.</summary>
internal static class PlayerRoster
{
    /// <summary>The civic a new nation holds in each option: the option's default, or else its first civic without
    /// requirements, replaced by the civilization's own starting civics.</summary>
    internal static void StartingCivics(ContentDb content, S.CivilizationDef civilization, IDictionary<string, string> civics)
    {
        foreach (var option in content.Get<S.CivicOptionDef>().All)
        {
            var defaultCivic = option.DefaultCivic ?? content.Get<S.CivicDef>().All.FirstOrDefault(c => c.Option == option.Id && c.Requirements is null)?.Id;
            if (defaultCivic is not null) civics[option.Id] = defaultCivic;
        }
        foreach (var civic in civilization.StartingCivics)
            if (content.Get<S.CivicDef>(civic).Option is { } optionId) civics[optionId] = civic;
    }

    /// <summary>Teach a team the granted technologies, in ordinal order.</summary>
    internal static void AddTechs(Team team, IReadOnlyDictionary<string, bool> grants) =>
        team.Techs.UnionWith(grants.Where(p => p.Value).Select(p => p.Key).Order(StringComparer.Ordinal));
}
