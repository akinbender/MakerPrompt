namespace MakerPrompt.Shared.ShapeIt.Parameters;

/// <summary>
/// Represents a parameter value update.
/// </summary>
/// <param name="Name">The parameter name/identifier.</param>
/// <param name="Value">The new value for this parameter.</param>
public record CadParameterValue(
    string Name,
    object? Value
);
