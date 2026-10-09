namespace JRK.JevRunner.Responses;

/// <summary>
/// Associates a score level's key and description with its probability.
/// </summary>
/// <param name="Key">The level's key in the legend and probabilities objects.</param>
/// <param name="Description">The legend's description of the level.</param>
/// <param name="Noul">The probability assigned to this level.</param>
public record LegendProbability(string Key, string Description, Noul Noul);
