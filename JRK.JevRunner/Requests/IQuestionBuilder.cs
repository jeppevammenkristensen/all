namespace JRK.JevRunner.Requests;

/// <summary>
/// Builds a typed question and its representation for inclusion in a request.
/// </summary>
public interface IQuestionBuilder<TQuestion> where TQuestion : IQuestion
{
    /// <summary>
    /// Creates a typed question from the builder's current configuration.
    /// </summary>
    TQuestion Build();
    /// <summary>
    /// Creates a question from the builder's current configuration for inclusion in a request.
    /// </summary>
    Question BuildQuestion();
}
