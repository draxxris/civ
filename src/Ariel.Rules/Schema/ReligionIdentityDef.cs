namespace Ariel.Content.Schema;

/// <summary>A religion as base content names it: in <c>religion</c> requirement leaves and in the religion fields of
/// buildings, units and leaders. The rules of a religion (founding, spread, its commerce) belong to the mod that runs
/// religion, whose own definition names this identity. Without that mod the identities exist but no city follows one.</summary>
public sealed class ReligionIdentityDef : GameContentDef { }
