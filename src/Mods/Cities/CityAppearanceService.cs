using Civ.Game;

namespace Civ.Mods.Cities;

/// <summary>Huts on dirt up to the medieval era, Victorian houses for the renaissance (dirt) and industrial
/// (cobblestone) eras, and modern buildings on concrete from the modern era on.</summary>
internal sealed class CityAppearanceService : ICityAppearanceService
{
    private static readonly IReadOnlyDictionary<string, CityAppearance> Eras = new Dictionary<string, CityAppearance>(StringComparer.Ordinal)
    {
        ["foundation:era_ancient"] = new(CityStyle.Huts, CityGround.Dirt),
        ["foundation:era_classical"] = new(CityStyle.Huts, CityGround.Dirt),
        ["foundation:era_medieval"] = new(CityStyle.Huts, CityGround.Dirt),
        ["foundation:era_renaissance"] = new(CityStyle.Victorian, CityGround.Dirt),
        ["foundation:era_industrial"] = new(CityStyle.Victorian, CityGround.Cobblestone),
        ["foundation:era_modern"] = new(CityStyle.Modern, CityGround.Concrete),
        ["foundation:era_transhuman"] = new(CityStyle.Modern, CityGround.Concrete),
        ["foundation:era_future"] = new(CityStyle.Modern, CityGround.Concrete),
    };

    public CityAppearance? Era(string era) => Eras.GetValueOrDefault(era);
}
