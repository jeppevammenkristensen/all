using System;
using JetBrains.Annotations;
using JRK.JevRunner;
using JRK.JevRunner.Responses;
using Xunit;

namespace JevRunner.Core.Tests;

/// <summary>
/// Verifies required answer lookups and their failure contracts for every answer type.
/// </summary>
[TestSubject(typeof(Response))]
public class ResponseTest
{
    /// <summary>
    /// Each typed lookup returns the original answer with the requested name.
    /// </summary>
    [Fact]
    public void GetRequiredAnswers_MatchingTypes_ReturnOriginalAnswers()
    {
        var noul = new NoulAnswer(0.75);
        var choice = new ChoiceAnswer("yes", [], 0.9);
        var score = new ScoreAnswer(0.5, [], 0.8);
        var response = new Response([new("noul", noul), new("choice", choice), new("score", score)]);

        Assert.Same(noul, response.GetRequiredNoulAnswer("noul"));
        Assert.Same(choice, response.GetRequiredChoiceAnswer("choice"));
        Assert.Same(score, response.GetRequiredScoreAnswer("score"));
    }

    /// <summary>
    /// Every typed lookup reports a missing name consistently.
    /// </summary>
    [Theory]
    [InlineData("NoulAnswer")]
    [InlineData("ChoiceAnswer")]
    [InlineData("ScoreAnswer")]
    public void GetRequiredAnswer_MissingName_Throws(string type)
    {
        var response = new Response([new("other", new NoulAnswer(0.5))]);

        var exception = Assert.Throws<InvalidOperationException>(() => GetRequiredAnswer(response, type, "missing"));

        Assert.Equal("Did not find match for missing", exception.Message);
    }

    /// <summary>
    /// A different union case reports the expected answer type and requested name.
    /// </summary>
    [Theory]
    [InlineData("NoulAnswer")]
    [InlineData("ChoiceAnswer")]
    [InlineData("ScoreAnswer")]
    public void GetRequiredAnswer_WrongType_Throws(string type)
    {
        Answer answer = type == "NoulAnswer" ? new ChoiceAnswer("yes", [], 0.9) : new NoulAnswer(0.5);
        var response = new Response([new("answer", answer)]);

        var exception = Assert.Throws<InvalidOperationException>(() => GetRequiredAnswer(response, type, "answer"));

        Assert.Equal($"Matched answer is not of type {type} for answer", exception.Message);
    }

    /// <summary>
    /// Duplicate names retain first-match behavior, even when a later answer has the requested type.
    /// </summary>
    [Fact]
    public void GetRequiredChoiceAnswer_DuplicateNames_ValidatesFirstMatch()
    {
        var response = new Response([new("answer", new NoulAnswer(0.5)), new("answer", new ChoiceAnswer("yes", [], 0.9))]);

        var exception = Assert.Throws<InvalidOperationException>(() => response.GetRequiredChoiceAnswer("answer"));

        Assert.Equal("Matched answer is not of type ChoiceAnswer for answer", exception.Message);
    }

    private static object GetRequiredAnswer(Response response, string type, string name) => type switch
    {
        "NoulAnswer" => response.GetRequiredNoulAnswer(name),
        "ChoiceAnswer" => response.GetRequiredChoiceAnswer(name),
        "ScoreAnswer" => response.GetRequiredScoreAnswer(name),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
