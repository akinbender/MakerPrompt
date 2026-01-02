namespace MakerPrompt.Shared.ShapeIt.Parameters;

/// <summary>
/// Defines the type of a CAD parameter.
/// </summary>
public enum CadParameterKind
{
    /// <summary>
    /// A numeric parameter (e.g., length, angle, count).
    /// </summary>
    Numeric,

    /// <summary>
    /// A text/string parameter.
    /// </summary>
    Text,

    /// <summary>
    /// A boolean parameter (e.g., enable/disable).
    /// </summary>
    Boolean,

    /// <summary>
    /// An enumeration parameter with a fixed set of choices.
    /// </summary>
    Choice
}
