using System.Text.Json;
using System.Text.Json.Nodes;
using JRK.JevRunner.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JRK.JevRunner;

/// <summary>
/// Submits requests to the TypeSafe System One API and parses their named answers.
/// </summary>
public partial class JevRunner : IJevRunner
{
    private static readonly HttpClient SharedClient = new();
    
    private readonly JevOptions _authorization;
    private readonly HttpClient _client;
    private readonly ILogger<JevRunner> _logger;

    /// <summary>
    /// Submits requests to the TypeSafe System One API and parses their named answers.
    /// </summary>
    public JevRunner(JevOptions authorization, HttpClient client, ILogger<JevRunner> logger)
    {
        _authorization = authorization;
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Convenience method to create a runner
    /// </summary>
    /// <param name="apiKey">The apiKey</param>
    /// <param name="logger"></param>
    /// <returns>A new <see cref="JevRunner"/> instance</returns>
    /// <remarks>This is quick usage. If you want to use dependency injection consider using the public constructor</remarks>
    public static JevRunner Init(string apiKey, ILogger<JevRunner>? logger = null)
    {
        return new JevRunner(new JevOptions(apiKey), SharedClient, logger ?? NullLogger<JevRunner>.Instance);
    }

    /// <summary>
    /// Serializes and submits the request, then parses the API's named answers into a response.
    /// </summary>
    /// <param name="request">The state, model, and questions to send to the API.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A response containing the supported answers returned by the API.</returns>
    /// <exception cref="InvalidOperationException">The API returns an unsuccessful HTTP status code.</exception>
    /// <exception cref="JsonException">The response cannot be read as a JSON object or contains a null JSON response.</exception>
    public async Task<Response> Execute(Request request, CancellationToken cancellationToken = default)
    {
        var json = CreateRequestJson(request);
        LogRequestJsonRequestjson(json);
        
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.typesafe.ai/v1/systemone");
        message.Content = JsonContent.Create(json);

        message.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", _authorization.ApiKey);
        
        var response = await _client.SendAsync(message, cancellationToken: cancellationToken);

        try
        {
            _ = response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, "Request failed");
            throw new InvalidOperationException(e.Message, e);
        }

        var responseJson = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken) ??
                           throw new JsonException("The API returned a null JSON response");

        var answersObject = responseJson["answers"]!.AsObject();
        return new Response([.. Answer.From(answersObject)]);
    }

    /// <summary>
    /// Creates the JSON request body containing the state, model identifier, and named questions.
    /// </summary>
    internal JsonObject CreateRequestJson(Request request)
    {
        var json = new JsonObject
        {
            {"state", request.State.Node},
            {"model", request.Model.Model}
        };

        var question = new JsonObject();

        foreach (var requestQuestion in request.Questions)
        {
            requestQuestion.AddTo(question);
        }

        json.Add("questions", question);

        return json;
    }

    [LoggerMessage(LogLevel.Debug, "Request json {RequestJson}")]
    partial void LogRequestJsonRequestjson(JsonObject requestJson);
}
