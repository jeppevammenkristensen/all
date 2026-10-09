namespace JRK.JevRunner.Requests;

/// <summary>
/// Defines the criteria for yes and no answers to a question.
/// </summary>
/// <param name="TrueDefinition">The description of when the answer should be yes.</param>
/// <param name="FalseDefinition">The description of when the answer should be no.</param>
public record YesNo(string TrueDefinition, string FalseDefinition);
