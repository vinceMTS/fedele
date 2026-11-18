namespace Fedele.Anonymizer.Tests;

public sealed class ValueClassifierTests
{
    [Theory]
    [InlineData("", ValueKind.Empty)]
    [InlineData("00123", ValueKind.Integer)]
    [InlineData("-5", ValueKind.Integer)]
    [InlineData("1.234,56", ValueKind.DecimalComma)]
    [InlineData("12,5", ValueKind.DecimalComma)]
    [InlineData("1.234.567", ValueKind.DecimalComma)]
    [InlineData("1,234.56", ValueKind.DecimalDot)]
    [InlineData("12.5", ValueKind.DecimalDot)]
    [InlineData("1.234", ValueKind.AmbiguousNumber)]
    [InlineData("1,234", ValueKind.AmbiguousNumber)]
    [InlineData("25/12/2021", ValueKind.Date)]
    [InlineData("25.12.21", ValueKind.Date)]
    [InlineData("2021-12-25", ValueKind.Date)]
    [InlineData("2021-12-25T10:30:00", ValueKind.DateTime)]
    [InlineData("25/12/2021 10:30", ValueKind.DateTime)]
    [InlineData("45/67/2020", ValueKind.Text)]
    [InlineData("RSSMRA85T10A562S", ValueKind.TaxCode)]
    [InlineData("12345678903", ValueKind.VatNumber)]
    [InlineData("IT12345678903", ValueKind.VatNumber)]
    [InlineData("12345678901", ValueKind.Integer)]
    [InlineData("IT60X0542811101000000123456", ValueKind.Iban)]
    [InlineData("IT60 X054 2811 1010 0000 0123 456", ValueKind.Iban)]
    [InlineData("IT61X0542811101000000123456", ValueKind.Text)]
    [InlineData("mario.rossi@example.com", ValueKind.Email)]
    [InlineData("+39 333 1234567", ValueKind.Phone)]
    [InlineData("333 1234567", ValueKind.Phone)]
    [InlineData("Via Roma 1", ValueKind.Text)]
    public void Classify_Value_ReturnsKind(string value, ValueKind expected)
    {
        Assert.Equal(expected, ValueClassifier.Classify(value).Kind);
    }

    [Theory]
    [InlineData("25/03/2020", DateOrder.DayFirst)]
    [InlineData("03/25/2020", DateOrder.MonthFirst)]
    [InlineData("05/03/2020", DateOrder.Ambiguous)]
    public void Classify_DayMonthYear_ReportsOrderEvidence(string value, DateOrder expected)
    {
        Assert.Equal(expected, ValueClassifier.Classify(value).Order);
    }

    [Theory]
    [InlineData("007", true)]
    [InlineData("0", false)]
    [InlineData("100", false)]
    public void Classify_Integer_ReportsLeadingZero(string value, bool expected)
    {
        Assert.Equal(expected, ValueClassifier.Classify(value).LeadingZero);
    }
}
