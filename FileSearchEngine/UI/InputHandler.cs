using FileSearchEngine.Helpers;
using FileSearchEngine.Services;
using Spectre.Console;

namespace FileSearchEngine.UI;

public static class InputHandler
{
    public static async Task HandleAddPathKey(ConsoleKeyInfo key, Config config, List<string> _customPaths)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            Global.SearchQuery = "";
            Global.IsAddPathMode = false;
            Global.PendingPath = "";
            return;
        }

        if (key.Key == ConsoleKey.Enter)
        {
            if (Directory.Exists(Global.PendingPath))
            {
                if (!_customPaths.Contains(Global.PendingPath))
                {
                    _customPaths.Add(Global.PendingPath);
                    config.CustomPaths = _customPaths;
                    AppConfig.Save(config);
                    
                    var startTime = DateTime.Now;
                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Star)
                        .SpinnerStyle(Color.Yellow)
                        .StartAsync("Rebuilding index...", async ctx =>
                        {
                            await IndexService.BuildAsync(config);
                        });
                    
                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    
                    AppConfig.SaveIndex(await IndexService.LoadOrBuildAsync(config));
                    
                    var allPaths = Global.DefaultSearchPaths.Where(Directory.Exists)
                        .Concat(_customPaths.Where(Directory.Exists))
                        .Distinct()
                        .ToList();
                    
                    Global.StatusMessage = $"[green]✓ Added '{Global.PendingPath}' ({Global.FileIndex.Count} items from {allPaths.Count} paths in {elapsed:F1}s)[/]";
                }
                else
                {
                    Global.StatusMessage = $"[yellow]Path already exists: {Global.PendingPath}[/]";
                }
            }
            else
            {
                Global.StatusMessage = $"[red]Directory does not exist: {Global.PendingPath}[/]";
            }
            Global.IsAddPathMode = false;
            Global.PendingPath = "";
            Global.Results = [];
            return;
        }

        if (key.Key == ConsoleKey.Backspace && Global.PendingPath.Length > 0)
        {
            Global.PendingPath = Global.PendingPath[..^1];
            return;
        }

        if (key.KeyChar >= 32 && key.KeyChar <= 126)
        {
            Global.PendingPath += key.KeyChar;
        }
    }
    
    public static async Task HandleSearchKey(ConsoleKeyInfo key, Config config)
    {
        if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.P)
        {
            Global.IsAddPathMode = true;
            Global.PendingPath = "";
            return;
        }

        if (key.Key == ConsoleKey.Escape)
        {
            Global.SearchQuery = "";
            Global.Results = [];
            Global.SelectedIndex = 0;
            return;
        }

        if (key.Key == ConsoleKey.Backspace && Global.SearchQuery.Length > 0)
        {
            Global.SearchQuery = Global.SearchQuery[..^1];
            SearchService.SearchFiles(Global.SearchQuery);
            Global.SelectedIndex = 0;
            return;
        }

        if (key.Key == ConsoleKey.UpArrow)
        {
            if (Global.Results.Count > 0)
            {
                Global.SelectedIndex = Math.Max(0, Global.SelectedIndex - 1);
                EnsurePageVisible();
            }
            return;
        }

        if (key.Key == ConsoleKey.DownArrow)
        {
            if (Global.Results.Count > 0)
            {
                Global.SelectedIndex = Math.Min(Global.Results.Count - 1, Global.SelectedIndex + 1);
                EnsurePageVisible();
            }
            return;
        }

        if (key.Key == ConsoleKey.PageDown)
        {
            if (Global.Results.Count > 0)
            {
                var totalPages = (int)Math.Ceiling((double)Global.Results.Count / Global.PageSize);
                Global.CurrentPage = Math.Min(totalPages - 1, Global.CurrentPage + 1);
                Global.SelectedIndex = Global.CurrentPage * Global.PageSize;
                if (Global.SelectedIndex >= Global.Results.Count) Global.SelectedIndex = Global.Results.Count - 1;
            }
            return;
        }

        if (key.Key == ConsoleKey.PageUp)
        {
            if (Global.Results.Count > 0)
            {
                Global.CurrentPage = Math.Max(0, Global.CurrentPage - 1);
                Global.SelectedIndex = Global.CurrentPage * Global.PageSize;
            }
            return;
        }

        if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.D)
        {
            Global.StatusMessage = "[dim]Showing debug info...[/]";
            return;
        }

        if (key.Key == ConsoleKey.Enter)
        {
            if (Global.Results.Count > 0 && Global.SelectedIndex >= 0 && Global.SelectedIndex < Global.Results.Count)
            {
                var selected = Global.Results[Global.SelectedIndex];
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
                    catch(Exception ex)
                    {
                        SentrySdk.CaptureException(ex);
                    }
                }
                else if (!string.IsNullOrEmpty(selected.Directory) && Directory.Exists(selected.Directory))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("open", selected.Directory);
                    }
                    catch(Exception ex)
                    {
                        SentrySdk.CaptureException(ex);
                    }
                }
                else
                {
                    Global.StatusMessage = $"[red]Cannot open: {pathToOpen}[/]";
                }
            }
            return;
        }

        if (key.KeyChar >= 32 && key.KeyChar <= 126)
        {
            Global.SearchQuery += key.KeyChar;
            SearchService.SearchFiles(Global.SearchQuery);
            Global.SelectedIndex = 0;
        }
    }
    
    private static void EnsurePageVisible()
    {
        var pageStart = Global.CurrentPage * Global.PageSize;
        var pageEnd = Math.Min(pageStart + Global.PageSize, Global.Results.Count);
        
        if (Global.SelectedIndex < pageStart)
        {
            Global.CurrentPage = Global.SelectedIndex / Global.PageSize;
        }
        else if (Global.SelectedIndex >= pageEnd)
        {
            Global.CurrentPage = (Global.SelectedIndex / Global.PageSize);
        }
    }
}