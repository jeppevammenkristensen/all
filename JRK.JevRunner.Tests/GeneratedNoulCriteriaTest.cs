#nullable enable

using System.Text.Json.Nodes;
using JRK.JevRunner.Annotation;
using JRK.JevRunner.Requests;
using Xunit;

namespace JRK.JevRunner.Tests;

/// <summary>Verifies generated noul criteria against the actual runtime builder and JSON serialization.</summary>
public partial class GeneratedNoulCriteriaTest
{
    /// <summary>Generates criteria only when both descriptions are non-null.</summary>
    [Theory]
    [InlineData("Within budget", "Exceeds budget")]
    [InlineData(null, null)]
    [InlineData("Within budget", null)]
    [InlineData(null, "Exceeds budget")]
    [InlineData("", null)]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void GetRequest_GeneratesCriteriaOnlyWhenBothDescriptionsAreNonNull(string? yes, string? no)
    {
        var request = new CriteriaQuery(yes, no).GetRequest();
        var questions = new JsonObject();
        Assert.Single(request.Questions).AddTo(questions);
        var question = Assert.IsType<JsonObject>(questions["Budget"]);

        Assert.Equal("noul", question["type"]!.GetValue<string>());
        Assert.Equal("Is the budget sufficient?", question["instructions"]!.GetValue<string>());
        if (yes == null || no == null)
        {
            Assert.False(question.ContainsKey("criteria"));
        }
        else
        {
            var criteria = Assert.IsType<JsonObject>(question["criteria"]);
            Assert.Equal(2, criteria.Count);
            Assert.True(criteria.ContainsKey("yes"));
            Assert.True(criteria.ContainsKey("no"));
            Assert.Equal(yes, criteria["yes"]?.GetValue<string>());
            Assert.Equal(no, criteria["no"]?.GetValue<string>());
        }
    }

    /// <summary>Supplies an interface-typed question with explicit implementations to the generator.</summary>
    [JevQuery]
    private partial record class CriteriaQuery(string? Yes, string? No)
    {
        public JevMessage State => "A weekend trip with a budget of 500 EUR.";
        public JevModel JevModel => global::JRK.JevRunner.Requests.JevModel.Latest;
        public INoulQuestionDefinition<string> Budget => new CriteriaDefinition(Yes, No);
    }

    /// <summary>Provides criteria through the definition contract rather than concrete public properties.</summary>
    private sealed record CriteriaDefinition(string? YesValue, string? NoValue) : INoulQuestionDefinition<string>
    {
        string INoulQuestionDefinition<string>.Instructions => "Is the budget sufficient?";
        string? INoulQuestionDefinition<string>.Yes => YesValue;
        string? INoulQuestionDefinition<string>.No => NoValue;
    }
}
