using System;
using JetBrains.Annotations;
using JRK.JevRunner;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JRK.JevRunner.Tests;

[TestSubject(typeof(JevRunnerExtensions))]
public class JevRunnerExtensionsTest
{
    /// <summary>
    /// A non-empty API key creates a runner through the convenience factory.
    /// </summary>
    [Fact]
    public void Init_WithApiKey_ReturnsRunner()
    {
        var runner = JevRunner.Init("test-api-key");

        Assert.NotNull(runner);
        Assert.IsType<JevRunner>(runner);
    }

    /// <summary>
    /// The optional logger can be supplied without changing the factory result.
    /// </summary>
    [Fact]
    public void Init_WithLogger_ReturnsRunner()
    {
        var runner = JevRunner.Init("test-api-key", NullLogger<JevRunner>.Instance);

        Assert.IsType<JevRunner>(runner);
    }

    /// <summary>
    /// Missing or whitespace-only API keys are rejected by the created runner.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void Init_WithInvalidApiKey_ThrowsArgumentException(string? apiKey)
    {
        var exception = Assert.Throws<ArgumentException>(() => JevRunner.Init(apiKey!));

        Assert.Equal("API key cannot be null or empty", exception.Message);
    }
}