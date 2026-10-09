using System;
using System.IO;
using System.Net;
using System.Net.Http;
using JetBrains.Annotations;
using JRK.JevRunner.Infrastructure;
using Xunit;

namespace JRK.JevRunner.Tests.Infrastructure;

[TestSubject(typeof(ExceptionTranslator))]
public class ExceptionTranslatorTest
{
    /// <summary>
    /// Known API failures receive an actionable message and retain the original exception as their cause.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized,
        "The request was forbidden. Ensure that you called with a valid api token")]
    [InlineData(HttpStatusCode.UnprocessableEntity,
        "The request body failed validation — for example a missing required field or a malformed question. The body details the offending field.")]
    [InlineData(HttpStatusCode.TooManyRequests, "You may have exceeded your rate limit")]
    [InlineData((HttpStatusCode) 509, "TypeSafe is temporarily overloaded. Retry after a short delay.")]
    public void TranslateRequestException_KnownStatusCode_WrapsOriginalException(
        HttpStatusCode statusCode, string expectedMessage)
    {
        var original = new HttpRequestException("Original HTTP failure", new InvalidOperationException("Root cause"),
            statusCode);

        var translated = ExceptionTranslator.TranslateRequestException(original);

        var knownException = Assert.IsType<JevKnownException>(translated);
        Assert.Equal(expectedMessage, knownException.Message);
        Assert.Same(original, knownException.InnerException);
    }

    /// <summary>
    /// Unmapped status codes pass through unchanged, including forbidden responses and other server failures.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData((HttpStatusCode) 599)]
    public void TranslateRequestException_UnknownStatusCode_ReturnsOriginalException(HttpStatusCode statusCode)
    {
        var original = new HttpRequestException("Original HTTP failure", null, statusCode);

        var translated = ExceptionTranslator.TranslateRequestException(original);

        Assert.Same(original, translated);
    }

    /// <summary>
    /// Non-HTTP failures pass through unchanged.
    /// </summary>
    [Fact]
    public void TranslateRequestException_NonHttpException_ReturnsOriginalException()
    {
        var original = new InvalidOperationException("Operation failed");

        var translated = ExceptionTranslator.TranslateRequestException(original);

        Assert.Same(original, translated);
    }

    /// <summary>
    /// Cancellation is preserved rather than converted into a known API failure.
    /// </summary>
    [Fact]
    public void TranslateRequestException_Cancellation_ReturnsOriginalException()
    {
        var original = new OperationCanceledException("Request cancelled");

        var translated = ExceptionTranslator.TranslateRequestException(original);

        Assert.Same(original, translated);
    }

    /// <summary>
    /// Only the supplied exception is matched; known HTTP failures nested inside other exceptions are not translated.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData((HttpStatusCode) 509)]
    public void TranslateRequestException_WrappedHttpException_ReturnsOriginalException(HttpStatusCode statusCode)
    {
        var httpException = new HttpRequestException("Original HTTP failure", null, statusCode);
        var original = new Exception("Outer failure", httpException);

        var translated = ExceptionTranslator.TranslateRequestException(original);

        Assert.Same(original, translated);
        Assert.Same(httpException, translated.InnerException);
    }

    /// <summary>
    /// Transport failures without an HTTP status code pass through unchanged.
    /// </summary>
    [Fact]
    public void TranslateRequestException_MissingStatusCode_ReturnsOriginalException()
    {
        var original = new HttpRequestException("Connection failed", new IOException("Connection reset"));

        var translated = ExceptionTranslator.TranslateRequestException(original);

        Assert.Same(original, translated);
    }
}
