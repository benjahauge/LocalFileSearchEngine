using System.Text.Json;
using FuzzySharp;
using Spectre.Console;

namespace FileSearchEngine.Services;

public static class IndexService
{
    private static readonly int MaxIndexFiles = 100000;

    private static readonly string[] ExcludedDirectories =
    [
        "node_modules", ".git", "bin", "obj", "vendor", ".svn", ".hg",
        "packages", ".idea", ".vs", "dist", "build", "out", "target"
    ];

    private static readonly string AppDir = "/Users/benjahauge/Projects/PrivateDev/DotNet/.filesearch";
    private static readonly string IndexFile = Path.Combine(AppDir, "index.json");

    public static async Task<List<FileResult>> LoadOrBuildAsync(Config config)
    {
        var loadedIndex = LoadFromDisk();
        if (loadedIndex != null && loadedIndex.Count > 0)
        {
            lock (Global.IndexLock)
            {
                Global.FileIndex = loadedIndex;
            }
            int fileCount, dirCount;
            lock (Global.IndexLock)
            {
                fileCount = Global.FileIndex.Count(f => !f.IsDirectory);
                dirCount = Global.FileIndex.Count(f => f.IsDirectory);
            }
            AnsiConsole.MarkupLine($"[green]✓ Loaded {fileCount} files, {dirCount} folders from cache[/]");
        }
        else
        {
            var startTime = DateTime.Now;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Star)
                .SpinnerStyle(Spectre.Console.Color.Yellow)
                .StartAsync("Building file index...", async ctx =>
                {
                    await BuildAsync(config);
                });

            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            int fileCount, dirCount;
            lock (Global.IndexLock)
            {
                fileCount = Global.FileIndex.Count(f => !f.IsDirectory);
                dirCount = Global.FileIndex.Count(f => f.IsDirectory);
            }
            AnsiConsole.MarkupLine($"[green]✓ Indexed {fileCount} files, {dirCount} folders in {elapsed:F1}s[/]");

            SaveToDisk(Global.FileIndex);
            AnsiConsole.MarkupLine("[dim]Index saved to disk[/]");
        }

        return Global.FileIndex;
    }

    public static Task BuildAsync(Config config)
    {
        return Task.Run(() =>
        {
            var newIndex = new List<FileResult>();

            var searchPaths = Global.DefaultSearchPaths
                .Where(Directory.Exists)
                .Concat(config.CustomPaths.Where(Directory.Exists))
                .Select(p => p.TrimEnd(Path.DirectorySeparatorChar))
                .Distinct()
                .ToList();

            foreach (var path in searchPaths)
            {
                if (newIndex.Count >= MaxIndexFiles) break;

                try
                {
                    var dirs = Directory.EnumerateDirectories(path, "*", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        AttributesToSkip = FileAttributes.System
                    });

                    foreach (var dir in dirs)
                    {
                        if (newIndex.Count >= MaxIndexFiles) break;

                        try
                        {
                            if (IsPathExcluded(dir)) continue;

                            var info = new DirectoryInfo(dir);
                            newIndex.Add(new FileResult
                            {
                                FullPath = dir,
                                FileName = Path.GetFileName(dir),
                                Directory = Path.GetDirectoryName(dir) ?? "",
                                Size = 0,
                                Modified = info.LastWriteTime,
                                Score = 100,
                                IsDirectory = true
                            });
                        }
                        catch (Exception ex)
                        {
                            SentrySdk.CaptureException(ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SentrySdk.CaptureException(ex);
                }
            }

            foreach (var path in searchPaths)
            {
                if (newIndex.Count >= MaxIndexFiles) break;

                try
                {
                    var files = Directory.EnumerateFiles(path, "*", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        AttributesToSkip = FileAttributes.System
                    });

                    foreach (var file in files)
                    {
                        if (newIndex.Count >= MaxIndexFiles) break;

                        try
                        {
                            if (IsPathExcluded(file)) continue;

                            var info = new FileInfo(file);
                            newIndex.Add(new FileResult
                            {
                                FullPath = file,
                                FileName = Path.GetFileName(file),
                                Directory = Path.GetDirectoryName(file) ?? "",
                                Size = info.Length,
                                Modified = info.LastWriteTime,
                                Score = 100,
                                IsDirectory = false
                            });
                        }
                        catch (Exception ex)
                        {
                            SentrySdk.CaptureException(ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SentrySdk.CaptureException(ex);
                }
            }

            lock (Global.IndexLock)
            {
                Global.FileIndex = newIndex;
            }
        });
    }

    public static void SaveToDisk(List<FileResult> index)
    {
        try
        {
            Directory.CreateDirectory(AppDir);
            var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(IndexFile, json);
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
        }
    }

    public static List<FileResult>? LoadFromDisk()
    {
        try
        {
            if (File.Exists(IndexFile))
            {
                var json = File.ReadAllText(IndexFile);
                return JsonSerializer.Deserialize<List<FileResult>>(json);
            }
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
        }
        return null;
    }

    private static bool IsPathExcluded(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar);
        foreach (var part in parts)
        {
            if (part.StartsWith(".")) return true;
            if (ExcludedDirectories.Contains(part, StringComparer.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
