using System.Text;
using Fedele.Core;

namespace Fedele.Formats.Csv.Tests;

public sealed class CsvReaderTests
{
    private static readonly Encoding _windows1252 = new TextEncodingInfo(TextEncodingKind.Windows1252, 0).Encoding;

    [Fact]
    public async Task ReadRecordsAsync_CommaFile_ReadsHeaderAndRecordsWithPositions()
    {
        var result = await CsvTestReader.ReadAsync("ID,Nome\r\n1,Rossi\r\n2,Bianchi\r\n");

        Assert.Equal(',', result.Layout.Separator.Value);
        Assert.Equal(["ID", "Nome"], result.Layout.ColumnNames);
        Assert.Equal([["1", "Rossi"], ["2", "Bianchi"]], result.Texts);
        Assert.Equal([2L, 3L], result.Records.Select(record => record.Number));
        Assert.Equal([2L, 3L], result.Records.Select(record => record.Line));
        Assert.Equal([9L, 18L], result.Records.Select(record => record.ByteOffset));
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ReadRecordsAsync_ItalianSemicolonFile_KeepsNumbersAsWritten()
    {
        var result = await CsvTestReader.ReadAsync("Nome;Importo;Data\r\nRossi;1.234,56;05/03/2020\r\n");

        Assert.Equal(';', result.Layout.Separator.Value);
        Assert.Equal([["Rossi", "1.234,56", "05/03/2020"]], result.Texts);
    }

    [Fact]
    public async Task ReadRecordsAsync_QuotedFields_UnescapesQuotesAndKeepsLineBreaks()
    {
        var result = await CsvTestReader.ReadAsync("a,b\r\n\"x,y\",\"he said \"\"hi\"\"\"\r\n\"line1\r\nline2\",3\r\nlast,4\r\n");

        Assert.Equal([["x,y", "he said \"hi\""], ["line1\r\nline2", "3"], ["last", "4"]], result.Texts);
        Assert.Equal([true, true], result.Records[0].Fields.Select(field => field.Quoted));
        Assert.Equal([2L, 3L, 5L], result.Records.Select(record => record.Line));
        Assert.Equal(LineEndingStyle.CrLf, result.Summary.LineEnding);
        Assert.Equal(4, result.Summary.CrLfCount);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ReadRecordsAsync_SpacesAroundValues_AreKept()
    {
        var result = await CsvTestReader.ReadAsync("a,b\n 1 , x\n");

        Assert.Equal([[" 1 ", " x"]], result.Texts);
    }

    [Fact]
    public async Task ReadRecordsAsync_Utf8Bom_IsSkippedButCountedInOffsets()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("Città,N\r\nPerù,1\r\n")];

        var result = await CsvTestReader.ReadAsync(bytes);

        Assert.Equal(new TextEncodingInfo(TextEncodingKind.Utf8, 3), result.Layout.Encoding.Value);
        Assert.Equal(["Città", "N"], result.Layout.ColumnNames);
        Assert.Equal([["Perù", "1"]], result.Texts);
        Assert.Equal(3 + 10, result.Records[0].ByteOffset);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ReadRecordsAsync_Utf16InTinyChunks_ReadsEveryCharacter(bool bigEndian, bool withBom)
    {
        var encoding = new UnicodeEncoding(bigEndian, byteOrderMark: false);
        byte[] bytes = [.. withBom ? encoding.GetPreamble() : [], .. encoding.GetBytes("Nome;Città\r\nRossi;Forlì\r\n\"Bi;anchi\";Aosta\r\n")];
        if (withBom)
        {
            bytes = [.. new UnicodeEncoding(bigEndian, byteOrderMark: true).GetPreamble(), .. bytes];
        }

        var result = await CsvTestReader.ReadAsync(bytes, maxChunk: 1);

        Assert.Equal(["Nome", "Città"], result.Layout.ColumnNames);
        Assert.Equal([["Rossi", "Forlì"], ["Bi;anchi", "Aosta"]], result.Texts);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ReadRecordsAsync_Windows1252_DecodesAccentsAndEuro()
    {
        var result = await CsvTestReader.ReadAsync(_windows1252.GetBytes("Città;Prezzo\r\nForlì;€ 12,50\r\n"));

        Assert.Equal(TextEncodingKind.Windows1252, result.Layout.Encoding.Value.Kind);
        Assert.Equal([["Forlì", "€ 12,50"]], result.Texts);
        Assert.Equal("windows-1252", result.Summary.Encoding.Id);
    }

    [Fact]
    public async Task Summary_AsciiSampleWithUtf8Later_ConfirmsUtf8()
    {
        var text = "ID,Nome\r\n" + string.Concat(Enumerable.Range(1, 8000).Select(i => $"{i},Rossi\r\n")) + "8001,Niccolò\r\n";

        var result = await CsvTestReader.ReadAsync(text);

        Assert.Equal(TextEncodingKind.Ascii, result.Layout.Encoding.Value.Kind);
        Assert.Equal(TextEncodingKind.Utf8, result.Summary.Encoding.Kind);
        Assert.Equal("Niccolò", result.Records[^1].Fields[1].Text);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ReadRecordsAsync_InvalidUtf8AfterSample_ReportsInvalidByteWithColumn()
    {
        var head = Encoding.UTF8.GetBytes("ID,Città\r\n" + string.Concat(Enumerable.Range(1, 8000).Select(i => $"{i},Forlì\r\n")));
        byte[] bytes = [.. head, .. "8001,Forl"u8, 0xEC, .. "\r\n"u8];

        var result = await CsvTestReader.ReadAsync(bytes);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueCodes.InvalidByte, issue.Code);
        Assert.Equal(new IssueLocation(8002, 8002, 2, "Città"), issue.Location);
        Assert.Equal("Forl�", result.Records[^1].Fields[1].Text);
    }

    [Fact]
    public async Task ReadRecordsAsync_RecordOfOnlySeparators_ReportsEmptyRecord()
    {
        var result = await CsvTestReader.ReadAsync("a,b,c\r\n1,2,3\r\n,,\r\n");

        Assert.Equal([(CsvIssueCodes.EmptyRecord, 3L)], result.IssueKeys);
        Assert.Equal(2, result.Summary.Records);
    }

    [Fact]
    public async Task ReadRecordsAsync_BlankLines_AreSkippedAndNotNumbered()
    {
        var result = await CsvTestReader.ReadAsync("a,b\r\n\r\n1,2\r\n\r\n\r\n3,4\r\n");

        Assert.Equal([2L, 3L], result.Records.Select(record => record.Number));
        Assert.Equal([3L, 6L], result.Records.Select(record => record.Line));
        Assert.Equal(3, result.Summary.BlankLines);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ReadRecordsAsync_WrongFieldCount_ReportsMismatch()
    {
        var result = await CsvTestReader.ReadAsync("a,b,c\n1,2,3\n4,5\n6,7,8,9\n");

        Assert.Equal([(CsvIssueCodes.FieldCountMismatch, 3L), (CsvIssueCodes.FieldCountMismatch, 4L)], result.IssueKeys);
        Assert.Equal([["4", "5"], ["6", "7", "8", "9"]], result.Texts.Skip(1));
    }

    [Fact]
    public async Task ReadRecordsAsync_UnterminatedQuote_ReportsErrorAndKeepsText()
    {
        var result = await CsvTestReader.ReadAsync("a,b\n1,\"open\n2,3\n");

        var issue = Assert.Single(result.Issues);
        Assert.Equal((CsvIssueCodes.UnterminatedQuote, IssueSeverity.Error), (issue.Code, issue.Severity));
        Assert.Equal(new IssueLocation(2, 4, 2, "b"), issue.Location);
        Assert.Equal([["1", "open\n2,3\n"]], result.Texts);
    }

    [Fact]
    public async Task ReadRecordsAsync_MalformedQuotes_AreReportedOncePerField()
    {
        var result = await CsvTestReader.ReadAsync("a,b\n\"abc\"def,x\"y\"z\n");

        Assert.Equal([(CsvIssueCodes.TextAfterClosingQuote, 2L), (CsvIssueCodes.StrayQuote, 2L)], result.IssueKeys);
        Assert.Equal([1, 2], result.Issues.Select(issue => issue.Location!.Column!.Value));
        Assert.Equal([["abcdef", "x\"y\"z"]], result.Texts);
    }

    [Theory]
    [InlineData("a,b\n1,2\n", LineEndingStyle.Lf, true)]
    [InlineData("a,b\r1,2\r", LineEndingStyle.Cr, true)]
    [InlineData("a,b\r\n1,2\n", LineEndingStyle.Mixed, true)]
    [InlineData("a,b\r\n1,2", LineEndingStyle.CrLf, false)]
    [InlineData("a,b", LineEndingStyle.None, false)]
    public async Task Summary_LineEndings_AreMeasured(string text, LineEndingStyle expected, bool finalLineEnding)
    {
        var result = await CsvTestReader.ReadAsync(text);

        Assert.Equal(expected, result.Summary.LineEnding);
        Assert.Equal(finalLineEnding, result.Summary.FinalLineEnding);
    }

    [Fact]
    public async Task ReadRecordsAsync_CrOnlyFile_CountsLines()
    {
        var result = await CsvTestReader.ReadAsync("a,b\r1,2\r3,4");

        Assert.Equal([2L, 3L], result.Records.Select(record => record.Line));
        Assert.Equal([["1", "2"], ["3", "4"]], result.Texts);
    }

    [Fact]
    public async Task ReadRecordsAsync_EmptyFile_HasNoRecordsAndNoHeader()
    {
        var result = await CsvTestReader.ReadAsync([]);

        Assert.Empty(result.Records);
        Assert.Null(result.Layout.ColumnNames);
        Assert.Equal(0, result.Summary.Records);
        Assert.Equal(LineEndingStyle.None, result.Summary.LineEnding);
    }

    [Fact]
    public async Task ReadRecordsAsync_HeaderAbsent_FirstRecordIsData()
    {
        var result = await CsvTestReader.ReadAsync("1,2\n3,4\n", new CsvReadOptions { Header = HeaderMode.Absent });

        Assert.Null(result.Layout.ColumnNames);
        Assert.Equal([1L, 2L], result.Records.Select(record => record.Number));
        Assert.Equal(2, result.Summary.Records);
    }

    [Fact]
    public async Task OpenCsvAsync_SeparatorInOptions_IsUsedWithFullConfidence()
    {
        var result = await CsvTestReader.ReadAsync("a|b,c\n1|2,3\n", new CsvReadOptions { Separator = '|' });

        Assert.Equal(new Detected<char?>('|', 1.0, "Set in the read options."), result.Layout.Separator);
        Assert.Equal([["1", "2,3"]], result.Texts);
    }

    [Fact]
    public async Task OpenCsvAsync_SeparatorEqualToQuote_Throws()
    {
        await using var stream = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await new CsvFormatReader().OpenCsvAsync(stream, new CsvReadOptions { Separator = '"' }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadRecordsAsync_CalledTwice_Throws()
    {
        await using var stream = new MemoryStream("a\n1\n"u8.ToArray());
        await using var reader = await new CsvFormatReader().OpenAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        _ = reader.ReadRecordsAsync(TestContext.Current.CancellationToken);

        Assert.Throws<InvalidOperationException>(() => reader.ReadRecordsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Issues_BeyondMaxIssues_AreCountedNotKept()
    {
        var text = "a,b\n" + string.Concat(Enumerable.Repeat(",\n", 10));

        var result = await CsvTestReader.ReadAsync(text, new CsvReadOptions { MaxIssues = 3 });

        Assert.Equal(3, result.Issues.Count);
        Assert.Equal(7, result.DroppedIssues);
    }

    [Fact]
    public async Task OpenAsync_TwoSeparatorsEquallyGood_ReportsAmbiguity()
    {
        var result = await CsvTestReader.ReadAsync("a,b;c\n1,2;3\n4,5;6\n");

        Assert.Equal([(CsvIssueCodes.AmbiguousSeparator, 0L)], result.IssueKeys);
        Assert.True(result.Layout.Separator.Confidence <= 0.5);
        Assert.Contains("guess", result.Layout.Separator.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAsync_SingleColumn_HasNoSeparator()
    {
        var result = await CsvTestReader.ReadAsync("Nome\nRossi\nBianchi\n");

        Assert.Null(result.Layout.Separator.Value);
        Assert.Equal([["Rossi"], ["Bianchi"]], result.Texts);
    }

    [Fact]
    public async Task ReadRecordsAsync_LargeFileInSmallChunks_MatchesWholeRead()
    {
        var text = "ID;Note\r\n" + string.Concat(Enumerable.Range(1, 5000).Select(i => $"{i};\"riga {i}\r\ncon a capo; e \"\"virgolette\"\"\"\r\n"));
        var bytes = Encoding.UTF8.GetBytes(text);

        var whole = await CsvTestReader.ReadAsync(bytes);
        var chunked = await CsvTestReader.ReadAsync(bytes, maxChunk: 7);

        Assert.Equal(5000, chunked.Records.Count);
        Assert.Equal(whole.Texts, chunked.Texts);
        Assert.Equal(whole.Records.Select(r => (r.Number, r.Line, r.ByteOffset)), chunked.Records.Select(r => (r.Number, r.Line, r.ByteOffset)));
        Assert.Equal("riga 5000\r\ncon a capo; e \"virgolette\"", chunked.Records[^1].Fields[1].Text);
        Assert.Equal(10_000, chunked.Records[^1].Line);
        Assert.Empty(chunked.Issues);
    }

    [Fact]
    public async Task ByteOffset_EveryRecord_PointsToItsFirstByte()
    {
        var bytes = Encoding.UTF8.GetBytes("a,b\r\n\r\nCittà,1\n\"x\r\ny\",2\r3,4");

        var result = await CsvTestReader.ReadAsync(bytes);

        Assert.All(result.Records, record =>
        {
            var first = record.Fields[0];
            var expected = first.Quoted ? (byte)'"' : Encoding.UTF8.GetBytes(first.Text)[0];
            Assert.Equal(expected, bytes[record.ByteOffset]);
        });
    }
}
