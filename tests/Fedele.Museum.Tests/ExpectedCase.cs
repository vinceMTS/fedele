using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fedele.Museum.Tests;

/// <summary>The content of a case's expected.json (docs/decisioni/0006-formato-expected-json.md).</summary>
internal sealed record ExpectedCase
{
    public required string Description { get; init; }

    public required ExpectedSource Source { get; init; }

    public required ExpectedFile File { get; init; }

    public required IReadOnlyList<ExpectedColumn> Columns { get; init; }

    public required IReadOnlyList<ExpectedIssue> Issues { get; init; }

    // Strict: unknown properties, missing required ones and nulls in non-nullable ones are errors.
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
    };

    public static ExpectedCase Parse(string json) =>
        JsonSerializer.Deserialize<ExpectedCase>(json, _options)
        ?? throw new JsonException("expected.json is null.");
}

internal sealed record ExpectedSource
{
    public required string Kind { get; init; }

    public string? Note { get; init; }
}

internal sealed record ExpectedFile
{
    public required string Encoding { get; init; }

    public required bool Bom { get; init; }

    public required string LineEnding { get; init; }

    public required bool FinalLineEnding { get; init; }

    public required string Separator { get; init; }

    public required string Quote { get; init; }

    public required bool Header { get; init; }

    public required long Records { get; init; }

    public required int FieldsPerRecord { get; init; }
}

internal sealed record ExpectedColumn
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public string? Semantic { get; init; }

    public string? DecimalSeparator { get; init; }

    public string? ThousandsSeparator { get; init; }

    public string? Format { get; init; }
}

internal sealed record ExpectedIssue
{
    public required string Code { get; init; }

    public required long Record { get; init; }

    public required string? Column { get; init; }
}

/// <summary>The allowed values of ADR 0006.</summary>
internal static class Vocabulary
{
    public static readonly string[] SourceKinds = ["full-copy", "prefix"];

    public static readonly string[] Encodings = ["ascii", "utf-8", "utf-16le", "utf-16be", "windows-1252"];

    public static readonly string[] LineEndings = ["crlf", "lf", "cr", "mixed"];

    public static readonly string[] Types = ["text", "integer", "decimal", "date", "datetime", "boolean"];

    public static readonly string[] IssueCodes = ["empty-record"];
}
