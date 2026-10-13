using Civ.Content;
using Civ.Modding;

namespace Civ.Mods;

/// <summary>Shared sidecar registration, linked into each independent mod assembly.</summary>
internal static class ContentRegistration
{
    public static void Load<T>(IModContext context, string file) where T : ContentDef
    {
        var registry = context.Content<T>();
        foreach (var definition in context.Data.ReadJson<T[]>("data/" + file)) registry.Register(definition);
    }

    public static void Evaluator<T>(IModContext context, string kind, Func<Requirement.Leaf, RequirementContext, bool>? predicate = null) where T : ContentDef =>
        context.RegisterService<IRequirementEvaluator>(kind + "_requirements", new PartialEvaluator<T>(kind, predicate));

    private sealed class PartialEvaluator<T>(string kind, Func<Requirement.Leaf, RequirementContext, bool>? predicate) : IContentRequirementEvaluator where T : ContentDef
    {
        public string Kind => kind;
        public Type DefinitionType => typeof(T);
        public string? Evaluate(Requirement.Leaf leaf, RequirementContext context)
        {
            // TODO: owning feature rules must implement missing state and scope semantics. Never grant an unavailable mechanic.
            if (predicate is null)
            {
                if (kind is "condition" or "culture-level" or "era")
                {
                    var service = context.Sim.Content.Services.Get<Civ.Game.ICityRequirementService>().SingleOrDefault();
                    if (service is not null) return service.Evaluate(leaf, context);
                }
                return $"Requirement '{kind}' is unavailable until its owning rules implement runtime state";
            }
            return predicate(leaf, context) ? null : $"Required {kind}: {leaf.Id}";
        }
    }
}
