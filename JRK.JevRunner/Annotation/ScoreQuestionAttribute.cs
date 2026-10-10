namespace JRK.JevRunner.Annotation;

/// <summary>
/// Marks a question type whose instructions and ordered criteria configure a score question.
/// </summary>
/// <remarks>
/// Supply readable instance properties named <c>Instructions</c> and <c>Criterias</c>.
/// Criterias must be enumerable as strings, in scoring level order.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public class ScoreQuestionAttribute : Attribute, IQuestionAttribute
{
}