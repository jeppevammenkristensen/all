using System.Text.Json;

namespace JRK.JevRunner;

/// <summary>
/// Interface for the IJevRunner
/// </summary>
public interface IJevRunner
{
    /// <summary>
    /// Serializes and submits the request, then parses the API's named answers into a response.
    /// </summary>
    /// <param name="request">The state, model, and questions to send to the API.</param>
    /// <param name="token"></param>
    /// <returns>A response containing the supported answers returned by the API.</returns>
    /// <exception cref="InvalidOperationException">The API returns an unsuccessful HTTP status code.</exception>
    /// <exception cref="JsonException">The response cannot be read as a JSON object or contains a null JSON response.</exception>
    Task<Response> Execute(Request request, CancellationToken token = default!);
}
