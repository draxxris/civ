using Ariel.Content.Schema;
using Ariel.Modding;

namespace Ariel.Rules;

/// <summary>How a base module or a mod adds kinds of memory: a <c>data/memory-kind.json</c> array of
/// <see cref="MemoryKindDef"/> in its own namespace, loaded through here so every kind is checked the same way.</summary>
public static class MemoryKinds
{
    public static void Load(IModContext context, ModJsonFile<MemoryKindDef[]> file)
    {
        var registry = context.Content<MemoryKindDef>();
        foreach (var kind in file.Read(context.Data))
        {
            if (kind.Decay < 1 || kind.Limit < 0)
                throw new InvalidDataException($"{kind.Id}: a memory must take at least one turn to fade and its limit must not be negative");
            registry.Register(kind);
        }
    }
}
