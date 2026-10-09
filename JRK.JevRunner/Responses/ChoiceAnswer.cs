using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace JRK.JevRunner.Responses;

/// <summary>
/// Contains the selected choice, its option probabilities, and distribution confidence.
/// </summary>
/// <param name="Choice">The selected option's key.</param>
/// <param name="Probabilities">The probability assigned to each option.</param>
/// <param name="Confidence">Distribution concentration, not a measure of answer accuracy.</param>
public record ChoiceAnswer(string Choice, ImmutableArray<Probability> Probabilities, Confidence Confidence)
{
    /// <summary>
    /// Parses the <c>choice</c>, <c>probabilities</c>, and <c>confidence</c> fields of an answer.
    /// </summary>
    /// <remarks>Option probabilities retain the JSON object's enumeration order.</remarks>
    public static ChoiceAnswer From(JsonObject obj)
    {
        var choice = obj["choice"]!.GetValue<string>();

        var probabilities = ImmutableArray.Create<Probability>();
        
        foreach (var keyValuePair in obj["probabilities"]!.AsObject())
        {
            probabilities = probabilities.Add(new(keyValuePair.Key, keyValuePair.Value!.GetValue<double>()));
        }

        Confidence confidence = obj["confidence"]!.GetValue<double>();
        return new ChoiceAnswer(choice, probabilities, confidence);
    }
}
