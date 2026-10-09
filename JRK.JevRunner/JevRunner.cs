using System.Text.Json;
using System.Text.Json.Nodes;
using JRK.JevRunner.Infrastructure;
using JRK.JevRunner.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JRK.JevRunner;

public static class JevRunnerExtensions 
{
    private static readonly HttpClient SharedClient = new();
    
    extension(JevRunner)
    {
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
    }
}


/// <summary>
/// Submits requests to the TypeSafe System One API and parses their named answers.
/// </summary>
public partial class JevRunner : IJevRunner
{
    private readonly JevOptions _options;
    private readonly HttpClient _client;
    private readonly ILogger<JevRunner> _logger;

    /// <summary>
    /// Submits requests to the TypeSafe System One API and parses their named answers.
    /// </summary>
    public JevRunner(JevOptions options, HttpClient client, ILogger<JevRunner> logger)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException("API key cannot be null or empty");
        }
        
        _options = options;
        _client = client;
        _logger = logger;
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
        
        var json = JsonTranslator.CreateRequestJson(request);
        LogRequestJsonRequestjson(json);
        
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.typesafe.ai/v1/systemone");
        message.Content = JsonContent.Create(json);

        message.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", _options.ApiKey);
        
        var response = await _client.SendAsync(message, cancellationToken: cancellationToken);

        try
        {
            _ = response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Request failed");
            throw ExceptionTranslator.TranslateRequestException(e);
        }

        try
        {
            var responseJson = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken) ??
                               throw new JsonException("The API returned a null JSON response");

            var answersObject = responseJson["answers"]!.AsObject();
            return new Response([.. Answer.From(answersObject)]);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("An unexpected error occurred when deserializing the response content", e);
        }
    }

    [LoggerMessage(LogLevel.Debug, "Request json {RequestJson}")]
    partial void LogRequestJsonRequestjson(JsonObject requestJson);
}
