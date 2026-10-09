namespace JRK.JevRunner.Requests;

/// <summary>
/// Identifies the model to use when executing a request.
/// </summary>
/// <param name="Model">The model identifier sent to the API.</param>
public record JevModel(string Model)
{
    /// <summary>
    /// Gets the model identified by <c>jev-latest</c>.
    /// </summary>
    public static JevModel Latest => new JevModel("jev-latest");
    
    /// <summary>
    /// Specific model for 1.13.0
    /// </summary>
    public static JevModel Version1_13_0 => new JevModel("jev-1.13.0");
}
