using GeoAutoEngine.Application;
using GeoAutoEngine.Csv;

namespace GeoAutoEngine.Tests;

public sealed class CsvContractTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void SingleFileConvenience_ReturnsTheCanonicalCollectionContract()
    {
        IReadOnlyList<ImportSourceRequest> requests = CsvImportSources.FromSingleFile("survey.csv");

        Assert.Single(requests);
        Assert.Equal(0, requests[0].SelectionOrdinal);
        Assert.EndsWith("survey.csv", requests[0].CanonicalPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void MultipleFiles_PreserveSelectionOrder()
    {
        var requests = CsvImportSources.FromFiles(["day-3.csv", "day-1.csv"]);

        Assert.Equal([0, 1], requests.Select(request => request.SelectionOrdinal));
        Assert.EndsWith("day-3.csv", requests[0].CanonicalPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("day-1.csv", requests[1].CanonicalPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void MultipleFiles_RejectDuplicateSources()
    {
        Assert.Throws<ArgumentException>(() => CsvImportSources.FromFiles(["day-1.csv", ".\\day-1.csv"]));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void MultipleFiles_RejectNonCsvSources()
    {
        Assert.Throws<ArgumentException>(() => CsvImportSources.FromFiles(["survey.txt"]));
    }
}
