using System.Runtime.ExceptionServices;

namespace Civ.Modding;

/// <summary>Optional typed sidecar inventory. Register still runs once, serially, in dependency order.</summary>
/// <remarks>
/// Return descriptors only: do not read data, change instance state or access registries here.
/// The loader parses these files in parallel into build-local storage. Read failures are rethrown
/// at the matching ReadJson call, so earlier registration failures retain precedence.
/// Each file/type pair is read once per build; repeated reads share that build's parsed value.
/// Sidecars must remain unchanged during Build. ReadText and Exists still read the live source.
/// An inventory getter failure is instead rethrown before this mod's Register starts.
/// </remarks>
public interface IPreparableMod : IMod
{
    /// <summary>Files to parse before registration. Unlisted reads keep their usual serial behavior.</summary>
    IReadOnlyList<ModJsonFile> PreparationFiles { get; }
}

/// <summary>Immutable file/type descriptor; contains no parsed data and can be reused across builds.</summary>
public abstract class ModJsonFile
{
    private protected ModJsonFile(string path, Type type) { Path = path; Type = type; }
    /// <summary>Path relative to the mod root.</summary>
    public string Path { get; }
    /// <summary>JSON result type.</summary>
    public Type Type { get; }
    internal abstract object Load(IModData data);
}

/// <summary>A typed sidecar descriptor shared by the preparation inventory and registration code.</summary>
public sealed class ModJsonFile<T>(string path) : ModJsonFile(path, typeof(T))
{
    /// <summary>Read this file, using the build-local prepared value when available.</summary>
    public T Read(IModData data) => data.ReadJson<T>(Path);
    internal override object Load(IModData data) => Read(data)!;
}

/// <summary>Owns parsed values and failures for one mod in one Build invocation.</summary>
internal sealed class PreparedModData(IModData source) : IModData
{
    private readonly Dictionary<(string Path, Type Type), PreparedJson> _files = new();
    private ExceptionDispatchInfo? _inventoryError;

    public static PreparedModData[] Prepare(IReadOnlyList<ModSource> mods)
    {
        using var trace = StartupTrace.Phase("mod preparation");
        var readers = new PreparedModData[mods.Count];
        var work = new List<PreparedJson>();
        for (int i = 0; i < mods.Count; i++)
        {
            readers[i] = new PreparedModData(new DirectoryModData(mods[i].Root));
            readers[i].Collect(mods[i].Instance, work);
        }
        // JSON parsing allocates heavily. Two workers limit GC contention and peak live allocations.
        Parallel.ForEach(work, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 2) },
            file => file.Load());
        return readers;
    }

    private void Collect(IMod mod, List<PreparedJson> work)
    {
        if (mod is not IPreparableMod preparable) return;
        try
        {
            foreach (var file in preparable.PreparationFiles)
            {
                var prepared = new PreparedJson(file, source);
                if (_files.TryAdd((file.Path, file.Type), prepared)) work.Add(prepared);
            }
        }
        catch (Exception error)
        {
            _inventoryError = ExceptionDispatchInfo.Capture(error);
        }
    }

    public void ThrowInventoryError() => _inventoryError?.Throw();
    public bool Exists(string path) => source.Exists(path);
    public string ReadText(string path) => source.ReadText(path);
    public T ReadJson<T>(string path) => _files.TryGetValue((path, typeof(T)), out var file)
        ? file.Read<T>() : source.ReadJson<T>(path);

    private sealed class PreparedJson(ModJsonFile file, IModData source)
    {
        private object? _value;
        private ExceptionDispatchInfo? _error;

        public void Load()
        {
            try { _value = file.Load(source); }
            // Do not let a later file's parse failure mask an earlier serial registration failure.
            catch (Exception error) { _error = ExceptionDispatchInfo.Capture(error); }
        }

        public T Read<T>()
        {
            _error?.Throw();
            return (T)_value!;
        }
    }
}
