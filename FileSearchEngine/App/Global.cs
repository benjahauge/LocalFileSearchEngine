namespace FileSearchEngine;

public static class Global
{
    public static string[] DefaultSearchPaths { get; set; }
    public static string StatusMessage { get; set; } = "";
    public static List<FileResult> FileIndex { get; set; }

    public static bool IsAddPathMode { get; set; }
    
    public static bool ShowFuzzy { get; set; }
    
    public static List<FileResult> Results = [];
    
    public static readonly int PageSize = 10;
    public static int CurrentPage = 0;
    public static int SelectedIndex = 0;
    
    public static string PendingPath { get; set; } = "";
    
    public static Lock IndexLock = new();

    public static string SearchQuery = "";
    public static Config? Config { get; set; }
    public static List<string> CustomPaths { get; set; } = [];
    
    static Global()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
        var envPaths = Environment.GetEnvironmentVariable("SEARCH_PATHS");
        if (!string.IsNullOrEmpty(envPaths))
        {
            CustomPaths = envPaths.Split(':').ToList();
            DefaultSearchPaths = CustomPaths.ToArray();
        }
        else
        {
            DefaultSearchPaths =
            [
                Path.Combine(home, "Documents"),
                Path.Combine(home, "Desktop"),
                Path.Combine(home, "Projects"),
            ];
        }

        FileIndex = new List<FileResult>();
    }

}