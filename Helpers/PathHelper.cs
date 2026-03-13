using Spectre.Console;

namespace FileSearchEngine.Helpers;

public static class PathHelper
{
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
}