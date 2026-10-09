# JRK.JevRunner

An unofficial .NET client for Jev and the TypeSafe System One API. Submit application state and named questions, and consume typed responses.

## Requirements

- .NET 11
- A TypeSafe API key

## Installation

```shell
dotnet add package JRK.JevRunner
```

## Quick start

Set the `TYPESAFE_API_KEY` environment variable to your API key. Do not commit API keys to source control.

```csharp
using JRK.JevRunner;
using JRK.JevRunner.Requests;

var apiKey = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY")
    ?? throw new InvalidOperationException("Set TYPESAFE_API_KEY before running.");

var runner = JevRunner.Init(apiKey);
var request = new Request("The brown fox jumped over the lazy dog.", JevModel.Latest)
    .AddQuestion(new NoulQuestionBuilder(
        "dog_was_jumped_over",
        "Did any animal jump over a lazy dog?"));

var response = await runner.Execute(request);
Console.WriteLine(response.GetRequiredNoulAnswer("dog_was_jumped_over"));
```

The package uses the `JRK.JevRunner.Core` namespaces. You can also supply an `HttpClient` and an `ILogger<JevRunner>` through the `JevRunner` constructor.

## License

MIT License. Copyright (c) 2026 Jeppe Roi Kristensen.
