using System.Text;

namespace Fedele.Anonymizer.Tests;

public sealed class FileAnonymizerTests
{
    public static TheoryData<string> Variants => TestFiles.VariantNames();

    [Theory]
    [MemberData(nameof(Variants))]
    public void Anonymize_NoColumns_ProducesIdenticalBytes(string variant)
    {
        var input = TestFiles.Variants[variant];

        var output = TestFiles.Anonymize(input, columns: []);

        Assert.Equal(input, output);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void Anonymize_AllColumns_KeepsStructure(string variant)
    {
        var input = TestFiles.Variants[variant];

        var output = TestFiles.Anonymize(input, columns: [0, 1, 2, 3], hasHeader: false);

        var before = TestFiles.Analyze(input);
        var after = TestFiles.Analyze(output);
        Assert.Equal(before.Encoding, after.Encoding);
        Assert.Equal(before.Separator.Separator, after.Separator.Separator);
        Assert.Equal((before.CrLf, before.Lf, before.Cr), (after.CrLf, after.Lf, after.Cr));
        Assert.Equal(before.EndsWithLineEnding, after.EndsWithLineEnding);
        Assert.Equal((before.Records, before.EmptyRecords), (after.Records, after.EmptyRecords));
        Assert.Equal(before.FieldCounts, after.FieldCounts);
        Assert.Equal(before.QuotedFields, after.QuotedFields);
        Assert.Equal(before.FieldsWithLineBreaks, after.FieldsWithLineBreaks);
        Assert.Equal(before.UnterminatedQuotes, after.UnterminatedQuotes);
        Assert.Equal(before.Columns.Select(c => c.ProbableKind), after.Columns.Select(c => c.ProbableKind));
    }

    [Fact]
    public void Anonymize_SelectedColumn_PreservesShapeHeaderAndOtherColumns()
    {
        var input = TestFiles.Utf8("id;name;code\r\n001;Mario Rossi;AB-12\r\n002;\"Anna; Bianchi\";CD-34\r\n");

        var lines = Encoding.UTF8.GetString(TestFiles.Anonymize(input, columns: [1])).Split("\r\n");

        Assert.Equal("id;name;code", lines[0]);
        Assert.Matches("^001;[A-Z][a-z]{4} [A-Z][a-z]{4};AB-12$", lines[1]);
        Assert.Matches("^002;\"[A-Z][a-z]{3}; [A-Z][a-z]{6}\";CD-34$", lines[2]);
        Assert.DoesNotContain("Mario Rossi", lines[1]);
        Assert.Equal(string.Empty, lines[3]);
    }

    [Fact]
    public void Anonymize_SameSeed_ProducesSameOutput()
    {
        var input = TestFiles.Utf8("name\nMario Rossi\nAnna Bianchi\n");

        Assert.Equal(TestFiles.Anonymize(input, [0], seed: 7), TestFiles.Anonymize(input, [0], seed: 7));
        Assert.NotEqual(TestFiles.Anonymize(input, [0], seed: 7), TestFiles.Anonymize(input, [0], seed: 8));
    }

    [Fact]
    public void Anonymize_LeadingZeros_ArePreserved()
    {
        var input = TestFiles.Utf8("code\n007\n0100\n");

        var lines = Encoding.UTF8.GetString(TestFiles.Anonymize(input, [0])).Split('\n');

        Assert.Matches("^00[1-9]$", lines[1]);
        Assert.Matches("^0[1-9]00$", lines[2]);
    }

    [Fact]
    public void Anonymize_Dates_StayValidWithSamePatternAndOrderEvidence()
    {
        var input = TestFiles.Utf8("d\n25/03/2020\n05/11/1999\n2021-07-04\n7.3.21 9:05\n");

        var lines = Encoding.UTF8.GetString(TestFiles.Anonymize(input, [0])).Split('\n');

        var dayFirst = ValueClassifier.Classify(lines[1]);
        Assert.Equal((ValueKind.Date, "nn/nn/nnnn", DateOrder.DayFirst), (dayFirst.Kind, dayFirst.Pattern, dayFirst.Order));
        Assert.Matches("^0[1-9]/1[0-2]/(19|20)[0-9]{2}$", lines[2]);
        Assert.Equal(ValueKind.Date, ValueClassifier.Classify(lines[3]).Kind);
        var shortDate = ValueClassifier.Classify(lines[4]);
        Assert.Equal((ValueKind.DateTime, "n.n.nn n:nn"), (shortDate.Kind, shortDate.Pattern));
    }

    [Fact]
    public void Anonymize_Windows1252_KeepsOtherColumnsByteIdentical()
    {
        var input = TestFiles.Windows1252("Città;Nome\r\nForlì;Niccolò\r\n");

        var output = TestFiles.Anonymize(input, [1]);

        var expectedPrefix = TestFiles.Windows1252("Città;Nome\r\nForlì;");
        Assert.Equal(expectedPrefix, output[..expectedPrefix.Length]);
        Assert.Equal(input.Length, output.Length);
        Assert.Equal(TestFiles.Windows1252("\r\n"), output[^2..]);
    }
}
