using Civ.Content;
using Civ.Content.Schema;
using Civ.Game;

namespace Civ.Mods.Economy;

internal sealed class ProcessKind(ICityProcessService processes) : IProductionKind
{
    public string Id => "economy:process";
    public string Label => "Processes";
    public bool IsContinuous => true;
    public IEnumerable<ContentDef> Candidates(ContentDb content) => content.Get<ProcessDef>().All;
    public ContentDef Definition(ContentDb content, string id) => content.Get<ProcessDef>(id);
    public long BaseCost(ContentDef definition) => 0;
    public bool IsVisible(RequirementResult eligibility) => true;
    public RequirementResult Validate(Simulation sim, int city, string id) => processes.Validate(sim, city, id);
    public CityProductionResult Calculate(Simulation sim, int city, string id) =>
        sim.Content.Services.Get<ICityProductionService>().Single().Calculate(sim, city, Id, id);
}
