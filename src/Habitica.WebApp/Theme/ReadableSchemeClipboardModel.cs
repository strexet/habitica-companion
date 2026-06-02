using System.Text.Json.Serialization;

namespace Habitica.WebApp.Theme;

public sealed record ReadableSchemeClipboardModel(
    [property: JsonPropertyName("$schema")] string Schema,
    string Name,
    string Variant,
    string Description,
    IReadOnlyDictionary<string, string> Colors,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? Gradients = null,
    IReadOnlyDictionary<string, string?>? TextShadows = null);

public enum SchemeParseResult
{
    Success,
    Empty,
    InvalidJson,
    NotAnObject,
    MissingName,
    NoTokens,
    InvalidValue,
    PartialGradient,
    ConflictingAliases
}

public sealed record ReadableSchemeParseOutcome(
    SchemeParseResult Result,
    ColorSchemeDefinition? Scheme = null,
    string? Detail = null);
