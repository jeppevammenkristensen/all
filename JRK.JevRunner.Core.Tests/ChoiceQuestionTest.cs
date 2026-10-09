using System.Collections.Generic;
using System.Text.Json.Nodes;
using JetBrains.Annotations;
using JRK.JevRunner.Requests;
using Xunit;

namespace JRK.JevRunner.Tests;

/// <summary>
/// Verifies choice-question serialization and insertion into named question objects.
/// </summary>
[TestSubject(typeof(ChoiceQuestion))]
public class ChoiceQuestionTest
{
    /// <summary>
    /// A single criterion produces the complete choice-question payload.
    /// </summary>
    [Fact]
    public void Node_SingleCriterion_SerializesChoicePayload()
    {
        var question = new ChoiceQuestion("priority", "Choose a priority",
            new ChoiceCriteria("urgent", "Requires immediate attention"));

        var json = question.Node;

        var expected = JsonNode.Parse("""
                                      {
                                        "type": "choice",
                                        "instructions": "Choose a priority",
                                        "criteria": { "urgent": "Requires immediate attention" }
                                      }
                                      """);
        Assert.True(JsonNode.DeepEquals(expected, json));
    }

    /// <summary>
    /// Enumerable criteria map each choice to its corresponding instruction.
    /// </summary>
    [Fact]
    public void Node_MultipleCriteria_SerializesEveryChoice()
    {
        IEnumerable<ChoiceCriteria> criteria = new List<ChoiceCriteria>
        {
            new("urgent", "Requires immediate attention"),
            new("normal", "Can wait"),
            new("ignore", "No action needed")
        };
        var question = new ChoiceQuestion("priority", "Choose a priority", criteria);

        var json = question.Node;

        var actual = Assert.IsType<JsonObject>(json["criteria"]);
        var expected = JsonNode.Parse("""
                                      {
                                        "urgent": "Requires immediate attention",
                                        "normal": "Can wait",
                                        "ignore": "No action needed"
                                      }
                                      """);
        Assert.True(JsonNode.DeepEquals(expected, actual));
    }

    /// <summary>
    /// Choice names and instructions retain empty, escaped, and Unicode text through JSON round trips.
    /// </summary>
    [Theory]
    [InlineData("urgent", "Choose a priority", "Requires immediate attention")]
    [InlineData("", "", "")]
    [InlineData("Quote: \"; slash: \\; æøå", "Line one\nLine two", "Tab:\t\"hello\" \\ æøå")]
    public void Node_StringValues_PreservesValues(string choice, string instructions, string criterionInstruction)
    {
        var question = new ChoiceQuestion("priority", instructions,
            new ChoiceCriteria(choice, criterionInstruction));

        var json = JsonNode.Parse(question.Node.ToJsonString());

        Assert.NotNull(json);
        Assert.Equal(instructions, json["instructions"]!.GetValue<string>());
        var criteria = Assert.IsType<JsonObject>(json["criteria"]);
        Assert.Single(criteria);
        Assert.Equal(criterionInstruction, criteria[choice]!.GetValue<string>());
    }

    /// <summary>
    /// Multiple instruction messages remain a JSON array in their original order.
    /// </summary>
    [Fact]
    public void Node_ArrayInstructions_SerializesItemsInOrder()
    {
        string[] instructions = ["Read the request", "Choose a priority"];
        var question = new ChoiceQuestion("priority", instructions,
            new ChoiceCriteria("urgent", "Requires immediate attention"));

        var json = question.Node;

        var actual = Assert.IsType<JsonArray>(json["instructions"]);
        var expected = JsonNode.Parse("""["Read the request","Choose a priority"]""");
        Assert.True(JsonNode.DeepEquals(expected, actual));
    }

    /// <summary>
    /// An empty instruction array remains an empty JSON array rather than null.
    /// </summary>
    [Fact]
    public void Node_EmptyArrayInstructions_ReturnsEmptyArray()
    {
        string[] instructions = [];
        var question = new ChoiceQuestion("priority", instructions,
            new ChoiceCriteria("urgent", "Requires immediate attention"));

        var json = question.Node;

        Assert.Empty(Assert.IsType<JsonArray>(json["instructions"]));
    }

    /// <summary>
    /// Structured instructions are embedded as JSON objects rather than encoded strings.
    /// </summary>
    [Fact]
    public void Node_ObjectInstructions_SerializesProperties()
    {
        var instructions = new Dictionary<string, string>
        {
            ["task"] = "Choose a priority",
            ["context"] = "Support request"
        };
        var question = new ChoiceQuestion("priority", instructions,
            new ChoiceCriteria("urgent", "Requires immediate attention"));

        var json = question.Node;

        var actual = Assert.IsType<JsonObject>(json["instructions"]);
        var expected = JsonNode.Parse("""{"task":"Choose a priority","context":"Support request"}""");
        Assert.True(JsonNode.DeepEquals(expected, actual));
    }

    /// <summary>
    /// Each serialization creates an independent JSON tree that can be attached to a different parent.
    /// </summary>
    [Fact]
    public void Node_MultipleReads_ReturnsIndependentTrees()
    {
        string[] instructions = ["Choose a priority"];
        var question = new ChoiceQuestion("priority", instructions,
            new ChoiceCriteria("urgent", "Requires immediate attention"));

        var first = question.Node;
        var second = question.Node;
        var firstParent = new JsonObject {["priority"] = first};
        var secondParent = new JsonObject {["priority"] = second};
        first["instructions"]!.AsArray()[0] = "Changed instructions";
        first["criteria"]!["urgent"] = "Changed criterion";

        Assert.NotSame(first, second);
        Assert.Same(firstParent, first.Parent);
        Assert.Same(secondParent, second.Parent);
        Assert.Equal("Choose a priority", second["instructions"]![0]!.GetValue<string>());
        Assert.Equal("Requires immediate attention", second["criteria"]!["urgent"]!.GetValue<string>());
    }

    /// <summary>
    /// The question union inserts a choice payload under its name without removing existing questions.
    /// </summary>
    [Fact]
    public void AddTo_ChoiceQuestion_AddsNamedPayloadAndPreservesExistingEntries()
    {
        var choice = new ChoiceQuestion("priority", "Choose a priority",
            new ChoiceCriteria("urgent", "Requires immediate attention"));
        Question question = choice;
        var existing = new JsonObject {["type"] = "noul"};
        var questions = new JsonObject {["existing"] = existing};

        question.AddTo(questions);

        Assert.Equal(2, questions.Count);
        Assert.Same(existing, questions["existing"]);
        var payload = Assert.IsType<JsonObject>(questions["priority"]);
        Assert.True(JsonNode.DeepEquals(choice.Node, payload));
    }
}
