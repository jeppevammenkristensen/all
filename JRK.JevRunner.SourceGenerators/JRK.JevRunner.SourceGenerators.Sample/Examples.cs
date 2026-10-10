using JRK.JevRunner.Annotation;
using JRK.JevRunner.Requests;
using JRK.JevRunner.Responses;

namespace JRK.JevRunner.SourceGenerators.Sample;

/// <summary>
/// Demonstrates request construction and typed answer access through a generated query API.
/// </summary>
public static class Examples
{
    /// <summary>Creates a request with named noul, choice, and score questions.</summary>
    public static Request CreateRequest() => new BudgetQuery().GetRequest();

    /// <summary>Retrieves the typed answer for the Budget question from an existing response.</summary>
    public static NoulAnswer ReadBudgetAnswer(Response response) => new BudgetQuery().GetBudgetAnswer(response);

    /// <summary>Retrieves the complete choice answer, including its probabilities and confidence.</summary>
    public static ChoiceAnswer ReadDestinationAnswer(Response response) =>
        new BudgetQuery().GetDestinationAnswer(response);

    /// <summary>Retrieves the complete score answer, including its legend probabilities and confidence.</summary>
    public static ScoreAnswer ReadComfortAnswer(Response response) => new BudgetQuery().GetComfortAnswer(response);
}

/// <summary>Asks about budget, destination, and comfort for the supplied travel state.</summary>
[JevQuery]
public partial class BudgetQuery
{
    public JevMessage State => "The traveler has a budget of 500 EUR for a weekend trip.";
    public JevModel JevModel => global::JRK.JevRunner.Requests.JevModel.Latest;
    public BudgetQuestion Budget { get; } = new();
    public DestinationQuestion Destination { get; } = new();
    public ComfortQuestion Comfort { get; } = new();
}

/// <summary>Provides instructions and yes/no criteria for a noul question.</summary>
public class BudgetQuestion : INoulQuestionDefinition<JevMessage>
{
    public JevMessage Instructions => "Is the budget sufficient for travel, accommodation, and meals?";
    public string? Yes => "The total cost of travel, accommodation, and meals is within 500 EUR.";
    public string? No => "The total cost exceeds 500 EUR.";
}

/// <summary>Provides instructions and ordered selection criteria for a choice question.</summary>
public class DestinationQuestion : IChoiceQuestionDefinition<JevMessage>
{
    public JevMessage Instructions => "Choose the destination best suited to the traveler.";

    public ChoiceCriteria[] Choices =>
    [
        new("city", "Prefer museums, restaurants, and cultural attractions."),
        new("coast", "Prefer beaches and outdoor relaxation.")
    ];
}

/// <summary>Provides instructions and ordered scoring levels for a score question.</summary>
public class ComfortQuestion : IScoreQuestionDefinition<JevMessage>
{
    public JevMessage Instructions => "Rate the comfort the traveler can afford.";

    public string[] Criterias =>
        ["Basic accommodation", "Comfortable accommodation", "Luxury accommodation"];
}
