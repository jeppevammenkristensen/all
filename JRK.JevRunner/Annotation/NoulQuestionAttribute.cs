using JRK.JevRunner.Requests;

namespace JRK.JevRunner.Annotation;


/// <summary>
/// Decorate a class that serves as a query
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class JevQueryAttribute : Attribute
{
    
}


public interface IQuestionAttribute
{
    
}

[AttributeUsage(AttributeTargets.Class)]
public class NoulQuestionAttribute : Attribute, IQuestionAttribute
{
    
}

/// <summary>
/// Defines the instructions and optional yes/no descriptions for a noul question definition.
/// </summary>
/// <typeparam name="TInstructionType">The instruction representation, which must be compatible with <see cref="JevMessage"/> when used by the query generator.</typeparam>
/// <remarks>
/// Implement this contract on a type marked with <see cref="NoulQuestionAttribute"/> to make its
/// required properties explicit. The query generator discovers questions through the attribute,
/// rather than this interface, and currently uses only <see cref="Instructions"/>;
/// it does not apply the optional <see cref="Yes"/> and <see cref="No"/> descriptions.
/// </remarks>
public interface INoulQuestionDefinition<out TInstructionType>
{
    /// <summary>Gets the instructions describing what the question should evaluate against the query state.</summary>
    public TInstructionType Instructions { get; }
    /// <summary>Gets an optional description of the conditions supporting a yes answer.</summary>
    public string? Yes { get; }
    /// <summary>Gets an optional description of the conditions supporting a no answer.</summary>
    public string? No { get; }
}

/// <summary>
/// Defines the instructions and selectable choices for a choice question definition.
/// </summary>
/// <typeparam name="TInstructionType">The instruction representation, which must be compatible with <see cref="JevMessage"/> when used by the query generator.</typeparam>
/// <remarks>
/// Implement this contract on a type marked with <see cref="ChoiceQuestionAttribute"/> to describe
/// the generated question's inputs. The query generator discovers the attribute and adds each
/// choice to the builder in collection order before adding the question to the request.
/// Implementing this interface alone does not enable generation.
/// </remarks>
public interface IChoiceQuestionDefinition<out TInstructionType>
{
    /// <summary>Gets the instructions describing how to select a choice using the query state.</summary>
    public TInstructionType Instructions { get; }
    /// <summary>Gets the ordered choices, each containing a choice name and instructions describing when to select it.</summary>
    public ChoiceCriteria[] Choices {get;}
}

/// <summary>
/// Defines the instructions and ordered evaluation criteria for a score question definition.
/// </summary>
/// <typeparam name="TInstructionType">The instruction representation, which must be compatible with <see cref="JevMessage"/> when used by the query generator.</typeparam>
/// <typeparam name="TScoreCriteria">The criterion representation; the current query generator requires string criteria.</typeparam>
/// <remarks>
/// Implement this contract on a type marked with <see cref="ScoreQuestionAttribute"/> to describe
/// the generated question's inputs. The query generator discovers the attribute and adds each
/// criterion to the score builder in collection order. Use <see cref="string"/> for
/// <typeparamref name="TScoreCriteria"/> with the current builder API.
/// Implementing this interface alone does not enable generation.
/// </remarks>
public interface IScoreQuestionDefinition<out TInstructionType, out TScoreCriteria>
{
    /// <summary>Gets the instructions describing what should be scored using the query state.</summary>
    public TInstructionType Instructions { get; }
    /// <summary>Gets the ordered criteria used to evaluate the score question.</summary>
    public TScoreCriteria[] Criterias { get; }
}
