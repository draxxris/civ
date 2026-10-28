using System.Reflection;
using System.Runtime.CompilerServices;
using Ariel.Game;

namespace Ariel.Mods.Tests;

/// <summary>
/// Mods load from their payload directories into their own load context, so a test cannot bind a planner at
/// compile time. Adding the project reference loads a second copy of <c>Ariel.Mods.Ai</c> beside the payload
/// one: the reference was enabled once to check, and <c>typeof(AiContext).Assembly</c> and the assembly of the
/// registered <c>ai:standard</c> came back as different instances, which would have made the tests exercise
/// code the game never runs. The payload copy is the correct subject, so binding stays by name, and every
/// name goes through here: a renamed planner then reports the name it could not find instead of surfacing as
/// a NullReferenceException somewhere in an unrelated assertion.
/// </summary>
internal static class AiBindings
{
    private const string Namespace = "Ariel.Mods.Ai";

    private static Assembly Ai(Simulation sim) =>
        sim.Content.PlayerAIs.Get("ai:standard").GetType().Assembly;

    internal static Type AiType(Simulation sim, string name)
    {
        var assembly = Ai(sim);
        return assembly.GetType($"{Namespace}.{name}") ?? throw new InvalidOperationException(
            $"{Namespace}.{name} is not in {assembly.GetName().Name}. A renamed planner needs its binding renamed here. "
            + $"Types now: {TypeNames(assembly)}");
    }

    internal static object NewAi(Simulation sim, string name, params object[] args)
    {
        var type = AiType(sim, name);
        try
        {
            return Activator.CreateInstance(type, args)
                ?? throw new InvalidOperationException($"{type.FullName} constructed to null");
        }
        catch (MissingMethodException)
        {
            throw new InvalidOperationException(
                $"{type.FullName} has no constructor taking ({string.Join(", ", args.Select(a => a?.GetType().Name ?? "null"))}). "
                + $"Constructors now: {string.Join("; ", type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(c => $"({string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name))})"))}");
        }
    }

    internal static object? Call(object target, string name, params object[] args) => MethodOf(target, name).Invoke(target, args);

    internal static T Property<T>(object target, string name) => (T)PropertyOf(target, name).GetValue(target)!;

    internal static void Set(object target, string name, object? value) => PropertyOf(target, name).SetValue(target, value);

    internal static T Method<T>(object target, string name) where T : Delegate => MethodOf(target, name).CreateDelegate<T>(target);

    private static MethodInfo MethodOf(object target, string name) =>
        target.GetType().GetMethod(name) ?? throw Missing(target.GetType(), name, "method");

    private static PropertyInfo PropertyOf(object target, string name) =>
        target.GetType().GetProperty(name) ?? throw Missing(target.GetType(), name, "property");

    private static InvalidOperationException Missing(Type type, string name, string kind) => new(
        $"{type.FullName}.{name} is not a public {kind}. A rename needs its binding renamed here. "
        + $"{kind} names now: {MemberNames(type, kind)}");

    private static string MemberNames(Type type, string kind) => string.Join(", ", type
        .GetMembers(BindingFlags.Instance | BindingFlags.Public)
        .Where(m => kind == "method" ? m is MethodInfo : m is PropertyInfo)
        .Select(m => m.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    /// <summary>Names for an error message, so it must not throw itself: a payload type that fails to load
    /// would otherwise replace the intended message with a ReflectionTypeLoadException.</summary>
    private static string TypeNames(Assembly assembly)
    {
        Type?[] types;
        try { types = assembly.GetTypes(); }
        catch (ReflectionTypeLoadException failed) { types = failed.Types; }
        return string.Join(", ", types
            .Where(t => t is { IsNested: false } && t.Namespace?.StartsWith(Namespace, StringComparison.Ordinal) == true
                && !t.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .Select(t => t!.Name).Order(StringComparer.Ordinal));
    }
}
