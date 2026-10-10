using JRK.JevRunner.Requests;

namespace JRK.JevRunner.Annotation;

/// <summary>Defines instructions and optional yes/no criteria for a generated noul question.</summary>
/// <typeparam name="TInstructionType">An instruction representation compatible with <see cref="JevMessage"/>.</typeparam>
/// <remarks>
/// Query properties implementing this interface are discovered semantically, including inherited and explicit implementations.
/// Criteria are generated only when both descriptions are non-null; otherwise criteria remain unset.
/// </remarks>
public interface INoulQuestionDefinition<out TInstructionType>
{
    /// <summary>Gets the instructions to evaluate against the query state.</summary>
    TInstructionType Instructions { get; }

    /// <summary>
    /// Gets an optional description of the conditions supporting a yes answer.
    /// Criteria are generated only when both this description and <see cref="No"/> are non-null.
    /// </summary>
    string? Yes { get; }

    /// <summary>
    /// Gets an optional description of the conditions supporting a no answer.
    /// Criteria are generated only when both this description and <see cref="Yes"/> are non-null.
    /// </summary>
    string? No { get; }
}
