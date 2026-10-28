using Ariel.Game;

namespace Ariel.Mods.World;

internal sealed class SetupPolicy : ISetupPolicy
{
    public string? Default(SetupField field) => field == SetupField.MapGenerator ? "world:continents" : null;
}
