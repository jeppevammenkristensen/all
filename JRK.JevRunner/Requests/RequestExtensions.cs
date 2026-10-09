using Microsoft.Extensions.Logging;

namespace JRK.JevRunner.Requests;

/// <summary>
/// Provides fluent methods for adding questions to requests.
/// </summary>
public static class RequestExtensions
{
    extension(Request request)
    {
        /// <summary>
        /// Appends a question to the request and returns the same request for further configuration.
        /// </summary>
        public Request AddQuestion(Question question)
        {
            request.Questions.Add(question);
            return request;
        }

        /// <summary>
        /// Builds and appends a question to the request and returns the same request for further configuration.
        /// </summary>
        public Request AddQuestion<TQuestion>(IQuestionBuilder<TQuestion> questionBuilder) where TQuestion : IQuestion
        {
            return request.AddQuestion(questionBuilder.BuildQuestion());
        }

        public async Task<Response> Execute(JevRunner runner)
        {
            return await runner.Execute(request);
        }
        
        public async Task<Response> Execute(string apiKey, ILogger<JevRunner>? logger = null)
        {
            return await JevRunner.Init(apiKey, logger).Execute(request);
        }
    }
}
