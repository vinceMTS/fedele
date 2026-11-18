namespace Fedele.Anonymizer.Tests;

public sealed class FileAnalyzerTests
{
    [Fact]
    public void Analyze_SemicolonFileWithCommaDecimals_DetectsStructureAndTypes()
    {
        var analysis = TestFiles.Analyze(TestFiles.Utf8("Codice;Importo;Data\r\n001;1.234,56;05/03/2020\r\n002;12,50;25/12/2021\r\n"));

        Assert.Equal(EncodingKind.Ascii, analysis.Encoding.Kind);
        Assert.Equal(';', analysis.Separator.Separator);
        Assert.Equal(1.0, analysis.Separator.Consistency);
        Assert.Equal((3L, 0L, 0L), (analysis.CrLf, analysis.Lf, analysis.Cr));
        Assert.Equal(3, analysis.Records);
        Assert.Equal(["Codice", "Importo", "Data"], analysis.HeaderNames!);
        Assert.Equal(ValueKind.Integer, analysis.Columns[0].ProbableKind);
        Assert.Equal(2, analysis.Columns[0].LeadingZeros);
        Assert.Equal(2, analysis.Columns[1].Kinds[ValueKind.DecimalComma]);
        Assert.Equal(2, analysis.Columns[2].Kinds[ValueKind.Date]);
        Assert.Equal((1L, 0L), (analysis.Columns[2].DayFirstProofs, analysis.Columns[2].MonthFirstProofs));
    }

    [Fact]
    public void Analyze_QuotedLineBreak_CountsOneRecord()
    {
        var analysis = TestFiles.Analyze(TestFiles.Variants["quoted-multiline"]);

        Assert.Equal(2, analysis.Records);
        Assert.Equal(1, analysis.QuotedFields);
        Assert.Equal(1, analysis.FieldsWithLineBreaks);
        Assert.Equal(2, analysis.CrLf);
    }

    [Fact]
    public void Analyze_EmptyAndIrregularRecords_ReportsRecordNumbers()
    {
        var analysis = TestFiles.Analyze(TestFiles.Utf8("a;b;c\n\n1;2;3\n1;2\n1;2;3;4\n"));

        Assert.Equal(4, analysis.Records);
        Assert.Equal(1, analysis.EmptyRecords);
        Assert.Equal(2, analysis.FieldCounts[3]);
        Assert.Equal([4L, 5L], analysis.IrregularRecords);
    }

    [Fact]
    public void Analyze_MixedLineEndings_CountsEachKind()
    {
        var analysis = TestFiles.Analyze(TestFiles.Variants["mixed-line-endings"]);

        Assert.Equal((1L, 1L, 1L), (analysis.CrLf, analysis.Lf, analysis.Cr));
        Assert.False(analysis.EndsWithLineEnding);
        Assert.Equal(4, analysis.Records);
    }

    [Fact]
    public void Analyze_MalformedQuotes_AreCounted()
    {
        Assert.Equal(1, TestFiles.Analyze(TestFiles.Variants["unterminated-quote"]).UnterminatedQuotes);

        var malformed = TestFiles.Analyze(TestFiles.Variants["text-after-quote-and-stray-quote"]);
        Assert.Equal((1L, 1L), (malformed.TextAfterClosingQuote, malformed.StrayQuotes));
    }

    [Fact]
    public void Analyze_InvalidUtf8_IsWindows1252()
    {
        var analysis = TestFiles.Analyze(TestFiles.Windows1252("Città;Prezzo\r\nRoma;1,50\r\n"));

        Assert.Equal(EncodingKind.SingleByte, analysis.Encoding.Kind);
        Assert.Equal("Città", analysis.HeaderNames![0]);
    }

    [Fact]
    public void Analyze_Utf8WithBom_ReportsBom()
    {
        var analysis = TestFiles.Analyze(TestFiles.Variants["utf8-bom-crlf"]);

        Assert.Equal(new DetectedEncoding(EncodingKind.Utf8, 3), analysis.Encoding);
        Assert.Equal("Codice", analysis.HeaderNames![0]);
    }

    [Theory]
    [InlineData(false, true, EncodingKind.Utf16LittleEndian, 2)]
    [InlineData(true, true, EncodingKind.Utf16BigEndian, 2)]
    [InlineData(false, false, EncodingKind.Utf16LittleEndian, 0)]
    [InlineData(true, false, EncodingKind.Utf16BigEndian, 0)]
    public void Analyze_Utf16_DetectsEncodingAndHeader(bool bigEndian, bool bom, EncodingKind expected, int bomLength)
    {
        var analysis = TestFiles.Analyze(TestFiles.Utf16("Nome;Età\r\nAnna;30\r\n", bigEndian, bom));

        Assert.Equal(new DetectedEncoding(expected, bomLength), analysis.Encoding);
        Assert.Equal(';', analysis.Separator.Separator);
        Assert.Equal(["Nome", "Età"], analysis.HeaderNames!);
    }

    [Fact]
    public void Analyze_SingleColumn_HasNoSeparator()
    {
        var analysis = TestFiles.Analyze(TestFiles.Utf8("Nome\nAnna\nLuca\n"));

        Assert.Null(analysis.Separator.Separator);
        Assert.Equal(3, analysis.Records);
    }

    [Fact]
    public void Report_DataValues_AreNeverPrinted()
    {
        var report = TestFiles.Report(TestFiles.Utf8("Nome;Codice\nSENTINELONE;SENTINELTWO\n\"SENTINEL;THREE\";SENTINELFOUR\n"));

        Assert.Contains("Nome", report);
        Assert.Contains("Codice", report);
        Assert.DoesNotContain("SENTINEL", report);
    }

    [Fact]
    public void Report_FirstRecordNotLikeHeader_HidesIt()
    {
        var report = TestFiles.Report(TestFiles.Utf8("SENTINELNAME;12345\nSENTINELOTHER;678\n"));

        Assert.Contains("not detected", report);
        Assert.DoesNotContain("SENTINEL", report);
    }
}
