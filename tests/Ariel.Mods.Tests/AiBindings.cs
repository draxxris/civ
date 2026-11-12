using System.Reflection;
using System.Runtime.CompilerServices;
using Ariel.Game;

namespace Ariel.Mods.Tests;

/// <summary>
/// The planners are internal to <c>Ariel.Ai</c>, so tests bind them by name, and every name goes through here: a
/// renamed planner then reports the name it could not find instead of surfacing as a NullReferenceException somewhere
/// in an unrelated assertion. The assembly is the one the registered <c>ai:standard</c> came from, the host's copy.
/// </summary>
internal static class AiBindings
{
    private const string Namespace = "Ariel.Ai";

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
            return Activator.CreateInstance(type, Members, binder: null, args, culture: null)
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

    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static MethodInfo MethodOf(object target, string name) =>
        target.GetType().GetMethod(name, Members) ?? throw Missing(target.GetType(), name, "method");

    private static PropertyInfo PropertyOf(object target, string name) =>
        target.GetType().GetProperty(name, Members) ?? throw Missing(target.GetType(), name, "property");

    private static InvalidOperationException Missing(Type type, string name, string kind) => new(
        $"{type.FullName}.{name} is not a {kind}. A rename needs its binding renamed here. "
        + $"{kind} names now: {MemberNames(type, kind)}");

    private static string MemberNames(Type type, string kind) => string.Join(", ", type
        .GetMembers(Members)
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
