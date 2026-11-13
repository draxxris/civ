namespace Ariel.Ai;

/// <summary>The standard AI's own planners, registered through <see cref="IAiPlanner"/> like any contributed one.
/// Each entry is stateless; it builds its planner for the turn. Planners that share work within a turn (sites, the
/// army plan, the production plan) get it from the context, which creates it on first use.</summary>
internal static class StandardPlanners
{
    private sealed class Planner(string name, int order, Action<AiContext> plan) : IAiPlanner
    {
        public readonly string Name = name;
        public string Id { get; } = $"ai:{name}";
        public int Order => order;
        public void Plan(AiContext ctx) => plan(ctx);
    }

    private static readonly Planner[] All =
    [
        new Planner("research", AiPlannerOrder.Research, ctx => new ResearchPlanner(ctx).Plan()),
        new Planner("war", AiPlannerOrder.War, ctx => new WarPlanner(ctx, ctx.Army).Plan()),
        new Planner("civics", AiPlannerOrder.Civics, ctx => new CivicPlanner(ctx).Plan()),
        new Planner("settlers", AiPlannerOrder.Settlers, ctx => new SettlerPlanner(ctx, ctx.Sites).Plan()),
        new Planner("citizens", AiPlannerOrder.Citizens, ctx =>
        {
            ctx.Refresh();
            new CitizenPlanner(ctx, ctx.Army).Plan();
        }),
        new Planner("cities", AiPlannerOrder.Cities, ctx =>
        {
            ctx.Refresh();
            ctx.Production.Plan();
        }),
        new Planner("workers", AiPlannerOrder.Workers, ctx => new WorkerPlanner(ctx).Plan()),
        new Planner("air", AiPlannerOrder.Air, ctx => new AirPlanner(ctx).Plan()),
        new Planner("military", AiPlannerOrder.Military, ctx =>
            new MilitaryPlanner(ctx, ctx.Army, ctx.Production, new AttackPlanner(ctx, new UnitOrders(ctx))).Plan()),
        new Planner("economy", AiPlannerOrder.Economy, ctx => new EconomyPlanner(ctx).Plan()),
        new Planner("hurry", AiPlannerOrder.Hurry, ctx => new HurryPlanner(ctx, ctx.Production).Plan()),
    ];

    /// <summary>Register every built-in planner under the module's namespace.</summary>
    public static void Register(Ariel.Modding.IModContext context)
    {
        foreach (var planner in All) context.RegisterService<IAiPlanner>(planner.Name, planner);
    }
}
