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
}
