namespace MakerPrompt.Shared.ShapeIt.Parameters;

/// <summary>
/// Describes a CAD parameter that can be modified by the user.
/// </summary>
/// <param name="Name">The parameter name/identifier.</param>
/// <param name="DisplayName">Human-readable display name.</param>
/// <param name="Kind">The type of parameter.</param>
/// <param name="DefaultValue">The default value for this parameter.</param>
/// <param name="MinValue">Optional minimum value (for numeric parameters).</param>
/// <param name="MaxValue">Optional maximum value (for numeric parameters).</param>
/// <param name="Choices">Optional list of choices (for choice parameters).</param>
public record CadParameterDescriptor(
    string Name,
    string DisplayName,
    CadParameterKind Kind,
    object? DefaultValue,
    double? MinValue = null,
    double? MaxValue = null,
    IReadOnlyList<string>? Choices = null
);
