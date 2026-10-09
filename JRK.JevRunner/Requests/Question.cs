using System.Text.Json.Nodes;

namespace JRK.JevRunner.Requests;

/// <summary>
/// Represents a noul, choice, or score question that can be added to a request.
/// </summary>
public union Question(NoulQuestion, ChoiceQuestion, ScoreQuestion)
{
    /// <summary>
    /// Adds the question's JSON representation to the supplied object under its name.
    /// </summary>
    /// <param name="obj">The object containing the request's named questions; an existing entry with the same name is replaced.</param>
    public void AddTo(JsonObject obj)
    {
        if (this is NoulQuestion noul)
        {
            obj[noul.Name] = noul.Node;
        }
        else if (this is ChoiceQuestion choice)
        {
            obj[choice.Name] = choice.Node;
        }
        else if (this is ScoreQuestion score)
        {
            obj[score.Name] = score.Node;
        }
    }
}
