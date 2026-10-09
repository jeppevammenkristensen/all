using System.Text.Json.Nodes;

namespace JRK.JevRunner.Responses;

/// <summary>
/// Contains the probability of yes for a noul question, without separate confidence.
/// </summary>
/// <param name="Noul">The probability that the answer is yes.</param>
public record NoulAnswer(Noul Noul)
{
    /// <summary>
    /// Parses the answer's numeric <c>noul</c> field as a validated probability.
    /// </summary>
    public static NoulAnswer From(JsonObject obj)
    {
        Noul noul = (Noul)obj["noul"]!.GetValue<double>();
        
        return new NoulAnswer(noul);
    }
}
