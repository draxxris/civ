using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Ariel.Client;

/// <summary>
/// Detects mod binaries compiled against the assembly names this project used before its namespaces were renamed.
/// Those binaries cannot bind to the current core assembly, so loading them would abort a normal startup.
/// </summary>
public static class LegacyModuleCheck
{
    /// <summary>The assembly-name stem the project used before the rename; every pre-rename core assembly starts with it.</summary>
    public const string PreRenameStem = "Civ";

    /// <summary>True when the file's assembly reference table names a pre-rename assembly. Unreadable files are not
    /// claimed as legacy: the loader reports them through its own error path.</summary>
    public static bool RequiresPreRenameCore(string assemblyPath)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata) return false;
            var reader = pe.GetMetadataReader();
            foreach (var handle in reader.AssemblyReferences)
                if (IsPreRenameName(reader.GetString(reader.GetAssemblyReference(handle).Name))) return true;
            return false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>True for the renamed stem itself and for every namespaced assembly under it, and nothing else:
    /// a domain assembly such as CivicsTools or an unrelated Civ4 tool stays valid.</summary>
    public static bool IsPreRenameName(string assemblyName) =>
        string.Equals(assemblyName, PreRenameStem, StringComparison.Ordinal)
        || assemblyName.StartsWith(PreRenameStem + ".", StringComparison.Ordinal);
}
