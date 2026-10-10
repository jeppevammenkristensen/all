using JRK.JevRunner.Requests;

namespace JRK.JevRunner.Annotation;

/// <summary>Defines instructions and ordered choices for a generated choice question.</summary>
/// <typeparam name="TInstructionType">An instruction representation compatible with <see cref="JevMessage"/>.</typeparam>
/// <remarks>Query properties implementing this interface are discovered semantically. Choices are added in array order before the question is added.</remarks>
public interface IChoiceQuestionDefinition<out TInstructionType>
{
    /// <summary>Gets the instructions describing how to select a choice using the query state.</summary>
    TInstructionType Instructions { get; }

    /// <summary>Gets the ordered choice names and their selection instructions.</summary>
    ChoiceCriteria[] Choices { get; }
}