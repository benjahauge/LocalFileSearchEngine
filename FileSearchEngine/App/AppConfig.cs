using System.Text.Json;

namespace FileSearchEngine;

public class AppConfig
{
    private static readonly string AppDir = "/Users/benjahauge/Projects/PrivateDev/DotNet/.filesearch";
    private static readonly string ConfigFile = Path.Combine(AppDir, "config.json");
    private static readonly string IndexFile = Path.Combine(AppDir, "index.json");

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
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
        }
        
        return new Config();
    }

    public static void Save(Config config)
    {
        try
        {
            Directory.CreateDirectory(AppDir);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
        }
    }

    public static void SaveIndex(List<FileResult> index)
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

    public static List<FileResult>? LoadIndex()
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
}

public class Config
{
    public List<string> CustomPaths { get; set; } = [];
}