namespace Fedele.Core.Tests;

public sealed class ModelTests
{
    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Detected_ConfidenceOutOfRange_Throws(double confidence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Detected<int>(1, confidence, "reason"));
    }

    [Fact]
    public void Detected_EmptyReason_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Detected<int>(1, 0.5, " "));
    }

    [Fact]
    public void Detected_ValidArguments_KeepsThem()
    {
        var detected = new Detected<char>(';', 0.75, "Most records have 4 fields.");

        Assert.Equal((';', 0.75, "Most records have 4 fields."), (detected.Value, detected.Confidence, detected.Reason));
    }

    [Fact]
    public void IssueCollector_BeyondLimit_CountsWithoutKeeping()
    {
        var collector = new IssueCollector(2);

        for (var i = 1; i <= 5; i++)
        {
            collector.Add(new Issue("empty-record", IssueSeverity.Warning, "Empty.", new IssueLocation(i, i)));
        }

        Assert.Equal([1L, 2L], collector.Issues.Select(issue => issue.Location!.Record));
        Assert.Equal(3, collector.DroppedCount);
    }

    [Fact]
    public void IssueCollector_NegativeLimit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IssueCollector(-1));
    }

    [Fact]
    public void ReadOptions_Defaults_HeaderPresentAndEncodingDeduced()
    {
        var options = new ReadOptions();

        Assert.Equal(HeaderMode.Present, options.Header);
        Assert.Null(options.Encoding);
        Assert.Equal(1000, options.MaxIssues);
    }
}
