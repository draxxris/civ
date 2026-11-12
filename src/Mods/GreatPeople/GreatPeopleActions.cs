using Ariel.Game;

namespace Ariel.Mods.GreatPeople;

/// <summary>What a great person can do where it stands, as the orders its panel offers.</summary>
internal sealed class GreatPeopleActions(IGreatPeopleService service) : IUnitActionSource
{
    public IEnumerable<UnitActionOffer> Offers(Simulation sim, int player, Unit unit)
    {
        foreach (var action in service.AvailableActions(sim, player, unit.Id))
        {
            string kind = action.Command[(action.Command.LastIndexOf(':') + 1)..];
            if (Order(kind, player, unit.Id, action.Target) is not var (label, command)) continue;
            yield return new(action.Command, label, command, action.Amount > 0 ? action.Amount.ToString() : "");
        }
    }

    private static (string Label, GameCommand Command)? Order(string kind, int player, int unit, string? target) => kind switch
    {
        "join_city" => ("Join city", new JoinGreatPersonCommand { Player = player, UnitId = unit, Target = target }),
        "discover_tech" => ("Discover technology", new DiscoverGreatPersonCommand { Player = player, UnitId = unit, Target = target }),
        "hurry_production" => ("Hurry production", new HurryGreatPersonCommand { Player = player, UnitId = unit, Target = target }),
        "culture_bomb" => ("Great work", new CultureGreatPersonCommand { Player = player, UnitId = unit, Target = target }),
        "golden_age" => ("Start golden age", new GoldenAgeGreatPersonCommand { Player = player, UnitId = unit, Target = target }),
        "lead_unit" => ("Attach leader", new LeadGreatPersonCommand { Player = player, UnitId = unit, Target = target }),
        "construct_building" => ("Construct building", new ConstructGreatPersonCommand { Player = player, UnitId = unit, Target = target }),
        "trade_mission" => ("Trade mission", new TradeGreatPersonCommand { Player = player, UnitId = unit, Target = target }),
        _ => null,
    };
}
