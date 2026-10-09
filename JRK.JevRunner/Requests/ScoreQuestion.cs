using System.Text.Json.Nodes;

namespace JRK.JevRunner.Requests;

/// <summary>
/// Defines a named score question with instructions and scoring criteria.
/// </summary>
/// <param name="Name">The key identifying the question in the request.</param>
/// <param name="Instructions">The instructions for answering the question.</param>
/// <param name="Criterias">The criteria used to determine the score.</param>
public record ScoreQuestion(string Name, JevMessage Instructions, JevMessage[] Criterias) : IQuestion
{
    /// <summary>
    /// Gets the JSON representation of the score question, including its instructions and criteria array.
    /// </summary>
    public JsonNode? Node
    {
        get
        {
            var jsonObject = new JsonObject();
            jsonObject["type"] = "score";
            jsonObject["instructions"] = Instructions.Node;
            jsonObject["criteria"] = new JsonArray(Criterias.Select(x => x.Node).ToArray());
            return jsonObject;
        }
    }
}
