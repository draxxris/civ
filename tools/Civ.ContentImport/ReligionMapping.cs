namespace Civ.ContentImport;

public sealed partial class ContentMapper
{
    private void ReligionBuildings(EffectiveRecord religion, Dictionary<string, object?> values)
    {
        var buildings = records.Values.Where(r => r.Class == "BuildingInfo").ToArray();
        values["shrines"] = RelatedBuildings(buildings, r => r.Node.Value("GlobalReligionCommerce") == religion.Type);
        foreach (var (property, category) in new[] { ("temples", "TEMPLE"), ("monasteries", "MONASTERY"), ("cathedrals", "CATHEDRAL") })
            values[property] = RelatedBuildings(buildings, r => r.Node.Value("ReligionType") == religion.Type
                && r.Node.Value("SpecialBuildingType") == "SPECIALBUILDING_" + category);
    }
    private string[] RelatedBuildings(EffectiveRecord[] buildings, Func<EffectiveRecord, bool> predicate) =>
        buildings.Where(predicate).Select(r => ids[(r.Class, r.Type)]).Order(StringComparer.Ordinal).ToArray();
}
