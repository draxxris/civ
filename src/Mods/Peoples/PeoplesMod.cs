using Ariel.Content.Schema;
using Ariel.Modding;
using IModifierSource = Ariel.Content.IModifierSource;
using Ariel.Game;

namespace Ariel.Mods.Peoples;

/// <summary>Identity, AI personality queries and leader trait contributions.</summary>
public sealed class PeoplesMod : IPreparableMod
{
    private static readonly ModJsonFile<CivilizationDef[]> Civilizations = new("data/civilization.json");
    private static readonly ModJsonFile<LeaderDef[]> Leaders = new("data/leader.json");
    private static readonly ModJsonFile<TraitDef[]> Traits = new("data/trait.json");
    public IReadOnlyList<ModJsonFile> PreparationFiles => [Civilizations, Leaders, Traits];

    public void Register(IModContext context)
    {
        ContentRegistration.Load(context, Civilizations);
        ContentRegistration.Load(context, Leaders);
        ContentRegistration.Load(context, Traits);
        var peoples = new PeoplesService();
        context.RegisterService<IPeoplesService>("identity", peoples);
        context.RegisterService<IModifierSource>("traits", new TraitEffects(peoples));
    }
}
