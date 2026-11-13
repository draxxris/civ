using Ariel.Ai;
using Ariel.Modding;

namespace Ariel.TestMods.ResourceVariety;

/// <summary>The "food variety" idea in miniature: a second copy of a resource is worth less than a first copy of
/// another, so corn and pigs beat two corn. One resource valuer says so, and the computer player hears it wherever it
/// values a resource: the plots of a city site, the plot a worker connects, and resources in deals.</summary>
public sealed class ResourceVarietyMod : IMod
{
    /// <summary>Points a resource the player lacks gains, and one it already holds loses.</summary>
    public const int Variety = 200;

    public void Register(IModContext context) =>
        context.RegisterService<IAiValuer<ResourceSubject>>("valuer", new VarietyValuer());

    private sealed class VarietyValuer : IAiValuer<ResourceSubject>
    {
        public string Id => "resource_variety:valuer";

        public int Adjust(AiContext ctx, in ResourceSubject subject) => subject.Held == 0 ? Variety : -Variety;
    }
}
