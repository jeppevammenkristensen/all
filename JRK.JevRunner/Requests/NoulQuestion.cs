using System.Text.Json.Nodes;

namespace JRK.JevRunner.Requests;

/// <summary>
/// Defines a named <c>noul</c> question with instructions and optional yes/no criteria.
/// </summary>
/// <param name="Name">The key identifying the question in the request.</param>
/// <param name="Instructions">The instructions for answering the question.</param>
/// <param name="Criteria">The optional definitions for yes and no answers.</param>
public record NoulQuestion(string Name, JevMessage Instructions, YesNo? Criteria) : IQuestion
{
    /// <summary>
    /// Gets the JSON representation of the question, including yes/no criteria when supplied.
    /// </summary>
    public JsonNode Node
    {
        get
        {
            var res = new JsonObject();
            res.Add("type", "noul");
            res.Add("instructions", Instructions.Node);

            if (Criteria != null)
            {
                var yesNo = new JsonObject();
                yesNo.Add("yes", Criteria.TrueDefinition);
                yesNo.Add("no", Criteria.FalseDefinition);

                res.Add("criteria", yesNo);
            }

            return res;
        }
    }
}

/// <summary>
/// Marker interface for a question
/// </summary>
public interface IQuestion
{
}
