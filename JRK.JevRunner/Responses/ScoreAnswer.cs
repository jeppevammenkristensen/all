using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace JRK.JevRunner.Responses;

/// <summary>
/// Contains a probability-weighted position on ordered levels, their probabilities, and confidence.
/// </summary>
/// <param name="Score">The numeric score returned by Jev.</param>
/// <param name="LegendProbabilities">Level descriptions paired with their probabilities.</param>
/// <param name="Confidence">Distribution concentration, not a measure of answer accuracy.</param>
public record ScoreAnswer(double Score, ImmutableArray<LegendProbability> LegendProbabilities, Confidence Confidence)
{
    /// <summary>
    /// Parses the <c>score</c>, <c>legend</c>, <c>probabilities</c>, and <c>confidence</c> fields.
    /// </summary>
    /// <remarks>
    /// Legend entries and probabilities are paired by enumeration order, not looked up by key.
    /// Pairing stops at the shorter object if their lengths differ.
    /// </remarks>
    /// <exception cref="InvalidOperationException">A paired legend and probability entry have different keys.</exception>
    public static ScoreAnswer From(JsonObject obj)
    {
        var score = obj["score"]!.GetValue<double>();

        var legendProbabilities = obj["legend"]!.AsObject().Zip(obj["probabilities"]!.AsObject())
            .Select(x =>
            {
                if (x.First.Key != x.Second.Key)
                {
                    throw new InvalidOperationException("The keys should match");
                }

                return new LegendProbability(x.First.Key, x.First.Value!.GetValue<string>(),
                    x.Second.Value!.GetValue<double>());
            }).ToImmutableArray();

        var confidence = obj["confidence"]!.GetValue<double>();
        
        return new ScoreAnswer(score, legendProbabilities, confidence);
    }
}
