using Civ.Game;

namespace Civ.Mods.Foundation;

internal sealed class SetupPolicy : ISetupPolicy
{
    private static readonly string[] Handicaps = ["settler", "chieftain", "warlord", "noble", "prince", "monarch", "emperor", "immortal", "deity"];

    public string? Default(SetupField field) => field switch
    {
        SetupField.WorldSize => "foundation:worldsize_standard",
        SetupField.GameSpeed => "foundation:gamespeed_normal",
        SetupField.Handicap => "foundation:handicap_noble",
        SetupField.StartingEra => "foundation:era_ancient",
        _ => null,
    };

    public int? SortOrder(SetupField field, string id) => field == SetupField.Handicap
        ? Array.IndexOf(Handicaps, id.Split('_').Last()) : null;

    public IEnumerable<SetupGameOption> GameOptions(Civ.Content.ContentDb content)
    {
        string[] implemented =
        [
            "foundation:gameoption_assimilation",
            "foundation:gameoption_realistic_corporations",
            "foundation:gameoption_realistic_culture_spread",
            "foundation:gameoption_religion_decay",
            "foundation:gameoption_no_inquisitions",
            "foundation:gameoption_no_revolution",
        ];
        foreach (string id in implemented)
        {
            if (!content.TryGet<Civ.Content.Schema.GameOptionDef>(id, out var option)) continue;
            yield return new(option.Id, option.Name, option.Default, option.Help);
        }
        yield return new("foundation:gameoption_multiple_religion_spread", "Multiple Religion Spread", false,
            "Cities with an existing religion can still receive additional religions via natural spread.");
        yield return new("foundation:gameoption_resource_depletion", "Resource Depletion", false,
            "Worked mines and resource deposits can run out. New resources are discovered more often.");
        yield return new("foundation:gameoption_realistic_timescale", "Realistic Timescale", true,
            "Adjust research costs to keep technology progress near the expected era.");
    }
}
