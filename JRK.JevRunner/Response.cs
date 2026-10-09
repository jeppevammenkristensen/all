using System.Text.Json.Nodes;
using JRK.JevRunner.Responses;

namespace JRK.JevRunner;

public record Usage ( long InputTokens, long OutputTokens )
{
    public static Usage From(JsonObject asObject)
    {
        return new Usage(asObject["input_tokens"]!.GetValue<long>(), asObject["output_tokens"]!.GetValue<long>());
    }
}

/// <summary>
/// Contains named answers and provides required, typed answer lookups.
/// </summary>
/// <param name="Answers">The named answers searched by the typed lookup methods.</param>
public record Response(List<AnswerWrapper> Answers, Usage Usage)
{
    /// <summary>
    /// Returns the named noul answer, throwing if it is missing or has another type.
    /// </summary>
    public NoulAnswer GetRequiredNoulAnswer(string name) =>
        GetRequiredAnswer(name, answer => answer is NoulAnswer noul ? noul : null);

    /// <summary>
    /// Returns the named choice answer, throwing if it is missing or has another type.
    /// </summary>
    public ChoiceAnswer GetRequiredChoiceAnswer(string name) =>
        GetRequiredAnswer(name, answer => answer is ChoiceAnswer choice ? choice : null);

    /// <summary>
    /// Returns the named score answer, throwing if it is missing or has another type.
    /// </summary>
    public ScoreAnswer GetRequiredScoreAnswer(string name) =>
        GetRequiredAnswer(name, answer => answer is ScoreAnswer score ? score : null);

    /// <summary>
    /// Looks up the first named answer and validates its type using the supplied union-case selector.
    /// </summary>
    private TAnswer GetRequiredAnswer<TAnswer>(string name, Func<Answer, TAnswer?> selectAnswer)
        where TAnswer : class
    {
        var match = Answers.FirstOrDefault(x => x.Name == name);
        if (match == null)
            throw new InvalidOperationException($"Did not find match for {name}");

        return selectAnswer(match.Answer) ??
               throw new InvalidOperationException($"Matched answer is not of type {typeof(TAnswer).Name} for {name}");
    }
}
