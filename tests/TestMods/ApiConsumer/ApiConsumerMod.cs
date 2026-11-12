using Ariel.Content;
using Ariel.Modding;
using Ariel.TestMods.ApiProvider;

namespace Ariel.TestMods.ApiConsumer;

/// <summary>Uses the provider's API without depending on the provider. Its probe answers with the service it finds
/// under its own view of <see cref="IGreeting"/>, so a non-null answer shows both mods share one copy of the API.</summary>
public sealed class ApiConsumerMod : IMod
{
    public void Register(IModContext ctx) =>
        ctx.RegisterService<Func<ContentDb, object?>>("probe", content => content.Services.Find<IGreeting>());
}
