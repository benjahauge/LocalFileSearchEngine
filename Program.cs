using FileSearchEngine.Helpers;
using FileSearchEngine.UI;


namespace FileSearchEngine;

public static class Program
{
    // private static readonly string[] DefaultSearchPaths;
    // private static readonly string[] ExcludedDirectories = 
    // [
    //     "node_modules", ".git", "bin", "obj", "vendor", ".svn", ".hg", 
    //     "packages", ".idea", ".vs", "dist", "build", "out", "target"
    // ];
    
    // private static List<string> _customPaths = [];
    // private static string _searchQuery = "";
    // private static string _statusMessage = "";
    // private static List<FileResult> _results = [];
    // private static List<FileResult> _fileIndex = [];
    // private static readonly Lock _indexLock = new();
    // private static int _selectedIndex = 0;
    // private static int _currentPage = 0;
    // private static readonly int _pageSize = 10;
    // private static bool _isAddPathMode = false;
    // private static string _pendingPath = "";
    
    static Program()
    {
        SentrySdk.Init(options =>
        {
            // A Sentry Data Source Name (DSN) is required.
            // See https://docs.sentry.io/product/sentry-basics/dsn-explainer/
            // You can set it in the SENTRY_DSN environment variable, or you can set it in code here.
            options.Dsn = "https://8cabc4a4c1a7d56941cf3b3b133075ff@o4510443959353344.ingest.de.sentry.io/4511037436526672";

            // When debug is enabled, the Sentry client will emit detailed debugging information to the console.
            // This might be helpful, or might interfere with the normal operation of your application.
            // We enable it here for demonstration purposes when first trying Sentry.
            // You shouldn't do this in your applications unless you're troubleshooting issues with Sentry.
            options.Debug = false;

            // This option is recommended. It enables Sentry's "Release Health" feature.
            options.AutoSessionTracking = true;
        });
        
            // var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            // DefaultSearchPaths =
            // [
            //     Path.Combine(home, "Documents"),
            //     Path.Combine(home, "Desktop"),
            //     Path.Combine(home, "Projects"),
            // ];
    }

    public static async Task<int> Main(string[] args)
    {
        var config = AppConfig.Load();
        
        await PathHelper.InitializePaths(args, config);
        Global.FileIndex = await PathHelper.InitializeIndex(config);
        
        var renderService = new RenderService(config.CustomPaths);
        

        // if (args.Length > 0 && args[0] == "add-path")
        // {
        //     if (args.Length > 1)
        //     {
        //         var newPath = string.Join(" ", args.Skip(1));
        //         if (Directory.Exists(newPath))
        //         {
        //             if (!_customPaths.Contains(newPath))
        //             {
        //                 _customPaths.Add(newPath);
        //                 config.CustomPaths = _customPaths;
        //                 AppConfig.Save(config);
        //                 AnsiConsole.MarkupLine($"[green]Added path:[/] {newPath}");
        //             }
        //             else
        //             {
        //                 AnsiConsole.MarkupLine($"[yellow]Path already exists:[/] {newPath}");
        //             }
        //         }
        //         else
        //         {
        //             AnsiConsole.MarkupLine($"[red]Directory does not exist:[/] {newPath}");
        //         }
        //     }
        //     else
        //     {
        //         AnsiConsole.MarkupLine("[red]Usage: filesearch add-path <path>[/]");
        //     }
        //     return 0;
        // }
        //
        // if (args.Length > 0 && args[0] == "list-paths")
        // {
        //     AnsiConsole.MarkupLine("[bold]Default Search Paths:[/]");
        //     foreach (var p in DefaultSearchPaths.Where(Directory.Exists))
        //     {
        //         AnsiConsole.MarkupLine($"  [blue]{p}[/]");
        //     }
        //     if (_customPaths.Count > 0)
        //     {
        //         AnsiConsole.MarkupLine("[bold]Custom Search Paths:[/]");
        //         foreach (var p in _customPaths.Where(Directory.Exists))
        //         {
        //             AnsiConsole.MarkupLine($"  [green]{p}[/]");
        //         }
        //     }
        //     return 0;
        // }

        // var loadedIndex = AppConfig.LoadIndex();
        // if (loadedIndex != null && loadedIndex.Count > 0)
        // {
        //     lock (_indexLock)
        //     {
        //         _fileIndex = loadedIndex;
        //     }
        //     int fileCount, dirCount;
        //     lock (_indexLock)
        //     {
        //         fileCount = _fileIndex.Count(f => !f.IsDirectory);
        //         dirCount = _fileIndex.Count(f => f.IsDirectory);
        //     }
        //     AnsiConsole.MarkupLine($"[green]✓ Loaded {fileCount} files, {dirCount} folders from cache[/]");
        // }
        // else
        // {
        //     var startTime = DateTime.Now;
        //     await AnsiConsole.Status()
        //         .Spinner(Spinner.Known.Star)
        //         .SpinnerStyle(Spectre.Console.Color.Yellow)
        //         .StartAsync("Building file index...", async ctx =>
        //         {
        //             await BuildIndexAsync(config);
        //         });
        //     
        //     var elapsed = (DateTime.Now - startTime).TotalSeconds;
        //     int fileCount, dirCount;
        //     lock (_indexLock)
        //     {
        //         fileCount = _fileIndex.Count(f => !f.IsDirectory);
        //         dirCount = _fileIndex.Count(f => f.IsDirectory);
        //     }
        //     AnsiConsole.MarkupLine($"[green]✓ Indexed {fileCount} files, {dirCount} folders in {elapsed:F1}s[/]");
        //     
        //     AppConfig.SaveIndex(_fileIndex);
        //     AnsiConsole.MarkupLine("[dim]Index saved to disk[/]");
        // }
        
        Console.CancelKeyPress += (s, e) =>
        {
            AppConfig.SaveIndex(Global.FileIndex);
        };
        
        while (true)
        {
            renderService.RenderHeader();
            
            if (Global.IsAddPathMode)
            {
                renderService.RenderAddPathMode();
            }
            else
            {
                renderService.RenderSearchMode();
            }
            
            renderService.RenderResults();

            var key = Console.ReadKey(true);
            
            if (Global.IsAddPathMode)
            {
                await InputHandler.HandleAddPathKey(key, config, config.CustomPaths);
            }
            else
            {
                await InputHandler.HandleSearchKey(key, config);
            }
        }
    }

    

    // private static void RenderHeader()
    // {
    //     AnsiConsole.Clear();
    //     AnsiConsole.MarkupLine("[bold gold3]╔════════════════════════════════════════╗[/]");
    //     AnsiConsole.MarkupLine("[bold gold3]║         File Search Engine             ║[/]");
    //     AnsiConsole.MarkupLine("[bold gold3]╚════════════════════════════════════════╝[/]");
    // }

    // private static void RenderSearchMode()
    // {
    //     AnsiConsole.MarkupLine("");
    //     AnsiConsole.MarkupLine("[dim]Type to search | Ctrl+P: Add path | Ctrl+D: Debug | Ctrl+C: Exit[/]");
    //     
    //     if (!string.IsNullOrEmpty(Global.StatusMessage))
    //     {
    //         if (Global.StatusMessage.Contains("debug", StringComparison.OrdinalIgnoreCase))
    //         {
    //             AnsiConsole.MarkupLine("");
    //             AnsiConsole.MarkupLine("[bold cyan]╭─ DEBUG: INDEX INFO ──────────────────────────╮[/]");
    //             
    //             List<FileResult> snapshot;
    //             lock (Global.IndexLock)
    //             {
    //                 snapshot = Global.FileIndex.ToList();
    //             }
    //             
    //             var allPaths = Global.DefaultSearchPaths.Where(Directory.Exists)
    //                 .Concat(_customPaths.Where(Directory.Exists))
    //                 .Distinct()
    //                 .ToList();
    //             
    //             AnsiConsole.MarkupLine($"[cyan]Search paths ({allPaths.Count}):[/]");
    //             foreach (var p in allPaths)
    //             {
    //                 var countInIndex = snapshot.Count(f => f.FullPath.StartsWith(p));
    //                 AnsiConsole.MarkupLine($"  [blue]{p}[/] [dim]({countInIndex} files)[/]");
    //             }
    //             
    //             AnsiConsole.MarkupLine("");
    //             AnsiConsole.MarkupLine($"[cyan]Total indexed: {snapshot.Count} files[/]");
    //             
    //             AnsiConsole.MarkupLine("");
    //             AnsiConsole.MarkupLine($"[cyan]Files containing 'Stock':[/]");
    //             var stockFiles = snapshot.Where(f => f.FileName.Contains("Stock", StringComparison.OrdinalIgnoreCase)).Take(20).ToList();
    //             if (stockFiles.Count > 0)
    //             {
    //                 foreach (var f in stockFiles)
    //                 {
    //                     AnsiConsole.MarkupLine($"  [white]{f.FileName}[/]");
    //                 }
    //             }
    //             else
    //             {
    //                 AnsiConsole.MarkupLine("[yellow]  None found[/]");
    //             }
    //             
    //             AnsiConsole.MarkupLine("[bold cyan]╰─────────────────────────────────────────╯[/]");
    //         }
    //         else
    //         {
    //             AnsiConsole.MarkupLine("");
    //             AnsiConsole.MarkupLine(Global.StatusMessage);
    //         }
    //         Global.StatusMessage = "";
    //     }
    //     
    //     AnsiConsole.MarkupLine("");
    //     AnsiConsole.Markup($"[bold green]Search:[/] ");
    //     AnsiConsole.MarkupLine(Spectre.Console.Markup.Escape(_searchQuery));
    //     AnsiConsole.MarkupLine("");
    // }

    // private static void RenderAddPathMode()
    // {
    //     AnsiConsole.MarkupLine("");
    //     AnsiConsole.MarkupLine("[bold yellow]╭─ ADD PATH MODE ──────────────────────────╮[/]");
    //     AnsiConsole.MarkupLine("[bold yellow]│ Enter folder path to add to search    │[/]");
    //     AnsiConsole.MarkupLine("[bold yellow]│ Press Escape to cancel                 │[/]");
    //     AnsiConsole.MarkupLine("[bold yellow]╰─────────────────────────────────────────╯[/]");
    //     
    //     AnsiConsole.MarkupLine("");
    //     AnsiConsole.MarkupLine("[bold]Current search paths:[/]");
    //     
    //     var allPaths = Global.DefaultSearchPaths.Where(Directory.Exists).Concat(_customPaths).Distinct().ToList();
    //     foreach (var p in allPaths)
    //     {
    //         AnsiConsole.MarkupLine($"  [blue]{p}[/]");
    //     }
    //     
    //     AnsiConsole.MarkupLine("");
    //     AnsiConsole.Markup($"[bold yellow]New path: [/]");
    //     AnsiConsole.MarkupLine(Markup.Escape(Global.PendingPath));
    //     AnsiConsole.MarkupLine("");
    // }
    //
    // private static async Task HandleAddPathKey(ConsoleKeyInfo key, Config config)
    // {
    //     if (key.Key == ConsoleKey.Escape)
    //     {
    //         Global.IsAddPathMode = false;
    //         Global.PendingPath = "";
    //         return;
    //     }
    //
    //     if (key.Key == ConsoleKey.Enter)
    //     {
    //         if (Directory.Exists(Global.PendingPath))
    //         {
    //             if (!_customPaths.Contains(Global.PendingPath))
    //             {
    //                 _customPaths.Add(Global.PendingPath);
    //                 config.CustomPaths = _customPaths;
    //                 AppConfig.Save(config);
    //                 
    //                 var startTime = DateTime.Now;
    //                 await AnsiConsole.Status()
    //                     .Spinner(Spinner.Known.Star)
    //                     .SpinnerStyle(Spectre.Console.Color.Yellow)
    //                     .StartAsync("Rebuilding index...", async ctx =>
    //                     {
    //                         await BuildIndexAsync(config);
    //                     });
    //                 
    //                 var elapsed = (DateTime.Now - startTime).TotalSeconds;
    //                 
    //                 AppConfig.SaveIndex(Global.FileIndex);
    //                 
    //                 var allPaths = Global.DefaultSearchPaths.Where(Directory.Exists)
    //                     .Concat(_customPaths.Where(Directory.Exists))
    //                     .Distinct()
    //                     .ToList();
    //                 
    //                 Global.StatusMessage = $"[green]✓ Added '{Global.PendingPath}' ({Global.FileIndex.Count} items from {allPaths.Count} paths in {elapsed:F1}s)[/]";
    //             }
    //             else
    //             {
    //                 Global.StatusMessage = $"[yellow]Path already exists: {Global.PendingPath}[/]";
    //             }
    //         }
    //         else
    //         {
    //             Global.StatusMessage = $"[red]Directory does not exist: {Global.PendingPath}[/]";
    //         }
    //         Global.IsAddPathMode = false;
    //         Global.PendingPath = "";
    //         _searchQuery = "";
    //         Global.Results = [];
    //         return;
    //     }
    //
    //     if (key.Key == ConsoleKey.Backspace && Global.PendingPath.Length > 0)
    //     {
    //         Global.PendingPath = Global.PendingPath[..^1];
    //         return;
    //     }
    //
    //     if (key.KeyChar >= 32 && key.KeyChar <= 126)
    //     {
    //         Global.PendingPath += key.KeyChar;
    //         return;
    //     }
    // }
    //
    // private static async Task HandleSearchKey(ConsoleKeyInfo key, Config config)
    // {
    //     if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.P)
    //     {
    //         Global.IsAddPathMode = true;
    //         Global.PendingPath = "";
    //         return;
    //     }
    //
    //     if (key.Key == ConsoleKey.Escape)
    //     {
    //         _searchQuery = "";
    //         Global.Results = [];
    //         Global.SelectedIndex = 0;
    //         return;
    //     }
    //
    //     if (key.Key == ConsoleKey.Backspace && _searchQuery.Length > 0)
    //     {
    //         _searchQuery = _searchQuery[..^1];
    //         SearchFiles(_searchQuery);
    //         Global.SelectedIndex = 0;
    //         return;
    //     }
    //
    //     if (key.Key == ConsoleKey.UpArrow)
    //     {
    //         if (Global.Results.Count > 0)
    //         {
    //             Global.SelectedIndex = Math.Max(0, Global.SelectedIndex - 1);
    //             EnsurePageVisible();
    //         }
    //         return;
    //     }
    //
    //     if (key.Key == ConsoleKey.DownArrow)
    //     {
    //         if (Global.Results.Count > 0)
    //         {
    //             Global.SelectedIndex = Math.Min(Global.Results.Count - 1, Global.SelectedIndex + 1);
    //             EnsurePageVisible();
    //         }
    //         return;
    //     }
    //
    //     if (key.Key == ConsoleKey.PageDown)
    //     {
    //         if (Global.Results.Count > 0)
    //         {
    //             var totalPages = (int)Math.Ceiling((double)Global.Results.Count / Global.PageSize);
    //             Global.CurrentPage = Math.Min(totalPages - 1, Global.CurrentPage + 1);
    //             Global.SelectedIndex = Global.CurrentPage * Global.PageSize;
    //             if (Global.SelectedIndex >= Global.Results.Count) Global.SelectedIndex = Global.Results.Count - 1;
    //         }
    //         return;
    //     }
    //
    //     if (key.Key == ConsoleKey.PageUp)
    //     {
    //         if (Global.Results.Count > 0)
    //         {
    //             Global.CurrentPage = Math.Max(0, Global.CurrentPage - 1);
    //             Global.SelectedIndex = Global.CurrentPage * Global.PageSize;
    //         }
    //         return;
    //     }
    //
    //     if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.D)
    //     {
    //         Global.StatusMessage = "[dim]Showing debug info...[/]";
    //         return;
    //     }
    //
    //     if (key.Key == ConsoleKey.Enter)
    //     {
    //         if (Global.Results.Count > 0 && Global.SelectedIndex >= 0 && Global.SelectedIndex < Global.Results.Count)
    //         {
    //             var selected = Global.Results[Global.SelectedIndex];
    //             string pathToOpen;
    //             
    //             if (selected.IsDirectory)
    //             {
    //                 pathToOpen = selected.FullPath;
    //             }
    //             else
    //             {
    //                 pathToOpen = Path.GetDirectoryName(selected.FullPath) ?? selected.Directory;
    //             }
    //             
    //             if (!string.IsNullOrEmpty(pathToOpen) && Directory.Exists(pathToOpen))
    //             {
    //                 try
    //                 {
    //                     System.Diagnostics.Process.Start("open", pathToOpen);
    //                 }
    //                 catch { }
    //             }
    //             else if (!string.IsNullOrEmpty(selected.Directory) && Directory.Exists(selected.Directory))
    //             {
    //                 try
    //                 {
    //                     System.Diagnostics.Process.Start("open", selected.Directory);
    //                 }
    //                 catch { }
    //             }
    //             else
    //             {
    //                 Global.StatusMessage = $"[red]Cannot open: {pathToOpen}[/]";
    //             }
    //         }
    //         return;
    //     }
    //
    //     if (key.KeyChar >= 32 && key.KeyChar <= 126)
    //     {
    //         _searchQuery += key.KeyChar;
    //         SearchFiles(_searchQuery);
    //         Global.SelectedIndex = 0;
    //         return;
    //     }
    // }
    //
    // private static void EnsurePageVisible()
    // {
    //     var pageStart = Global.CurrentPage * Global.PageSize;
    //     var pageEnd = Math.Min(pageStart + Global.PageSize, Global.Results.Count);
    //     
    //     if (Global.SelectedIndex < pageStart)
    //     {
    //         Global.CurrentPage = Global.SelectedIndex / Global.PageSize;
    //     }
    //     else if (Global.SelectedIndex >= pageEnd)
    //     {
    //         Global.CurrentPage = (Global.SelectedIndex / Global.PageSize);
    //     }
    // }
    //
    // private static readonly int MaxIndexFiles = 100000;
    //
    // private static Task BuildIndexAsync(Config config)
    // {
    //     return Task.Run(() =>
    //     {
    //         var newIndex = new List<FileResult>();
    //         
    //         var searchPaths = Global.DefaultSearchPaths
    //             .Where(Directory.Exists)
    //             .Concat(_customPaths.Where(Directory.Exists))
    //             .Concat(config.CustomPaths.Where(Directory.Exists))
    //             .Select(p => p.TrimEnd(Path.DirectorySeparatorChar))
    //             .Distinct()
    //             .ToList();
    //
    //         foreach (var path in searchPaths)
    //         {
    //             if (newIndex.Count >= MaxIndexFiles) break;
    //             
    //             try
    //             {
    //                 var dirs = Directory.EnumerateDirectories(path, "*", new EnumerationOptions
    //                 {
    //                     RecurseSubdirectories = true,
    //                     IgnoreInaccessible = true,
    //                     AttributesToSkip = FileAttributes.System
    //                 });
    //
    //                 foreach (var dir in dirs)
    //                 {
    //                     if (newIndex.Count >= MaxIndexFiles) break;
    //                     
    //                     try
    //                     {
    //                         if (IsPathExcluded(dir)) continue;
    //                         
    //                         var info = new DirectoryInfo(dir);
    //                         newIndex.Add(new FileResult
    //                         {
    //                             FullPath = dir,
    //                             FileName = Path.GetFileName(dir),
    //                             Directory = Path.GetDirectoryName(dir) ?? "",
    //                             Size = 0,
    //                             Modified = info.LastWriteTime,
    //                             Score = 100,
    //                             IsDirectory = true
    //                         });
    //                     }
    //                     catch { }
    //                 }
    //             }
    //             catch { }
    //         }
    //
    //         foreach (var path in searchPaths)
    //         {
    //             if (newIndex.Count >= MaxIndexFiles) break;
    //             
    //             try
    //             {
    //                 var files = Directory.EnumerateFiles(path, "*", new EnumerationOptions
    //                 {
    //                     RecurseSubdirectories = true,
    //                     IgnoreInaccessible = true,
    //                     AttributesToSkip = FileAttributes.System
    //                 });
    //
    //                 foreach (var file in files)
    //                 {
    //                     if (newIndex.Count >= MaxIndexFiles) break;
    //                     
    //                     try
    //                     {
    //                         if (IsPathExcluded(file)) continue;
    //                         
    //                         var info = new FileInfo(file);
    //                         newIndex.Add(new FileResult
    //                         {
    //                             FullPath = file,
    //                             FileName = Path.GetFileName(file),
    //                             Directory = Path.GetDirectoryName(file) ?? "",
    //                             Size = info.Length,
    //                             Modified = info.LastWriteTime,
    //                             Score = 100,
    //                             IsDirectory = false
    //                         });
    //                     }
    //                     catch { }
    //                 }
    //             }
    //             catch { }
    //         }
    //
    //         lock (Global.IndexLock)
    //         {
    //             Global.FileIndex = newIndex;
    //         }
    //     });
    // }
    //
    // private static void SearchFiles(string query)
    // {
    //     Global.Results.Clear();
    //     Global.SelectedIndex = 0;
    //     Global.CurrentPage = 0;
    //     Global.ShowFuzzy = false;
    //
    //     if (string.IsNullOrWhiteSpace(query))
    //     {
    //         return;
    //     }
    //
    //     List<FileResult> snapshot;
    //     lock (Global.IndexLock)
    //     {
    //         snapshot = Global.FileIndex.ToList();
    //     }
    //
    //     var queryLower = query.ToLowerInvariant();
    //     var exactDirMatches = new List<FileResult>();
    //     var exactFileMatches = new List<FileResult>();
    //     var partialDirMatches = new List<FileResult>();
    //     var partialFileMatches = new List<FileResult>();
    //
    //     foreach (var item in snapshot)
    //     {
    //         var nameLower = item.FileName.ToLowerInvariant();
    //
    //         if (nameLower.Contains(queryLower))
    //         {
    //             if (nameLower == queryLower)
    //             {
    //                 if (item.IsDirectory)
    //                     exactDirMatches.Add(item);
    //                 else
    //                     exactFileMatches.Add(item);
    //             }
    //             else
    //             {
    //                 if (item.IsDirectory)
    //                     partialDirMatches.Add(item);
    //                 else
    //                     partialFileMatches.Add(item);
    //             }
    //         }
    //     }
    //
    //     Global.Results = [.. exactDirMatches, .. exactFileMatches, .. partialDirMatches, .. partialFileMatches];
    //
    //     if (Global.Results.Count == 0)
    //     {
    //         var fuzzyDirResults = new List<FileResult>();
    //         var fuzzyFileResults = new List<FileResult>();
    //         var fuzzyThreshold = 70;
    //
    //         foreach (var item in snapshot)
    //         {
    //             var score = Fuzz.WeightedRatio(queryLower, item.FileName.ToLowerInvariant());
    //             if (score >= fuzzyThreshold)
    //             {
    //                 if (item.IsDirectory)
    //                     fuzzyDirResults.Add(item);
    //                 else
    //                     fuzzyFileResults.Add(item);
    //             }
    //         }
    //
    //         if (fuzzyDirResults.Count > 0 || fuzzyFileResults.Count > 0)
    //         {
    //             Global.Results = fuzzyDirResults
    //                 .OrderByDescending(r => r.Score)
    //                 .Concat(fuzzyFileResults.OrderByDescending(r => r.Score))
    //                 .Take(50)
    //                 .ToList();
    //             Global.ShowFuzzy = true;
    //         }
    //     }
    //     else
    //     {
    //         Global.Results = Global.Results.Take(100).ToList();
    //     }
    // }
    //
    // private static void RenderResults()
    // {
    //     if (Global.IsAddPathMode) return;
    //
    //     int fileCount, dirCount;
    //     lock (Global.IndexLock)
    //     {
    //         fileCount = Global.FileIndex.Count(f => !f.IsDirectory);
    //         dirCount = Global.FileIndex.Count(f => f.IsDirectory);
    //     }
    //     AnsiConsole.MarkupLine($"[dim]Indexed: {fileCount} files, {dirCount} folders[/]");
    //
    //     if (Global.ShowFuzzy)
    //     {
    //         AnsiConsole.MarkupLine($"[yellow]No exact matches. Showing fuzzy results for:[/] [bold]{_searchQuery}[/]");
    //     }
    //     else if (Global.Results.Count > 0)
    //     {
    //         var totalPages = (int)Math.Ceiling((double)Global.Results.Count / Global.PageSize);
    //         var currentPageNum = Global.CurrentPage + 1;
    //         AnsiConsole.MarkupLine($"[green]Found[/] [bold]{Global.Results.Count}[/] [green]file(s) | Page[/] [bold]{currentPageNum}[/][green]/[/][bold]{totalPages}[/]");
    //     }
    //     else if (!string.IsNullOrEmpty(_searchQuery))
    //     {
    //         AnsiConsole.MarkupLine($"[yellow]No files found for:[/] [bold]{_searchQuery}[/]");
    //     }
    //
    //     AnsiConsole.MarkupLine("");
    //
    //     if (Global.Results.Count == 0)
    //     {
    //         AnsiConsole.MarkupLine("[dim]Start typing to search files...[/]");
    //         return;
    //     }
    //
    //     var table = new Table()
    //         .Border(TableBorder.None)
    //         .AddColumn("")
    //         .AddColumn("[bold]Name[/]")
    //         .AddColumn("[bold]Path[/]")
    //         .AddColumn("[bold]Size[/]")
    //         .AddColumn("[bold]Modified[/]")
    //         .AddColumn("[bold]Score[/]");
    //
    //     var startIdx = Global.CurrentPage * Global.PageSize;
    //     var endIdx = Math.Min(Global.Results.Count, startIdx + Global.PageSize);
    //
    //     for (int i = startIdx; i < endIdx; i++)
    //     {
    //         var r = Global.Results[i];
    //         var isSelected = i == Global.SelectedIndex;
    //         var marker = isSelected ? "[bold green]▶[/]" : " ";
    //         var rowColor = isSelected ? "green" : "white";
    //         var typeIcon = r.IsDirectory ? "[bold blue]📁[/]" : "📄";
    //
    //         table.AddRow(
    //             $"[{rowColor}]{marker}[/]",
    //             $"[{rowColor}]{typeIcon} {Truncate(r.FileName, 28)}[/]",
    //             $"[{rowColor}]{Truncate(r.Directory, 35)}[/]",
    //             r.IsDirectory ? "[dim]-[/]" : $"[{rowColor}]{FormatSize(r.Size)}[/]",
    //             $"[{rowColor}]{r.Modified:yyyy-MM-dd HH:mm}[/]",
    //             Global.ShowFuzzy ? $"[{rowColor}]{r.Score}%[/]" : ""
    //         );
    //     }
    //
    //     AnsiConsole.Write(table);
    //
    //     AnsiConsole.MarkupLine("");
    //     AnsiConsole.MarkupLine("[dim]↑↓ Navigate | PgUp/PgDown: Pages | Enter: Open | Escape: Clear | Ctrl+P: Add path[/]");
    // }
    //
    // private static string Truncate(string s, int maxLength)
    // {
    //     if (string.IsNullOrEmpty(s) || s.Length <= maxLength) return s;
    //     return s[..(maxLength - 3)] + "...";
    // }
    //
    // private static string FormatSize(long bytes)
    // {
    //     if (bytes < 1024) return $"{bytes}B";
    //     if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1}KB";
    //     if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1}MB";
    //     return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1}GB";
    // }

    // private static bool IsPathExcluded(string path)
    // {
    //     var parts = path.Split(Path.DirectorySeparatorChar);
    //     foreach (var part in parts)
    //     {
    //         if (part.StartsWith(".")) return true;
    //         if (ExcludedDirectories.Contains(part, StringComparer.OrdinalIgnoreCase)) return true;
    //     }
    //     return false;
    // }
}
