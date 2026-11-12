using Ariel.Game;

namespace Ariel.Rules.World;

internal sealed class SetupPolicy : ISetupPolicy
{
    public string? Default(SetupField field) => field == SetupField.MapGenerator ? "world:continents" : null;
}
