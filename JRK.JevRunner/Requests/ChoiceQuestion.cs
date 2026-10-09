using System.Text.Json.Nodes;

namespace JRK.JevRunner.Requests;



/// <summary>
/// Defines a named question whose answer is selected from the supplied choices.
/// </summary>
/// <param name="Name">The key identifying the question in the request.</param>
/// <param name="Instructions">The instructions for answering the question.</param>
/// <param name="Criterias">The available choices and their selection instructions.</param>
public record ChoiceQuestion(string Name, JevMessage Instructions, OneOrMore<ChoiceCriteria> Criterias) : IQuestion
{
    /// <summary>
    /// Gets the JSON representation of the choice question, including its instructions and criteria.
    /// </summary>
    public JsonObject Node
    {
        get
        {
            var obj = new JsonObject
            {
                {"type", "choice"},
                {"instructions", Instructions.Node}
            };

            var criteria = new JsonObject();
            
            foreach (var choiceCriteria in Criterias.ToEnumerable())
            {
                criteria.Add(choiceCriteria.Choice, choiceCriteria.Instruction);
            }

            obj.Add("criteria", criteria);
            return obj;
        }
    }
}
