using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneMoreMove.Core;
using OneMoreMove.Core.Solving;
using OneMoreMove.Persistence;

namespace OneMoreMove.LevelLab
{
    public static class Program
    {
        private const string Usage =
            "LevelLab — level design tool on top of the game's RulesEngine\n\n" +
            "  analyze [path]                        Report every level JSON in a folder (default: the seed folder) or one file\n" +
            "  map <row> <row> ...                   Report one ASCII map (P E G # o x O X .)\n" +
            "  generate <profile> [--seed N] [--tries N] [--top N] [--min N] [--max N]\n" +
            "                                        Random search for candidate maps; profiles: " + "{0}\n";

        public static int Main(string[] args)
        {
            if (args.Length == 0) return Fail(string.Format(Usage, string.Join(", ", GeneratorProfile.All.Keys)));

            try
            {
                switch (args[0])
                {
                    case "analyze": return Analyze(args.Length > 1 ? args[1] : FindSeedFolder());
                    case "map": return AnalyzeMap(args.Skip(1).ToArray());
                    case "generate": return Generate(args.Skip(1).ToArray());
                    default: return Fail(string.Format(Usage, string.Join(", ", GeneratorProfile.All.Keys)));
                }
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is IOException)
            {
                return Fail(e.Message);
            }
        }

        private static int Analyze(string path)
        {
            var files = Directory.Exists(path)
                ? Directory.GetFiles(path, "*.json").OrderBy(p => p, StringComparer.Ordinal).ToArray()
                : new[] { path };

            foreach (var file in files)
            {
                var level = LevelJson.Parse(File.ReadAllText(file));
                var report = Report.Line(Path.GetFileNameWithoutExtension(file), level, LevelAnalyzer.Analyze(level));
                Console.WriteLine(report);
            }

            return 0;
        }

        private static int AnalyzeMap(string[] rows)
        {
            if (rows.Length == 0) return Fail("map needs at least one row.");
            var level = AsciiLevelParser.Parse(rows);
            Console.WriteLine(Report.Line("map", level, LevelAnalyzer.Analyze(level)));
            Console.WriteLine(Report.Board(level));
            return 0;
        }

        private static int Generate(string[] args)
        {
            if (args.Length == 0 || !GeneratorProfile.All.TryGetValue(args[0], out var profile))
                return Fail("Unknown profile. Profiles: " + string.Join(", ", GeneratorProfile.All.Keys));

            var options = ParseOptions(args.Skip(1));
            var seed = options.TryGetValue("seed", out var s) ? s : 1;
            var tries = options.TryGetValue("tries", out var t) ? t : 20000;
            var top = options.TryGetValue("top", out var k) ? k : 8;

            if (options.TryGetValue("min", out var min)) profile.Optimal = (min, profile.Optimal.Max);
            if (options.TryGetValue("max", out var max)) profile.Optimal = (profile.Optimal.Min, max);

            var candidates = LevelGenerator.Search(profile, seed, tries);
            foreach (var candidate in candidates.Take(top))
            {
                Console.WriteLine($"score {candidate.Score:0.0}  {Report.Line(args[0], candidate.Level, candidate.Analysis)}");
                Console.WriteLine("  \"map\": [" + string.Join(", ", AsciiLevelParser.Render(candidate.Level).Select(r => $"\"{r}\"")) + "]");
            }

            Console.WriteLine($"{candidates.Count} candidates from {tries} tries (seed {seed}).");
            return 0;
        }

        private static Dictionary<string, int> ParseOptions(IEnumerable<string> args)
        {
            var result = new Dictionary<string, int>();
            var list = args.ToList();
            for (var i = 0; i + 1 < list.Count; i += 2)
            {
                if (!list[i].StartsWith("--") || !int.TryParse(list[i + 1], out var value)) throw new ArgumentException($"Bad option '{list[i]}'.");
                result[list[i].Substring(2)] = value;
            }

            return result;
        }

        private static string FindSeedFolder()
        {
            for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "Assets", "_Project", "Content", "LevelSource");
                if (Directory.Exists(candidate)) return candidate;
            }

            throw new IOException("Seed folder not found; pass a path.");
        }

        private static int Fail(string message)
        {
            Console.Error.WriteLine(message);
            return 1;
        }
    }
}
