using Ariel.SyncCheck;

// Headless multiplayer checks. Each mode prints results to stdout and diagnostics to stderr.
var root = AppContext.BaseDirectory;
Environment.ExitCode = args.FirstOrDefault() switch
{
    "hash-trace" => HashTrace.Run(args[1..], Mods.Load(root, args)),
    "corporations" => CorporationReport.Run(args[1..], Mods.Load(root, args)),
    "diff-saves" => SaveDiff.Run(args[1..]),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: Ariel.SyncCheck diff-saves A.json B.json [--max N]");
    Console.Error.WriteLine("usage: Ariel.SyncCheck corporations [--seed N] [--players N] [--turns N] [--world id] [--era id] [--optional A,B] [--without A,B] [--game-option ID=BOOL] [--load PATH [--force]] [--save PATH]");
    Console.Error.WriteLine("usage: Ariel.SyncCheck hash-trace [--seed N] [--players N] [--turns N] [--reload-every N] [--world id] [--era id] [--turn-mode simultaneous|dynamic|sequential] [--background] [--waves N|max] [--verify-read-epochs] [--verify-modifier-cache] [--optional A,B] [--without A,B] [--game-option ID=BOOL] [--load PATH] [--save PATH] [--profile DIR]");
    return 2;
}
