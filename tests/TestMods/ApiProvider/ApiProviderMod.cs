using Ariel.Modding;

namespace Ariel.TestMods.ApiProvider;

/// <summary>Implements the published <see cref="IGreeting"/>.</summary>
public sealed class ApiProviderMod : IMod
{
    public void Register(IModContext ctx) => ctx.RegisterService<IGreeting>("greeting", new Greeting());

    private sealed class Greeting : IGreeting
    {
        public string Hello() => "hello";
    }
}
