using FileSearchEngine.Services;

namespace FileSearchEngine.Tests;

public class SearchServiceTests
{
    [SetUp]
    public void Setup()
    {
        Global.Results.Clear();
        Global.ShowFuzzy = false;
    }

    [Test]
    public void SearchFiles_ExactMatch_ReturnsResultsWithDirectoriesFirst()
    {
        Global.FileIndex =
        [
            new FileResult { FileName = "document.txt", IsDirectory = false },
            new FileResult { FileName = "docs", IsDirectory = true },
            new FileResult { FileName = "report.pdf", IsDirectory = false },
        ];

        SearchService.SearchFiles("docs");

        Assert.That(Global.Results, Is.Not.Empty);
        Assert.That(Global.Results[0].FileName, Is.EqualTo("docs"));
        Assert.That(Global.Results[0].IsDirectory, Is.True);
    }

    [Test]
    public void SearchFiles_NoExactMatch_FallsBackToFuzzy()
    {
        Global.FileIndex =
        [
            new FileResult { FileName = "MyDocument.txt", IsDirectory = false },
            new FileResult { FileName = "Documentation.pdf", IsDirectory = false },
        ];

        SearchService.SearchFiles("Dokument");

        Assert.That(Global.Results, Is.Not.Empty);
        Assert.That(Global.ShowFuzzy, Is.True);
    }

    [Test]
    public void SearchFiles_PartialMatch_ReturnsResults()
    {
        Global.FileIndex =
        [
            new FileResult { FileName = "testing123.txt", IsDirectory = false },
            new FileResult { FileName = "production.log", IsDirectory = false },
        ];

        SearchService.SearchFiles("test");

        Assert.That(Global.Results, Is.Not.Empty);
        Assert.That(Global.Results.First().FileName, Does.Contain("test"));
    }

    [Test]
    public void SearchFiles_NoMatch_ReturnsEmpty()
    {
        Global.FileIndex =
        [
            new FileResult { FileName = "file.txt", IsDirectory = false },
        ];

        SearchService.SearchFiles("nonexistent");

        Assert.That(Global.Results, Is.Empty);
    }
}