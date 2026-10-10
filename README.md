# JRK.JevRunner

[![NuGet](https://img.shields.io/nuget/v/JRK.JevRunner.svg)](https://www.nuget.org/packages/JRK.JevRunner)

An unofficial .NET client for Jev and the TypeSafe System One API. Submit application state and named questions, and consume typed responses.

## Requirements

- .NET 11 or later
- A TypeSafe API key

## Installation

```shell
dotnet add package JRK.JevRunner
```

The package includes its source generators automatically; no separate generator package or
analyzer reference is needed when consuming it through NuGet. The generator assembly targets
`netstandard2.0` for compiler-host compatibility, while the client targets `net11.0`.

For repository development, analyzer project references are not transitive. See the sample
project's explicit analyzer reference. Build and pack the client with:

```shell
dotnet pack JRK.JevRunner/JRK.JevRunner.csproj -c Release
dotnet build JRK.JevRunner.SourceGenerators/JRK.JevRunner.SourceGenerators.Sample/JRK.JevRunner.SourceGenerators.Sample.csproj -c Release -p:UsePackedJevRunner=true
```

The second command is a build-only packaging smoke check: it consumes the locally packed
NuGet without an explicit generator reference and compiles code that requires generated types.

Package restores audit both direct and transitive dependencies and fail on known vulnerabilities
or audit-feed failures. To repeat the advisory check:

```shell
dotnet restore all.slnx --force-evaluate
dotnet package list --project all.slnx --vulnerable --include-transitive --no-restore
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

This package is targeting C# 15 and dotnet sdk which is currently in the time of writing in preview

## License

MIT License. Copyright (c) 2026 Jeppe Roi Kristensen.
