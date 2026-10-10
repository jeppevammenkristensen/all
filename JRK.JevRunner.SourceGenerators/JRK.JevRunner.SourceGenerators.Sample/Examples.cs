using JRK.JevRunner.Annotation;
using JRK.JevRunner.Requests;
using JRK.JevRunner.Responses;

namespace JRK.JevRunner.SourceGenerators.Sample;

/// <summary>
/// Demonstrates request construction and typed answer access through a generated query API.
/// </summary>
public static class Examples
{
    /// <summary>Creates a request with a named noul question.</summary>
    public static Request CreateRequest() => new BudgetQuery().GetRequest();

    /// <summary>Retrieves the typed answer for the Budget question from an existing response.</summary>
    public static NoulAnswer ReadBudgetAnswer(Response response) => new BudgetQuery().GetBudgetAnswer(response);
}

/// <summary>Asks whether the supplied travel budget is sufficient.</summary>
[JevQuery]
public partial class BudgetQuery
{
    public JevMessage State => "The traveler has a budget of 500 EUR for a weekend trip.";
    public JevModel JevModel => global::JRK.JevRunner.Requests.JevModel.Latest;
    public BudgetQuestion Budget { get; } = new();
}

/// <summary>Provides the instructions for a noul question.</summary>
[NoulQuestion]
public class BudgetQuestion
{
    public JevMessage Instructions => "Is the budget sufficient for travel, accommodation, and meals?";
}