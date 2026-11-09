using Ariel.SyncCheck;

// Headless multiplayer checks. Each mode prints results to stdout and diagnostics to stderr.
var root = AppContext.BaseDirectory;
Environment.ExitCode = args.FirstOrDefault() switch
{
    "hash-trace" => HashTrace.Run(args[1..], Mods.Load(root, args)),
    "diff-saves" => SaveDiff.Run(args[1..]),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: Ariel.SyncCheck diff-saves A.json B.json [--max N]");
    Console.Error.WriteLine("usage: Ariel.SyncCheck hash-trace [--seed N] [--players N] [--turns N] [--reload-every N] [--world id] [--turn-mode simultaneous|dynamic|sequential] [--background] [--optional A,B]");
    return 2;
}
