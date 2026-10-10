# Jev query source generator

`JevQueryGenerator` generates request construction and typed answer accessors for
classes and record classes annotated with `JRK.JevRunner.Annotation.JevQueryAttribute`.

## Query contract

- The query and all containing types must be partial classes or record classes.
  The query must be non-static, and none of these types may be file-local.
- Supply readable instance properties named `State` and `JevModel`, compatible with
  the runtime `Request` constructor.
- Each question property has a type annotated with an attribute implementing
  `IQuestionAttribute`, such as `[NoulQuestion]`. That type supplies a readable
  instance `Instructions` property compatible with its question builder.
- A supported marker `{Prefix}QuestionAttribute` maps to
  `Requests.{Prefix}QuestionBuilder`, `Responses.{Prefix}Answer`, and
  `Response.GetRequired{Prefix}Answer(string)`.

For a question property named `Budget`, the generated `GetRequest()` adds a
question named `Budget`, and `GetBudgetAnswer(Response)` retrieves that named
typed answer. Question names come from property identifiers, not their types.
Existing members must not collide with these generated method names.

See [Examples.cs](../JRK.JevRunner.SourceGenerators.Sample/Examples.cs) for a
complete noul example that consumes both generated methods.

## Diagnostics

| ID | Meaning |
| --- | --- |
| JEV001 | Unsupported query or containing type declaration |
| JEV002 | Missing or unreadable required query/question property |
| JEV003 | Unsupported question marker or missing builder/answer/accessor mapping |
| JEV004 | Generated code cannot bind to the request/response API |
| JEV005 | An existing member collides with a generated method name |

## Projects and packaging

The generator targets `netstandard2.0` for compiler-host compatibility. It is not
a standalone NuGet package: `JRK.JevRunner` bundles its DLL under
`analyzers/dotnet/cs`, so NuGet consumers receive it automatically.

The sample and tests target .NET 11. The sample uses explicit analyzer and runtime
project references for local development. Its `UsePackedJevRunner=true` setting
instead consumes only the runtime NuGet package to check analyzer packaging.

The tests use Roslyn `GeneratorDriver` with isolated runtime contract stubs. They
check diagnostics, generated API shape, compilation/emission, and named request
and answer behavior without referencing the runtime project.

From the repository root, run the tests with:

```powershell
dotnet run --project JRK.JevRunner.SourceGenerators/JRK.JevRunner.SourceGenerators.Tests/JRK.JevRunner.SourceGenerators.Tests.csproj
```

For debugging, use the Roslyn component profile in
[launchSettings.json](Properties/launchSettings.json) against the sample, or debug
`JevQueryGeneratorTests` in Rider.
