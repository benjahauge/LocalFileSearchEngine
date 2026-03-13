using Spectre.Console;

namespace FileSearchEngine.UI;

public static class RenderService
{

    // private string _pendingPath = "";
    
    
    public static void RenderHeader()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold gold3]╔════════════════════════════════════════╗[/]");
        AnsiConsole.MarkupLine("[bold gold3]║         File Search Engine             ║[/]");
        AnsiConsole.MarkupLine("[bold gold3]╚════════════════════════════════════════╝[/]");
    }
    
    public static void RenderSearchMode()
    {
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[dim]Type to search | Ctrl+P: Add path | Ctrl+D: Debug | Ctrl+C: Exit[/]");
        
        if (!string.IsNullOrEmpty(Global.StatusMessage))
        {
            if (Global.StatusMessage.Contains("debug", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine("[bold cyan]╭─ DEBUG: INDEX INFO ──────────────────────────╮[/]");
                
                List<FileResult> snapshot;
                lock (Global.IndexLock)
                {
                    snapshot = Global.FileIndex.ToList();
                }
                
                var allPaths = Global.DefaultSearchPaths.Where(Directory.Exists)
                    .Concat(Global.CustomPaths.Where(Directory.Exists))
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
                AnsiConsole.MarkupLine(Global.StatusMessage);
            }
            Global.StatusMessage = "";
        }
        
        AnsiConsole.MarkupLine("");
        AnsiConsole.Markup($"[bold green]Search:[/] ");
        AnsiConsole.MarkupLine(Markup.Escape(Global.SearchQuery));
        AnsiConsole.MarkupLine("");
    }
    
    public static void RenderAddPathMode()
    {
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[bold yellow]╭─ ADD PATH MODE ──────────────────────────╮[/]");
        AnsiConsole.MarkupLine("[bold yellow]│ Enter folder path to add to search    │[/]");
        AnsiConsole.MarkupLine("[bold yellow]│ Press Escape to cancel                 │[/]");
        AnsiConsole.MarkupLine("[bold yellow]╰─────────────────────────────────────────╯[/]");
        
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[bold]Current search paths:[/]");
        
        var allPaths = Global.DefaultSearchPaths.Where(Directory.Exists).Concat(Global.CustomPaths).Distinct().ToList();
        foreach (var p in allPaths)
        {
            AnsiConsole.MarkupLine($"  [blue]{p}[/]");
        }
        
        AnsiConsole.MarkupLine("");
        AnsiConsole.Markup("[bold yellow]New path: [/]");
        AnsiConsole.MarkupLine(Markup.Escape(Global.PendingPath));
        AnsiConsole.MarkupLine("");
    }
    
    public static void RenderResults()
    {
        if (Global.IsAddPathMode) return;
    
        int fileCount, dirCount;
        lock (Global.IndexLock)
        {
            fileCount = Global.FileIndex.Count(f => !f.IsDirectory);
            dirCount = Global.FileIndex.Count(f => f.IsDirectory);
        }
        AnsiConsole.MarkupLine($"[dim]Indexed: {fileCount} files, {dirCount} folders[/]");
    
        if (Global.ShowFuzzy)
        {
            AnsiConsole.MarkupLine($"[yellow]No exact matches. Showing fuzzy results for:[/] [bold]{Global.SearchQuery}[/]");
        }
        else if (Global.Results.Count > 0)
        {
            var totalPages = (int)Math.Ceiling((double)Global.Results.Count / Global.PageSize);
            var currentPageNum = Global.CurrentPage + 1;
            AnsiConsole.MarkupLine($"[green]Found[/] [bold]{Global.Results.Count}[/] [green]file(s) | Page[/] [bold]{currentPageNum}[/][green]/[/][bold]{totalPages}[/]");
        }
        else if (!string.IsNullOrEmpty(Global.SearchQuery))
        {
            AnsiConsole.MarkupLine($"[yellow]No files found for:[/] [bold]{Global.SearchQuery}[/]");
        }
    
        AnsiConsole.MarkupLine("");
    
        if (Global.Results.Count == 0)
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
    
        var startIdx = Global.CurrentPage * Global.PageSize;
        var endIdx = Math.Min(Global.Results.Count, startIdx + Global.PageSize);
    
        for (int i = startIdx; i < endIdx; i++)
        {
            var r = Global.Results[i];
            var isSelected = i == Global.SelectedIndex;
            var marker = isSelected ? "[bold green]▶[/]" : " ";
            var rowColor = isSelected ? "green" : "white";
            var typeIcon = r.IsDirectory ? "[bold blue]📁[/]" : "📄";
    
            table.AddRow(
                $"[{rowColor}]{marker}[/]",
                $"[{rowColor}]{typeIcon} {Truncate(r.FileName, 28)}[/]",
                $"[{rowColor}]{Truncate(r.Directory, 35)}[/]",
                r.IsDirectory ? "[dim]-[/]" : $"[{rowColor}]{FormatSize(r.Size)}[/]",
                $"[{rowColor}]{r.Modified:yyyy-MM-dd HH:mm}[/]",
                Global.ShowFuzzy ? $"[{rowColor}]{r.Score}%[/]" : ""
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
}