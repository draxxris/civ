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

    /// <summary>Register the evaluator of a leaf kind whose ids name definitions of <typeparamref name="T"/>. With a
    /// <paramref name="predicate"/> the leaf passes when it is true; <paramref name="describe"/>, when given, says what
    /// the leaf asks for (a phrase completing "Requires ...", null to fall back to the kind and id), and a failed leaf
    /// is explained with it.</summary>
    public static void Evaluator<T>(IModContext context, string kind, Func<Requirement.Leaf, RequirementContext, bool>? predicate = null,
        Func<Requirement.Leaf, ContentDb, string?>? describe = null) where T : ContentDef =>
        context.RegisterService<IRequirementEvaluator>(kind + "_requirements", new PartialEvaluator<T>(kind, predicate, describe));

    private sealed class PartialEvaluator<T>(string kind, Func<Requirement.Leaf, RequirementContext, bool>? predicate,
        Func<Requirement.Leaf, ContentDb, string?>? describe) : IContentRequirementEvaluator, IDescribedRequirementEvaluator where T : ContentDef
    {
        public string Kind => kind;
        public Type DefinitionType => typeof(T);
        public string? Describe(Requirement.Leaf leaf, ContentDb content) => describe?.Invoke(leaf, content);
        public string? Evaluate(Requirement.Leaf leaf, RequirementContext context)
        {
            if (predicate is null)
            {
                // City rules own these kinds. Any other kind without a predicate has no runtime state yet and fails
                // closed, so an unavailable mechanic is never granted.
                if (kind is "condition" or "culture-level" or "era") return context.Sim.Rules().CityRequirements.Evaluate(leaf, context);
                return $"Requirement '{kind}' is unavailable until its owning rules implement runtime state";
            }
            if (predicate(leaf, context)) return null;
            return Describe(leaf, context.Sim.Content) is { } phrase ? "Requires " + phrase : $"Required {kind}: {leaf.Id}";
        }
    }
}
