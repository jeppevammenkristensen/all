using JRK.JevRunner.Requests;

namespace JRK.JevRunner.Annotation;

/// <summary>Defines instructions and ordered string criteria for a generated score question.</summary>
/// <typeparam name="TInstructionType">An instruction representation compatible with <see cref="JevMessage"/>.</typeparam>
/// <remarks>Query properties implementing this interface are discovered semantically. Criteria are added in array order before the question is added.</remarks>
public interface IScoreQuestionDefinition<out TInstructionType>
{
    /// <summary>Gets the instructions describing what should be scored using the query state.</summary>
    TInstructionType Instructions { get; }

    /// <summary>Gets the ordered criteria used to evaluate the score question.</summary>
    string[] Criterias { get; }
}
