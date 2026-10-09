using System.Net;

namespace JRK.JevRunner.Infrastructure;

internal class ExceptionTranslator
{
    internal static Exception TranslateRequestException(Exception ex)
    {
        return ex switch
        {
            HttpRequestException {StatusCode: HttpStatusCode.Unauthorized} unauthorized => new JevKnownException(
                "The request was forbidden. Ensure that you called with a valid api token", unauthorized),
            HttpRequestException {StatusCode: HttpStatusCode.UnprocessableEntity} unprocessable => new JevKnownException("The request body failed validation — for example a missing required field or a malformed question. The body details the offending field.",unprocessable),
            HttpRequestException {StatusCode: HttpStatusCode.TooManyRequests} toManyRequests => new JevKnownException("You may have exceeded your rate limit",toManyRequests),
            HttpRequestException {StatusCode: (HttpStatusCode)509} overloaded => new JevKnownException("TypeSafe is temporarily overloaded. Retry after a short delay.", overloaded),
            _ => ex
        };
    }
}

public class JevKnownException(string errorMessage, HttpRequestException httpRequestException)
    : Exception(errorMessage, httpRequestException);