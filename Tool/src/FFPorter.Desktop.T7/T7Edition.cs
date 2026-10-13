using System.IO;
using FFPorter.Desktop.Models;

namespace FFPorter.Desktop;

public sealed class T7Edition : Edition
{
    private static readonly byte[] ListKey = [0x5A, 0x3C, 0x91, 0xE7, 0x1D, 0x68, 0xB4, 0x2F, 0xC6, 0x0B, 0x7E, 0xD3, 0x49, 0xA0, 0x85, 0x16, 0x73, 0xEE, 0x28, 0x9B, 0x04, 0xBD, 0x61, 0xF2];
    private static readonly string List = Reveal("Mkjll25SmwCgO07mZ8LkdRiMRPp+2APAdF/+ijIO3UOjJDyceo3GYwCaR/Z3khXFdV/wk3wE20joYQ28Jw==");

    public override string Codename => "t7";
    public override string Title => "PS4 .FF Porter — Black Ops III";
    public override string Subtitle => "Drop maps, mods, weapons or zones to convert them for PS4.";
    public override string Author => "ItsJokerZz";
    public override string GameName => "Black Ops III";
    public override string GameLabel => "BO3";
    public override string SettingsFile => "ui.t7.json";
    public override string GameFolderName => "Black Ops III game files";
    public override string GameFolderHint => "Pick the Black Ops III folder, or the zone folder inside it.";
    public override string GameFolderOption => "--pc-reference";
    public override string CommunityList => List;
    public override string UpdateSource => "ItsJokerZz/PS4-BO3-Customs";
    public override string ConsoleArchive => "Console.zip";
    public override string ConsolePlugin => "BO3-Customs.sprx";

    public override string? ConsoleFolder(Job job) =>
        job.Mod != null ? $"mods/{job.Mod}" : job.Kind == "Map" ? $"usermaps/{job.Name}" : null;

    public override string? ConsoleFolder(string name, bool mod) =>
        mod ? $"mods/{name}" : KindOf("", name) == "Map" ? $"usermaps/{name}" : null;

    public override bool IsConverted(string zone)
    {
        byte[] head = Fastfiles.Head(zone, 16);
        return Fastfiles.Game(head) == GameName && head.Length >= 16 && head[14] != 0;
    }

    public override bool FindsInstalled => true;

    public override IReadOnlyList<Installed> FindInstalled(string? gameFolder)
    {
        var found = new List<Installed>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in Core.T7.T7Paths.ContentRoots(gameFolder))
        {
            try
            {
                foreach (string folder in Directory.GetDirectories(root))
                {
                    if (seen.Add(folder) && Describe(folder) is { } item)
                        found.Add(item);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }
        return [.. found.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)];
    }

    private Installed? Describe(string folder)
    {
        try
        {
            string[] places = [.. ((string[])[folder, Path.Combine(folder, "zone")]).Where(Directory.Exists)];
            var zones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in places.SelectMany(place => Directory.EnumerateFiles(place, "*.ff")))
                zones.TryAdd(Path.GetFileNameWithoutExtension(file), file);
            string[] mains = [.. zones.Keys
                .Where(stem => !(stem.Length > 3 && stem[2] == '_' && char.IsAsciiLetter(stem[0]) && char.IsAsciiLetter(stem[1]) && zones.ContainsKey(stem[3..])) && Accepts(zones[stem], out _))
                .Order(StringComparer.OrdinalIgnoreCase)];
            if (mains.Length == 0)
                return null;
            string? mod = mains.FirstOrDefault(stem => KindOf(Path.GetDirectoryName(zones[stem])!, stem) == "Mod");
            string? map = mains.FirstOrDefault(stem => KindOf(Path.GetDirectoryName(zones[stem])!, stem) == "Map");
            string name = mod != null ? ModName(Path.GetDirectoryName(zones[mod])!) : map ?? Path.GetFileName(folder);
            string type = mod != null ? CommunityKinds.Mod
                : map == null ? ""
                : map.StartsWith("zm_", StringComparison.OrdinalIgnoreCase) ? CommunityKinds.Zombies
                : map.StartsWith("mp_", StringComparison.OrdinalIgnoreCase) ? CommunityKinds.Multiplayer
                : CommunityKinds.Campaign;
            string kind = type switch
            {
                CommunityKinds.Mod => "Mod",
                CommunityKinds.Zombies => "Zombies map",
                CommunityKinds.Multiplayer => "Multiplayer map",
                CommunityKinds.Campaign => "Campaign map",
                _ => "Zone",
            };
            string? picture = places.SelectMany(place => ((string[])["previewimage.png", "previewimage.jpg", "loadingimage.png", "loadingimage.jpg"]).Select(file => Path.Combine(place, file)))
                .FirstOrDefault(File.Exists);
            long size = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);
            return new Installed(WorkshopText(places, "Title") is { } title && title.Trim().Length > 0 ? title.Trim() : name, kind, name, folder, picture, size, type);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public override void PrepareTools(Action<string> log) =>
        Core.T7.Scripts.ActsInstall.Ensure(Core.Workspace.Locate(), log);

    public override IReadOnlyList<string> CacheFolders()
    {
        string work = Core.T7.T7Paths.Work(Core.Workspace.Locate());
        return Directory.Exists(work) ? Directory.GetDirectories(work) : [];
    }

    public override string CacheFolder(Job job) => Path.Combine(Core.T7.T7Paths.Work(Core.Workspace.Locate()), job.Key);

    public override bool IsGameFolder(string folder) => File.Exists(Path.Combine(folder, "base.xpak"));

    public override double? EstimateSeconds(Job job, bool sideBySide)
    {
        try
        {
            string work = Core.T7.T7Paths.Work(Core.Workspace.Locate());
            return Core.T7.Port.T7Timing.Load(Core.T7.Port.T7Timing.PathIn(work)).Before(new Core.T7.Port.T7Timing.Request(job.MainFile, Path.Combine(work, job.Key),
                Concurrent: sideBySide, ConvertedBefore: job.IsEarlier || job.Report != null));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    public override Originals FindOriginals(Job job)
    {
        Core.T7.Port.T7MapPort.Originals found = Core.T7.Port.T7MapPort.FindOriginals(job.MainFile);
        return new Originals(found.Files, found.PackageFiles, found.Root);
    }

    public override bool TryGameFolder(string chosen, out string? folder)
    {
        folder = null;
        if (string.IsNullOrWhiteSpace(chosen))
            return false;
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(chosen));
        foreach (string candidate in (string[])[root, Path.Combine(root, "zone")])
        {
            if (IsGameFolder(candidate))
            {
                folder = candidate;
                return true;
            }
        }
        return false;
    }

    public override bool Accepts(string file, out string? why)
    {
        why = null;
        byte[] head = Fastfiles.Head(file, 16);
        if (Fastfiles.Game(head) != GameName)
        {
            why = head.Length == 0 ? "the file could not be read" : Fastfiles.Skipped(head, GameName);
            return false;
        }
        if (head.Length < 16 || head[14] != 0)
        {
            why = "a Black Ops III fastfile that is not a PC zone (already converted?)";
            return false;
        }
        return true;
    }

    public override IEnumerable<Job> Jobs(IReadOnlyList<string> files)
    {
        static string? BaseOf(string file)
        {
            string stem = Path.GetFileNameWithoutExtension(file);
            string folder = Path.GetDirectoryName(file)!;
            return stem.Length > 3 && stem[2] == '_' && char.IsAsciiLetter(stem[0]) && char.IsAsciiLetter(stem[1]) && File.Exists(Path.Combine(folder, stem[3..] + ".ff"))
                ? Path.Combine(folder, stem[3..] + ".ff")
                : null;
        }
        var mains = new SortedSet<string>(files.Select(file => BaseOf(file) ?? file), StringComparer.OrdinalIgnoreCase);
        foreach (string main in mains)
        {
            string folder = Path.GetDirectoryName(main)!;
            string stem = Path.GetFileNameWithoutExtension(main);
            var localized = Directory.EnumerateFiles(folder, "??_" + stem + ".ff").Where(f => BaseOf(f) != null).Order(StringComparer.OrdinalIgnoreCase).ToList();
            var streams = Directory.EnumerateFiles(folder, "*.xpak")
                .Where(f => Path.GetFileNameWithoutExtension(f) is { } x && (x.Equals(stem, StringComparison.OrdinalIgnoreCase) || x.EndsWith("_" + stem, StringComparison.OrdinalIgnoreCase)
                    || x.Equals(stem + "_d", StringComparison.OrdinalIgnoreCase) || x.EndsWith("_" + stem + "_d", StringComparison.OrdinalIgnoreCase)))
                .ToList();
            string sound = Path.Combine(folder, "snd");
            var banks = Directory.Exists(sound) ? Directory.EnumerateFiles(sound, stem + ".*.sab*", SearchOption.AllDirectories).ToList() : [];
            long length = new[] { main }.Concat(localized).Concat(streams).Concat(banks).Sum(f => new FileInfo(f).Length);
            string kind = KindOf(folder, stem);
            yield return new Job
            {
                Kind = kind, Name = stem, MainFile = main, CompanionZones = localized, Mod = kind == "Mod" ? ModName(folder) : null,
                CompanionSummary = $"{localized.Count} language zone{(localized.Count == 1 ? "" : "s")}",
                Streams = streams.Count, SoundBanks = banks.Count, Length = length,
            };
        }
    }

    private static string KindOf(string folder, string stem)
    {
        string lower = stem.ToLowerInvariant();
        return folder.Split(Path.DirectorySeparatorChar).Contains("mods", StringComparer.OrdinalIgnoreCase) || lower.EndsWith("_mod") ? "Mod"
            : (lower.StartsWith("zm_") || lower.StartsWith("mp_") || lower.StartsWith("cp_")) && !lower.Contains("common") && !lower.EndsWith("_patch") ? "Map"
            : "Zone";
    }

    private static string ModName(string folder)
    {
        string root = Path.GetFileName(folder).Equals("zone", StringComparison.OrdinalIgnoreCase) && Path.GetDirectoryName(folder) is { Length: > 0 } parent ? parent : folder;
        string name = string.Concat((WorkshopText([folder, root], "FolderName") ?? Path.GetFileName(root)).Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c))
            .Trim('.', ' ');
        return name.Length > 0 ? name : "mod";
    }

    private static string? WorkshopText(IEnumerable<string> places, string property)
    {
        foreach (string place in places)
        {
            try
            {
                string file = Path.Combine(place, "workshop.json");
                if (!File.Exists(file))
                    continue;
                using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
                if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                    && document.RootElement.TryGetProperty(property, out System.Text.Json.JsonElement value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return value.GetString();
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
            }
        }
        return null;
    }

    private static string Reveal(string text)
    {
        byte[] data = Convert.FromBase64String(text);
        for (int index = 0; index < data.Length; index++)
            data[index] ^= ListKey[index % ListKey.Length];
        return System.Text.Encoding.UTF8.GetString(data);
    }

    public override int RunBackend(string[] arguments, TextWriter output, TextWriter error) =>
        Cli.T7Cli.Run(arguments.Length > 0 && arguments[0] == Codename ? arguments[1..] : arguments, output, error);
}
