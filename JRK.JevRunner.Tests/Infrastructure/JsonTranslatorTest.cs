using System.Text.Json.Nodes;
using JetBrains.Annotations;
using JRK.JevRunner;
using JRK.JevRunner.Infrastructure;
using JRK.JevRunner.Requests;
using Xunit;

namespace JRK.JevRunner.Tests.Infrastructure;

[TestSubject(typeof(JsonTranslator))]
public class JsonTranslatorTest
{
    /// <summary>
    /// A request without questions still contains the state, model, and empty question object.
    /// </summary>
    [Fact]
    public void CreateRequestJson_EmptyRequest_SerializesRequestEnvelope()
    {
        var request = new Request("request state", new JevModel("jev-test"));

        var json = JsonTranslator.CreateRequestJson(request);

        var expected = JsonNode.Parse("""
                                      {
                                        "state": "request state",
                                        "model": "jev-test",
                                        "questions": {}
                                      }
                                      """);
        Assert.True(JsonNode.DeepEquals(expected, json));
    }

    /// <summary>
    /// A missing model is represented by the request's latest-model default.
    /// </summary>
    [Fact]
    public void CreateRequestJson_MissingModel_UsesLatestModel()
    {
        var request = new Request("request state", null);

        var json = JsonTranslator.CreateRequestJson(request);

        Assert.Equal("jev-latest", json["model"]!.GetValue<string>());
    }

    /// <summary>
    /// State arrays remain arrays and preserve their order in the request envelope.
    /// </summary>
    [Fact]
    public void CreateRequestJson_ArrayState_PreservesStateItems()
    {
        var request = new Request(new object[] {"first", 2, true}, JevModel.Latest);

        var json = JsonTranslator.CreateRequestJson(request);

        var state = Assert.IsType<JsonArray>(json["state"]);
        Assert.Equal("first", state[0]!.GetValue<string>());
        Assert.Equal(2, state[1]!.GetValue<int>());
        Assert.True(state[2]!.GetValue<bool>());
    }

    /// <summary>
    /// Every supported question type is added beneath its name in the questions object.
    /// </summary>
    [Fact]
    public void CreateRequestJson_Questions_SerializesNamedQuestionPayloads()
    {
        var request = new Request("state", JevModel.Version1_13_0)
        {
            Questions =
            [
                new NoulQuestion("approved", "Is this approved?", null),
                new ChoiceQuestion("priority", "Choose a priority",
                    new ChoiceCriteria("urgent", "Requires immediate attention")),
                new ScoreQuestion("quality", "Rate the quality",
                    [new JevMessage("Accuracy"), new JevMessage("Clarity")])
            ]
        };

        var json = JsonTranslator.CreateRequestJson(request);
        var questions = Assert.IsType<JsonObject>(json["questions"]);

        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{"type":"noul","instructions":"Is this approved?"}"""),
            questions["approved"]));
        Assert.Equal("choice", questions["priority"]!["type"]!.GetValue<string>());
        Assert.Equal("score", questions["quality"]!["type"]!.GetValue<string>());
        Assert.Equal("jev-1.13.0", json["model"]!.GetValue<string>());
    }

    /// <summary>
    /// Question names are JSON property names and retain escaped and Unicode content.
    /// </summary>
    [Fact]
    public void CreateRequestJson_EscapedQuestionName_PreservesPropertyName()
    {
        const string name = "priority \"æøå\"";
        var request = new Request("state", JevModel.Latest)
        {
            Questions = [new ChoiceQuestion(name, "Choose", new ChoiceCriteria("yes", "Yes"))]
        };

        var json = JsonTranslator.CreateRequestJson(request);

        Assert.NotNull(json["questions"]![name]);
    }
}