using System.Text.RegularExpressions;

namespace Fedele.Museum.Tests;

/// <summary>
/// Runs on every case of museum/. It checks that each case is complete and that
/// expected.json agrees with the bytes of the input, independently of Fedele.
/// FedeleReadingTests compares expected.json with what Fedele itself finds.
/// </summary>
public sealed partial class MuseumTests
{
    public static TheoryData<string> Cases => MuseumCases.TheoryData();

    [Fact]
    public void Museum_Folders_AreNumberedCasesWithUniqueNumbers()
    {
        var names = MuseumCases.Names();

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.Matches(MuseumCases.CaseNameRegex(), name));
        Assert.Equal(names.Count, names.Select(name => name[..4]).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Case_Files_AreComplete(string caseName)
    {
        var directory = MuseumCases.CaseDirectory(caseName);

        Assert.Single(Directory.EnumerateFiles(directory, "input.*"));
        Assert.True(File.Exists(Path.Combine(directory, "expected.json")), "expected.json is missing.");
        Assert.True(File.Exists(Path.Combine(directory, "README.md")), "README.md is missing.");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExpectedJson_Contents_FollowAdr0006(string caseName)
    {
        var expected = MuseumCases.Expected(caseName);

        Assert.False(string.IsNullOrWhiteSpace(expected.Description));
        Assert.Contains(expected.Source.Kind, Vocabulary.SourceKinds);

        var file = expected.File;
        Assert.Contains(file.Encoding, Vocabulary.Encodings);
        Assert.Contains(file.LineEnding, Vocabulary.LineEndings);
        Assert.Single(file.Separator);
        Assert.Single(file.Quote);
        Assert.True(file.Records >= 0);
        Assert.True(file.FieldsPerRecord >= 1);

        Assert.Equal(file.FieldsPerRecord, expected.Columns.Count);
        Assert.Equal(expected.Columns.Count, expected.Columns.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(expected.Columns, CheckColumn);

        var names = expected.Columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var lastRecord = file.Records + (file.Header ? 1 : 0);
        Assert.All(expected.Issues, issue =>
        {
            Assert.Contains(issue.Code, Vocabulary.IssueCodes);
            Assert.InRange(issue.Record, 1, lastRecord);
            Assert.True(issue.Column is null || names.Contains(issue.Column), $"Unknown column in issue: {issue.Column}");
        });
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Input_Encoding_MatchesExpected(string caseName)
    {
        var file = MuseumCases.Expected(caseName).File;
        var bytes = File.ReadAllBytes(MuseumCases.InputPath(caseName));

        var bomLength = InputFacts.BomLength(bytes, file.Encoding);
        Assert.Equal(file.Bom, bomLength > 0);

        // Decoding is strict: an invalid byte for the declared encoding throws.
        InputFacts.Decode(bytes, file.Encoding, bomLength);
        if (file.Encoding == "windows-1252")
        {
            Assert.False(InputFacts.IsValidUtf8(bytes), "The file is valid UTF-8: declare utf-8 or ascii, not windows-1252.");
        }

        if (file.Encoding == "utf-8")
        {
            Assert.True(bytes.Skip(bomLength).Any(b => b >= 0x80), "The file has only ASCII bytes: declare ascii.");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Input_LineEndingsAndRecords_MatchExpected(string caseName)
    {
        var expected = MuseumCases.Expected(caseName);
        var file = expected.File;
        var bytes = File.ReadAllBytes(MuseumCases.InputPath(caseName));
        var text = InputFacts.Decode(bytes, file.Encoding, InputFacts.BomLength(bytes, file.Encoding));

        var facts = InputFacts.Scan(text, file.Separator[0], file.Quote[0]);

        Assert.Equal(file.LineEnding, facts.LineEnding);
        Assert.Equal(file.FinalLineEnding, facts.FinalLineEnding);
        Assert.Equal(file.Records, facts.Records - (file.Header ? 1 : 0));
        Assert.Equal(file.FieldsPerRecord, facts.FirstRecord.Count);
        if (file.Header)
        {
            Assert.Equal(expected.Columns.Select(c => c.Name), facts.FirstRecord);
        }
    }

    [Fact]
    public void Scan_QuotedLineBreaksAndEscapedQuotes_AreInsideFields()
    {
        var facts = InputFacts.Scan("a,\"b\"\"c\"\r\n\"x\ny\",2\r\n,\r\n\r\n", ',', '"');

        Assert.Equal((4L, 0L, 0L), (facts.CrLf, facts.Lf, facts.Cr));
        Assert.Equal(3, facts.Records);
        Assert.Equal(["a", "b\"c"], facts.FirstRecord);
        Assert.True(facts.FinalLineEnding);
    }

    [Fact]
    public void ExpectedCase_UnknownProperty_IsRejected()
    {
        var json = File.ReadAllText(Path.Combine(MuseumCases.CaseDirectory(MuseumCases.Names()[0]), "expected.json"));
        var withTypo = json.Replace("\"records\"", "\"recrods\"", StringComparison.Ordinal);

        Assert.ThrowsAny<System.Text.Json.JsonException>(() => ExpectedCase.Parse(withTypo));
    }

    private static void CheckColumn(ExpectedColumn column)
    {
        Assert.False(string.IsNullOrWhiteSpace(column.Name));
        Assert.Contains(column.Type, Vocabulary.Types);

        if (column.Semantic is not null)
        {
            Assert.Matches(SemanticIdRegex(), column.Semantic);
        }

        if (column.Type == "decimal")
        {
            Assert.Contains(column.DecimalSeparator, new[] { ".", "," });
            Assert.NotEqual(column.DecimalSeparator, column.ThousandsSeparator);
        }
        else
        {
            Assert.Null(column.DecimalSeparator);
            Assert.Null(column.ThousandsSeparator);
        }

        if (column.Type is "date" or "datetime")
        {
            Assert.False(string.IsNullOrWhiteSpace(column.Format), $"Column {column.Name} needs a format.");
        }
        else
        {
            Assert.Null(column.Format);
        }
    }

    // "phone", "email", "iban", "it.tax-code", "it.vat-number"
    [GeneratedRegex("^([a-z]{2}\\.)?[a-z]+(-[a-z]+)*$")]
    private static partial Regex SemanticIdRegex();
}
