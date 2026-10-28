using Ariel.Game;

namespace Ariel.BaseGame;

internal sealed class SetupPolicy : ISetupPolicy
{
    public string? Default(SetupField field) => field == SetupField.MapGenerator ? "base:continents" : null;
}
