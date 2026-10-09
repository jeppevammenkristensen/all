using System.Text.Json.Nodes;

namespace JRK.JevRunner.Responses;

/// <summary>
/// Represents a noul, score, or choice answer returned by Jev.
/// </summary>
public union Answer(NoulAnswer, ScoreAnswer, ChoiceAnswer)
{
    /// <summary>
    /// Enumerates named answers, selecting a parser from each answer's <c>type</c> field.
    /// </summary>
    /// <param name="obj">The response's <c>answers</c> object, keyed by question name.</param>
    /// <remarks>
    /// Parses <c>choice</c>, <c>score</c>, and <c>noul</c> entries in enumeration order;
    /// unsupported type strings are skipped. Parsing occurs when the result is enumerated.
    /// </remarks>
    internal static IEnumerable<AnswerWrapper> From(JsonObject obj)
    {
        foreach (var keyValuePair in obj.AsObject())
        {
            var key = keyValuePair.Key;
            var value = keyValuePair.Value!;
            var type = value["type"]!.GetValue<string>();

            if (type == "choice")
            {
                yield return new AnswerWrapper(key,ChoiceAnswer.From(value.AsObject()));
            }
            else if (type == "score")
            {
                yield return new AnswerWrapper(key,ScoreAnswer.From(value.AsObject()));
            }
            else if (type == "noul")
            {
                yield return new AnswerWrapper(key, NoulAnswer.From(value.AsObject()));
            }
        }
    }
}
