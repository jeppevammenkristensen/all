namespace JRK.JevRunner.Responses;

/// <summary>
/// Associates an answer with the name of its question in the response.
/// </summary>
/// <param name="Name">The question name used as the answer's key in the response.</param>
/// <param name="Answer">The typed answer for that question.</param>
public record AnswerWrapper(string Name, Answer Answer)
{
}
