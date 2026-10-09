namespace JRK.JevRunner.Requests;

/// <summary>
/// Defines a selectable choice and the instruction used to evaluate it.
/// </summary>
/// <param name="Choice">The choice name used as a key in the question's criteria.</param>
/// <param name="Instruction">The instruction describing when to select this choice.</param>
public record ChoiceCriteria(string Choice, string Instruction);
