using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JRK.JevRunner.Infrastructure;
using JRK.JevRunner.Requests;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
// ReSharper disable AccessToDisposedClosure

namespace JRK.JevRunner.Tests;

/// <summary>
/// Verifies request submission, representative returned answers, and HTTP failure handling without network access.
/// </summary>
[TestSubject(typeof(JevRunner))]
public class JevRunnerTest
{
    /// <summary>
    /// The injected client receives an authenticated JSON request and its successful response supplies named answers.
    /// </summary>
    [Fact]
    public async Task Execute_Success_SubmitsRequestAndReturnsNamedAnswers()
    {
        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponse.Content = new StringContent("""
                                                 {
                                                   "answers": {
                                                     "approved": { "type": "noul", "noul": 0.75 },
                                                     "priority": {
                                                       "type": "choice",
                                                       "choice": "urgent",
                                                       "probabilities": { "urgent": 0.8, "normal": 0.2 },
                                                       "confidence": 0.9
                                                     }
                                                   }
                                                 }
                                                 """, Encoding.UTF8, "application/json");
        var callCount = 0;
        using var client = new HttpClient(new FakeHttpMessageHandler(async (message, cancellationToken) =>
        {
            callCount++;
            Assert.Equal(HttpMethod.Post, message.Method);
            Assert.Equal(new Uri("https://api.typesafe.ai/v1/systemone"), message.RequestUri);
            Assert.Equal("Bearer", message.Headers.Authorization!.Scheme);
            Assert.Equal("test-api-key", message.Headers.Authorization.Parameter);
            Assert.NotNull(message.Content);
            Assert.Equal("application/json", message.Content.Headers.ContentType!.MediaType);
            var body = JsonNode.Parse(await message.Content.ReadAsStringAsync(cancellationToken))!;
            Assert.Equal("request state", body["state"]!.GetValue<string>());
            Assert.Equal("jev-latest", body["model"]!.GetValue<string>());
            Assert.Equal("noul", body["questions"]!["approved"]!["type"]!.GetValue<string>());
            Assert.Equal("choice", body["questions"]!["priority"]!["type"]!.GetValue<string>());
            return httpResponse;
        }));
        var runner = CreateRunner(client);
        var request = new Request("request state", JevModel.Latest)
        {
            Questions =
            [
                new NoulQuestion("approved", "Is this approved?", null),
                new ChoiceQuestion("priority", "Choose a priority",
                    OneOrMore<ChoiceCriteria>.FromEnumerable(
                        [new ChoiceCriteria("urgent", "Urgent"), new ChoiceCriteria("normal", "Normal")]))
            ]
        };

        var response = await runner.Execute(request, TestContext.Current.CancellationToken);

        Assert.Equal(1, callCount);
        Assert.Equal(2, response.Answers.Count);
        Assert.Equal(0.75, response.GetRequiredNoulAnswer("approved").Noul.Value);
        var priority = response.GetRequiredChoiceAnswer("priority");
        Assert.Equal("urgent", priority.Choice);
        Assert.Equal(0.9, priority.Confidence.Value);
        Assert.Equal(2, priority.Probabilities.Length);
    }

    /// <summary>
    /// Known HTTP error responses receive actionable messages and retain the HTTP failure as their cause.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized,
        "The request was forbidden. Ensure that you called with a valid api token")]
    [InlineData(HttpStatusCode.UnprocessableEntity,
        "The request body failed validation — for example a missing required field or a malformed question. The body details the offending field.")]
    [InlineData(HttpStatusCode.TooManyRequests, "You may have exceeded your rate limit")]
    [InlineData((HttpStatusCode) 509, "TypeSafe is temporarily overloaded. Retry after a short delay.")]
    public async Task Execute_KnownErrorStatus_ThrowsKnownException(HttpStatusCode statusCode, string expectedMessage)
    {
        using var httpResponse = new HttpResponseMessage(statusCode);
        using var client = new HttpClient(new FakeHttpMessageHandler((_, _) => Task.FromResult(httpResponse)));
        var runner = CreateRunner(client);

        var exception = await Assert.ThrowsAsync<JevKnownException>(() => runner.Execute(CreateRequest(), TestContext.Current.CancellationToken));

        Assert.Equal(expectedMessage, exception.Message);
        var httpException = Assert.IsType<HttpRequestException>(exception.InnerException);
        Assert.Equal(statusCode, httpException.StatusCode);
    }

    /// <summary>
    /// Unmapped HTTP error responses remain HTTP exceptions with their original status code.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData((HttpStatusCode) 599)]
    public async Task Execute_UnknownErrorStatus_ThrowsHttpRequestException(HttpStatusCode statusCode)
    {
        using var httpResponse = new HttpResponseMessage(statusCode);
        using var client = new HttpClient(new FakeHttpMessageHandler((_, _) => Task.FromResult(httpResponse)));
        var runner = CreateRunner(client);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => runner.Execute(CreateRequest(), TestContext.Current.CancellationToken));

        Assert.Equal(statusCode, exception.StatusCode);
    }

    /// <summary>
    /// A transport failure before receiving a response passes through as the original exception.
    /// </summary>
    [Fact]
    public async Task Execute_TransportFailure_PreservesOriginalException()
    {
        var original = new HttpRequestException("Connection failed");
        using var client = new HttpClient(new FakeHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(original)));
        var runner = CreateRunner(client);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => runner.Execute(CreateRequest(),TestContext.Current.CancellationToken));

        Assert.Same(original, exception);
        Assert.Null(exception.StatusCode);
    }

    private static JevRunner CreateRunner(HttpClient client) =>
        new(new JevOptions("test-api-key"), client, NullLogger<JevRunner>.Instance);

    private static Request CreateRequest() => new("request state", JevModel.Latest);

    /// <summary>
    /// Supplies a per-test HTTP response or failure through the injected client's normal send pipeline.
    /// </summary>
    private sealed class FakeHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        /// <summary>
        /// Delegates the outgoing request to the test's response callback.
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => sendAsync(request, cancellationToken);
    }
}