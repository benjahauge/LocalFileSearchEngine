using FuzzySharp;

namespace FileSearchEngine.Services;

public static class SearchService
{
    public static void SearchFiles(string query)
    {
        Global.Results.Clear();
        Global.SelectedIndex = 0;
        Global.CurrentPage = 0;
        Global.ShowFuzzy = false;

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        List<FileResult> snapshot;
        lock (Global.IndexLock)
        {
            snapshot = Global.FileIndex.ToList();
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

        Global.Results = [.. exactDirMatches, .. exactFileMatches, .. partialDirMatches, .. partialFileMatches];

        if (Global.Results.Count == 0)
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
                Global.Results = fuzzyDirResults
                    .OrderByDescending(r => r.Score)
                    .Concat(fuzzyFileResults.OrderByDescending(r => r.Score))
                    .Take(50)
                    .ToList();
                Global.ShowFuzzy = true;
            }
        }
        else
        {
            Global.Results = Global.Results.Take(100).ToList();
        }
    }
}