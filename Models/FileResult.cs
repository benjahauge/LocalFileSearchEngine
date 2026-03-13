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