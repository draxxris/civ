using Ariel.Content;
using Ariel.Game;
using Ariel.Modding;

namespace Ariel.Rules;

/// <summary>Shared sidecar registration for base modules and mods.</summary>
public static class ContentRegistration
{
    public static void Load<T>(IModContext context, ModJsonFile<T[]> file) where T : ContentDef
    {
        var registry = context.Content<T>();
        foreach (var definition in file.Read(context.Data)) registry.Register(definition);
    }

    public static void Evaluator<T>(IModContext context, string kind, Func<Requirement.Leaf, RequirementContext, bool>? predicate = null) where T : ContentDef =>
        context.RegisterService<IRequirementEvaluator>(kind + "_requirements", new PartialEvaluator<T>(kind, predicate));

    private sealed class PartialEvaluator<T>(string kind, Func<Requirement.Leaf, RequirementContext, bool>? predicate) : IContentRequirementEvaluator where T : ContentDef
    {
        public string Kind => kind;
        public Type DefinitionType => typeof(T);
        public string? Evaluate(Requirement.Leaf leaf, RequirementContext context)
        {
            if (predicate is null)
            {
                // City rules own these kinds. Any other kind without a predicate has no runtime state yet and fails
                // closed, so an unavailable mechanic is never granted.
                if (kind is "condition" or "culture-level" or "era") return context.Sim.Rules().CityRequirements.Evaluate(leaf, context);
                return $"Requirement '{kind}' is unavailable until its owning rules implement runtime state";
            }
            return predicate(leaf, context) ? null : $"Required {kind}: {leaf.Id}";
        }
    }
}
