using System.Runtime.CompilerServices;

namespace JRK.JevRunner;

/// <summary>
/// Provides range validation shared by probability and confidence value types.
/// </summary>
public static class ErrorMessages
{
    /// <summary>
    /// Throws when the supplied value is not finite or lies outside the inclusive range from zero to one.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name reported by the exception; defaults to the caller's value expression.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is non-finite, less than zero, or greater than one.</exception>
    public static void ThrowIfNotBetween0And1(double value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(paramName,  "Value must  be between 0 and 1 and not finite");
        }
    }
}
