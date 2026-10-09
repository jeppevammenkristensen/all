namespace JRK.JevRunner.Responses;

/// <summary>
/// Associates a choice option with its probability.
/// </summary>
/// <param name="Choice">The option's key.</param>
/// <param name="Value">The probability assigned to the option.</param>
public record Probability(string Choice, Noul Value);
