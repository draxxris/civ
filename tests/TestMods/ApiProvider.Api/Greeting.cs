[assembly: Ariel.Modding.ModApi("apiprovider")]

namespace Ariel.TestMods.ApiProvider;

/// <summary>The service the provider test mod publishes.</summary>
public interface IGreeting
{
    /// <summary>A fixed answer, so a consumer can show it reached the provider.</summary>
    string Hello();
}
