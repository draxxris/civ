using Ariel.Game;
using S = Ariel.Content.Schema;

namespace Ariel.Rules.World;

/// <summary>Plot changes after the map exists. Each change settles cliffs, heights and the feature before
/// <see cref="TerrainChanged"/> goes out; areas and lake flags are rebuilt once per pass, because nothing a change
/// settles reads them.</summary>
internal sealed class TerrainChanges : ITerrainChanges
{
    public bool FeatureFits(Simulation sim, TileId tile, string feature) =>
        Placement.FeatureValid(sim.State.Grid, sim.State.Plots, sim.Content, tile, sim.Content.Get<S.FeatureDef>(feature));

    public ITerrainChangePass Begin(Simulation sim) => new Pass(sim);

    private sealed class Pass(Simulation sim) : ITerrainChangePass
    {
        private bool _reshaped;

        public bool Change(TileId tile, string terrain, string? feature)
        {
            var plot = sim.State.Plots[tile.Value];
            string previousTerrain = plot.Terrain;
            string? previousFeature = plot.Feature;
            if (terrain == previousTerrain && feature == previousFeature) return false;
            plot.Terrain = terrain;
            plot.Feature = feature;
            CliffRules.AfterTerrainChange(sim.State, sim.Content, tile);
            if (plot.Feature is { } remaining && !Placement.FeatureValid(sim.State.Grid, sim.State.Plots, sim.Content, tile, sim.Content.Get<S.FeatureDef>(remaining)))
                plot.Feature = null;
            _reshaped |= Geography.ChangesAreas(sim.Content, previousTerrain, plot.Terrain);
            sim.Events.Publish(new TerrainChanged(tile, previousTerrain, plot.Terrain, previousFeature, plot.Feature));
            return true;
        }

        public void Dispose()
        {
            if (!_reshaped) return;
            _reshaped = false;
            Geography.AssignAreas(sim.State.Grid, sim.State.Plots, sim.Content);
        }
    }
}
