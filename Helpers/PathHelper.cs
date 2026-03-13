using Spectre.Console;

namespace FileSearchEngine.Helpers;

public static class PathHelper
{
    // public readonly string[] DefaultSearchPaths;
    // private static List<FileResult> _fileIndex = [];
    // private readonly Lock _indexLock;
    private static readonly int MaxIndexFiles = 100000;

    private static readonly string[] ExcludedDirectories = 
    [
        "node_modules", ".git", "bin", "obj", "vendor", ".svn", ".hg", 
        "packages", ".idea", ".vs", "dist", "build", "out", "target"
    ];

    private static List<string> _customPaths = [];
    
    public static async Task<int> InitializePaths(string[] args, Config config)
    {
        // var config = AppConfig.Load();
        _customPaths = config.CustomPaths;

        if (args.Length > 0 && args[0] == "add-path")
        {
            if (args.Length > 1)
            {
                var newPath = string.Join(" ", args.Skip(1));
                if (Directory.Exists(newPath))
                {
                    if (!_customPaths.Contains(newPath))
                    {
                        _customPaths.Add(newPath);
                        config.CustomPaths = _customPaths;
                        AppConfig.Save(config);
                        AnsiConsole.MarkupLine($"[green]Added path:[/] {newPath}");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow]Path already exists:[/] {newPath}");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Directory does not exist:[/] {newPath}");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Usage: filesearch add-path <path>[/]");
            }
            return 0;
        }

        if (args.Length > 0 && args[0] == "list-paths")
        {
            AnsiConsole.MarkupLine("[bold]Default Search Paths:[/]");
            foreach (var p in Global.DefaultSearchPaths.Where(Directory.Exists))
            {
                AnsiConsole.MarkupLine($"  [blue]{p}[/]");
            }
            if (_customPaths.Count > 0)
            {
                AnsiConsole.MarkupLine("[bold]Custom Search Paths:[/]");
                foreach (var p in _customPaths.Where(Directory.Exists))
                {
                    AnsiConsole.MarkupLine($"  [green]{p}[/]");
                }
            }
        }

        return 0;
    }
    
    public static async Task<List<FileResult>> InitializeIndex(Config config)
    {
        var loadedIndex = AppConfig.LoadIndex();
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
                .SpinnerStyle(Color.Yellow)
                .StartAsync("Building file index...", async ctx =>
                {
                    await BuildIndexAsync(config);
                });
            
            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            int fileCount, dirCount;
            lock (Global.IndexLock)
            {
                fileCount = Global.FileIndex.Count(f => !f.IsDirectory);
                dirCount = Global.FileIndex.Count(f => f.IsDirectory);
            }
            AnsiConsole.MarkupLine($"[green]✓ Indexed {fileCount} files, {dirCount} folders in {elapsed:F1}s[/]");
            
            AppConfig.SaveIndex(Global.FileIndex);
            AnsiConsole.MarkupLine("[dim]Index saved to disk[/]");
        }

        return Global.FileIndex;
    }
    
    public static Task BuildIndexAsync(Config config)
    {
        return Task.Run(() =>
        {
            var newIndex = new List<FileResult>();
            
            var searchPaths = Global.DefaultSearchPaths
                .Where(Directory.Exists)
                .Concat(_customPaths.Where(Directory.Exists))
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
                        catch(Exception ex)
                        {
                            SentrySdk.CaptureException(ex);
                        }
                    }
                }
                catch(Exception ex)
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