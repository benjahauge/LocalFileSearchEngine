using System.Text.Json;
using Spectre.Console;
using FuzzySharp;

namespace FileSearchEngine;

public class FileResult
{
    public string FullPath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Directory { get; set; } = "";
    public long Size { get; set; }
    public DateTime Modified { get; set; }
    public int Score { get; set; }
    public bool IsDirectory { get; set; }
}

public class Config
{
    public List<string> CustomPaths { get; set; } = [];
}

public class AppConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FileSearchEngine");
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

    public static Config Load()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                return JsonSerializer.Deserialize<Config>(json) ?? new Config();
            }
        }
        catch { }
        return new Config();
    }

    public static void Save(Config config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
        }
        catch { }
    }
}

public static class Program
{
    private static readonly string[] DefaultSearchPaths;
    private static readonly string[] ExcludedDirectories = 
    [
        "node_modules", ".git", "bin", "obj", "vendor", ".svn", ".hg", 
        "packages", ".idea", ".vs", "dist", "build", "out", "target"
    ];
    
    private static List<string> _customPaths = [];
    private static string _searchQuery = "";
    private static string _statusMessage = "";
    private static List<FileResult> _results = [];
    private static List<FileResult> _fileIndex = [];
    private static readonly object _indexLock = new();
    private static int _selectedIndex = 0;
    private static int _currentPage = 0;
    private static readonly int _pageSize = 10;
    private static bool _showFuzzy = false;
    private static bool _isAddPathMode = false;
    private static string _pendingPath = "";

    static Program()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        DefaultSearchPaths =
        [
            Path.Combine(home, "Documents"),
            Path.Combine(home, "Desktop"),
            Path.Combine(home, "Projects"),
        ];
    }

    public static async Task<int> Main(string[] args)
    {
        var config = AppConfig.Load();
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
            foreach (var p in DefaultSearchPaths.Where(Directory.Exists))
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
            return 0;
        }

        var startTime = DateTime.Now;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Star)
            .SpinnerStyle(Spectre.Console.Color.Yellow)
            .StartAsync("Building file index...", async ctx =>
            {
                await BuildIndexAsync(config);
            });
        
        var elapsed = (DateTime.Now - startTime).TotalSeconds;
        AnsiConsole.MarkupLine($"[green]✓ Indexed { _fileIndex.Count} files in {elapsed:F1}s[/]");
        
        while (true)
        {
            RenderHeader();
            
            if (_isAddPathMode)
            {
                RenderAddPathMode();
            }
            else
            {
                RenderSearchMode();
            }
            
            RenderResults();

            var key = Console.ReadKey(true);
            
            if (_isAddPathMode)
            {
                await HandleAddPathKey(key, config);
            }
            else
            {
                await HandleSearchKey(key, config);
            }
        }
    }

    private static void RenderHeader()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold gold3]╔════════════════════════════════════════╗[/]");
        AnsiConsole.MarkupLine("[bold gold3]║         File Search Engine             ║[/]");
        AnsiConsole.MarkupLine("[bold gold3]╚════════════════════════════════════════╝[/]");
    }

    private static void RenderSearchMode()
    {
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[dim]Type to search | Ctrl+P: Add path | Ctrl+D: Debug | Ctrl+C: Exit[/]");
        
        if (!string.IsNullOrEmpty(_statusMessage))
        {
            if (_statusMessage.Contains("debug", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine("[bold cyan]╭─ DEBUG: INDEX INFO ──────────────────────────╮[/]");
                
                List<FileResult> snapshot;
                lock (_indexLock)
                {
                    snapshot = _fileIndex.ToList();
                }
                
                var allPaths = DefaultSearchPaths.Where(Directory.Exists)
                    .Concat(_customPaths.Where(Directory.Exists))
                    .Distinct()
                    .ToList();
                
                AnsiConsole.MarkupLine($"[cyan]Search paths ({allPaths.Count}):[/]");
                foreach (var p in allPaths)
                {
                    var countInIndex = snapshot.Count(f => f.FullPath.StartsWith(p));
                    AnsiConsole.MarkupLine($"  [blue]{p}[/] [dim]({countInIndex} files)[/]");
                }
                
                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine($"[cyan]Total indexed: {snapshot.Count} files[/]");
                
                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine($"[cyan]Files containing 'Stock':[/]");
                var stockFiles = snapshot.Where(f => f.FileName.Contains("Stock", StringComparison.OrdinalIgnoreCase)).Take(20).ToList();
                if (stockFiles.Count > 0)
                {
                    foreach (var f in stockFiles)
                    {
                        AnsiConsole.MarkupLine($"  [white]{f.FileName}[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]  None found[/]");
                }
                
                AnsiConsole.MarkupLine("[bold cyan]╰─────────────────────────────────────────╯[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine(_statusMessage);
            }
            _statusMessage = "";
        }
        
        AnsiConsole.MarkupLine("");
        AnsiConsole.Markup($"[bold green]Search:[/] ");
        AnsiConsole.MarkupLine(Spectre.Console.Markup.Escape(_searchQuery));
        AnsiConsole.MarkupLine("");
    }

    private static void RenderAddPathMode()
    {
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[bold yellow]╭─ ADD PATH MODE ──────────────────────────╮[/]");
        AnsiConsole.MarkupLine("[bold yellow]│ Enter folder path to add to search    │[/]");
        AnsiConsole.MarkupLine("[bold yellow]│ Press Escape to cancel                 │[/]");
        AnsiConsole.MarkupLine("[bold yellow]╰─────────────────────────────────────────╯[/]");
        
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[bold]Current search paths:[/]");
        
        var allPaths = DefaultSearchPaths.Where(Directory.Exists).Concat(_customPaths).Distinct().ToList();
        foreach (var p in allPaths)
        {
            AnsiConsole.MarkupLine($"  [blue]{p}[/]");
        }
        
        AnsiConsole.MarkupLine("");
        AnsiConsole.Markup($"[bold yellow]New path: [/]");
        AnsiConsole.MarkupLine(Spectre.Console.Markup.Escape(_pendingPath));
        AnsiConsole.MarkupLine("");
    }

    private static async Task HandleAddPathKey(ConsoleKeyInfo key, Config config)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            _isAddPathMode = false;
            _pendingPath = "";
            return;
        }

        if (key.Key == ConsoleKey.Enter)
        {
            if (Directory.Exists(_pendingPath))
            {
                if (!_customPaths.Contains(_pendingPath))
                {
                    _customPaths.Add(_pendingPath);
                    config.CustomPaths = _customPaths;
                    AppConfig.Save(config);
                    
                    var startTime = DateTime.Now;
                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Star)
                        .SpinnerStyle(Spectre.Console.Color.Yellow)
                        .StartAsync("Rebuilding index...", async ctx =>
                        {
                            await BuildIndexAsync(config);
                        });
                    
                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    
                    var allPaths = DefaultSearchPaths.Where(Directory.Exists)
                        .Concat(_customPaths.Where(Directory.Exists))
                        .Distinct()
                        .ToList();
                    
                    _statusMessage = $"[green]✓ Added '{_pendingPath}' ({_fileIndex.Count} files from {allPaths.Count} paths in {elapsed:F1}s)[/]";
                }
                else
                {
                    _statusMessage = $"[yellow]Path already exists: {_pendingPath}[/]";
                }
            }
            else
            {
                _statusMessage = $"[red]Directory does not exist: {_pendingPath}[/]";
            }
            _isAddPathMode = false;
            _pendingPath = "";
            _searchQuery = "";
            _results = [];
            return;
        }

        if (key.Key == ConsoleKey.Backspace && _pendingPath.Length > 0)
        {
            _pendingPath = _pendingPath[..^1];
            return;
        }

        if (key.KeyChar >= 32 && key.KeyChar <= 126)
        {
            _pendingPath += key.KeyChar;
            return;
        }
    }

    private static async Task HandleSearchKey(ConsoleKeyInfo key, Config config)
    {
        if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.P)
        {
            _isAddPathMode = true;
            _pendingPath = "";
            return;
        }

        if (key.Key == ConsoleKey.Escape)
        {
            _searchQuery = "";
            _results = [];
            _selectedIndex = 0;
            return;
        }

        if (key.Key == ConsoleKey.Backspace && _searchQuery.Length > 0)
        {
            _searchQuery = _searchQuery[..^1];
            SearchFiles(_searchQuery);
            _selectedIndex = 0;
            return;
        }

        if (key.Key == ConsoleKey.UpArrow)
        {
            if (_results.Count > 0)
            {
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                EnsurePageVisible();
            }
            return;
        }

        if (key.Key == ConsoleKey.DownArrow)
        {
            if (_results.Count > 0)
            {
                _selectedIndex = Math.Min(_results.Count - 1, _selectedIndex + 1);
                EnsurePageVisible();
            }
            return;
        }

        if (key.Key == ConsoleKey.PageDown)
        {
            if (_results.Count > 0)
            {
                var totalPages = (int)Math.Ceiling((double)_results.Count / _pageSize);
                _currentPage = Math.Min(totalPages - 1, _currentPage + 1);
                _selectedIndex = _currentPage * _pageSize;
                if (_selectedIndex >= _results.Count) _selectedIndex = _results.Count - 1;
            }
            return;
        }

        if (key.Key == ConsoleKey.PageUp)
        {
            if (_results.Count > 0)
            {
                _currentPage = Math.Max(0, _currentPage - 1);
                _selectedIndex = _currentPage * _pageSize;
            }
            return;
        }

        if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.D)
        {
            _statusMessage = "[dim]Showing debug info...[/]";
            return;
        }

        if (key.Key == ConsoleKey.Enter)
        {
            if (_results.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _results.Count)
            {
                var selected = _results[_selectedIndex];
                string pathToOpen;
                
                if (selected.IsDirectory)
                {
                    pathToOpen = selected.FullPath;
                }
                else
                {
                    pathToOpen = Path.GetDirectoryName(selected.FullPath) ?? selected.Directory;
                }
                
                if (!string.IsNullOrEmpty(pathToOpen) && Directory.Exists(pathToOpen))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("open", pathToOpen);
                    }
                    catch { }
                }
                else if (!string.IsNullOrEmpty(selected.Directory) && Directory.Exists(selected.Directory))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("open", selected.Directory);
                    }
                    catch { }
                }
                else
                {
                    _statusMessage = $"[red]Cannot open: {pathToOpen}[/]";
                }
            }
            return;
        }

        if (key.KeyChar >= 32 && key.KeyChar <= 126)
        {
            _searchQuery += key.KeyChar;
            SearchFiles(_searchQuery);
            _selectedIndex = 0;
            return;
        }
    }

    private static void EnsurePageVisible()
    {
        var pageStart = _currentPage * _pageSize;
        var pageEnd = Math.Min(pageStart + _pageSize, _results.Count);
        
        if (_selectedIndex < pageStart)
        {
            _currentPage = _selectedIndex / _pageSize;
        }
        else if (_selectedIndex >= pageEnd)
        {
            _currentPage = (_selectedIndex / _pageSize);
        }
    }

    private static readonly int MaxIndexFiles = 100000;

    private static Task BuildIndexAsync(Config config)
    {
        return Task.Run(() =>
        {
            var newIndex = new List<FileResult>();
            
            var searchPaths = DefaultSearchPaths
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
                        catch { }
                    }
                }
                catch { }
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
                        catch { }
                    }
                }
                catch { }
            }

            lock (_indexLock)
            {
                _fileIndex = newIndex;
            }
        });
    }

    private static void SearchFiles(string query)
    {
        _results.Clear();
        _selectedIndex = 0;
        _currentPage = 0;
        _showFuzzy = false;

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        List<FileResult> snapshot;
        lock (_indexLock)
        {
            snapshot = _fileIndex.ToList();
        }

        var queryLower = query.ToLowerInvariant();
        var exactDirMatches = new List<FileResult>();
        var exactFileMatches = new List<FileResult>();
        var partialDirMatches = new List<FileResult>();
        var partialFileMatches = new List<FileResult>();

        foreach (var item in snapshot)
        {
            var nameLower = item.FileName.ToLowerInvariant();

            if (nameLower.Contains(queryLower))
            {
                if (nameLower == queryLower)
                {
                    if (item.IsDirectory)
                        exactDirMatches.Add(item);
                    else
                        exactFileMatches.Add(item);
                }
                else
                {
                    if (item.IsDirectory)
                        partialDirMatches.Add(item);
                    else
                        partialFileMatches.Add(item);
                }
            }
        }

        _results = [.. exactDirMatches, .. exactFileMatches, .. partialDirMatches, .. partialFileMatches];

        if (_results.Count == 0)
        {
            var fuzzyDirResults = new List<FileResult>();
            var fuzzyFileResults = new List<FileResult>();
            var fuzzyThreshold = 70;

            foreach (var item in snapshot)
            {
                var score = Fuzz.WeightedRatio(queryLower, item.FileName.ToLowerInvariant());
                if (score >= fuzzyThreshold)
                {
                    if (item.IsDirectory)
                        fuzzyDirResults.Add(item);
                    else
                        fuzzyFileResults.Add(item);
                }
            }

            if (fuzzyDirResults.Count > 0 || fuzzyFileResults.Count > 0)
            {
                _results = fuzzyDirResults
                    .OrderByDescending(r => r.Score)
                    .Concat(fuzzyFileResults.OrderByDescending(r => r.Score))
                    .Take(50)
                    .ToList();
                _showFuzzy = true;
            }
        }
        else
        {
            _results = _results.Take(100).ToList();
        }
    }

    private static void RenderResults()
    {
        if (_isAddPathMode) return;

        int fileCount, dirCount;
        lock (_indexLock)
        {
            fileCount = _fileIndex.Count(f => !f.IsDirectory);
            dirCount = _fileIndex.Count(f => f.IsDirectory);
        }
        AnsiConsole.MarkupLine($"[dim]Indexed: {fileCount} files, {dirCount} folders[/]");

        if (_showFuzzy)
        {
            AnsiConsole.MarkupLine($"[yellow]No exact matches. Showing fuzzy results for:[/] [bold]{_searchQuery}[/]");
        }
        else if (_results.Count > 0)
        {
            var totalPages = (int)Math.Ceiling((double)_results.Count / _pageSize);
            var currentPageNum = _currentPage + 1;
            AnsiConsole.MarkupLine($"[green]Found[/] [bold]{_results.Count}[/] [green]file(s) | Page[/] [bold]{currentPageNum}[/][green]/[/][bold]{totalPages}[/]");
        }
        else if (!string.IsNullOrEmpty(_searchQuery))
        {
            AnsiConsole.MarkupLine($"[yellow]No files found for:[/] [bold]{_searchQuery}[/]");
        }

        AnsiConsole.MarkupLine("");

        if (_results.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]Start typing to search files...[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.None)
            .AddColumn("")
            .AddColumn("[bold]Name[/]")
            .AddColumn("[bold]Path[/]")
            .AddColumn("[bold]Size[/]")
            .AddColumn("[bold]Modified[/]")
            .AddColumn("[bold]Score[/]");

        var startIdx = _currentPage * _pageSize;
        var endIdx = Math.Min(_results.Count, startIdx + _pageSize);

        for (int i = startIdx; i < endIdx; i++)
        {
            var r = _results[i];
            var isSelected = i == _selectedIndex;
            var marker = isSelected ? "[bold green]▶[/]" : " ";
            var rowColor = isSelected ? "green" : "white";
            var typeIcon = r.IsDirectory ? "[bold blue]📁[/]" : "📄";

            table.AddRow(
                $"[{rowColor}]{marker}[/]",
                $"[{rowColor}]{typeIcon} {Truncate(r.FileName, 28)}[/]",
                $"[{rowColor}]{Truncate(r.Directory, 35)}[/]",
                r.IsDirectory ? "[dim]-[/]" : $"[{rowColor}]{FormatSize(r.Size)}[/]",
                $"[{rowColor}]{r.Modified:yyyy-MM-dd HH:mm}[/]",
                _showFuzzy ? $"[{rowColor}]{r.Score}%[/]" : ""
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[dim]↑↓ Navigate | PgUp/PgDown: Pages | Enter: Open | Escape: Clear | Ctrl+P: Add path[/]");
    }

    private static string Truncate(string s, int maxLength)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= maxLength) return s;
        return s[..(maxLength - 3)] + "...";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes}B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1}KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1}MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1}GB";
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
