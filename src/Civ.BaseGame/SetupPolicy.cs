using Civ.Game;

namespace Civ.BaseGame;

internal sealed class SetupPolicy : ISetupPolicy
{
    public string? Default(SetupField field) => field == SetupField.MapGenerator ? "base:continents" : null;
}
