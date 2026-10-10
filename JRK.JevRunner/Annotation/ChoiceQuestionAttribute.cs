namespace JRK.JevRunner.Annotation;

/// <summary>
/// Marks a question type whose instructions and ordered choices configure a choice question.
/// </summary>
/// <remarks>
/// Supply readable instance properties named <c>Instructions</c> and <c>Choices</c>.
/// Choices must be enumerable as <see cref="Requests.ChoiceCriteria"/> values.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public class ChoiceQuestionAttribute : Attribute, IQuestionAttribute
{
}