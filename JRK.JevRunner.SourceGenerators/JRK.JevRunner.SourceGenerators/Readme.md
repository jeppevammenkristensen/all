# Jev query source generator

`JevQueryGenerator` generates request construction and typed answer accessors for
classes and record classes annotated with `JRK.JevRunner.Annotation.JevQueryAttribute`.

## Query contract

- The query and all containing types must be partial classes or record classes.
  The query must be non-static, and none of these types may be file-local.
- Supply readable instance properties named `State` and `JevModel`, compatible with
  the runtime `Request` constructor.
- Each question property has a type annotated with an attribute implementing
  `IQuestionAttribute`, such as `[NoulQuestion]`, `[ChoiceQuestion]`, or
  `[ScoreQuestion]`. That type supplies a readable
  instance `Instructions` property compatible with its question builder.
- A supported marker `{Prefix}QuestionAttribute` maps to
  `Requests.{Prefix}QuestionBuilder`, `Responses.{Prefix}Answer`, and
  `Response.GetRequired{Prefix}Answer(string)`.

### Strict question configuration contract

| Marker | Required readable instance properties | Generated configuration |
| --- | --- | --- |
| `[NoulQuestion]` | `Instructions` | Construct `NoulQuestionBuilder` |
| `[ChoiceQuestion]` | `Instructions`, `Choices` assignable to `IEnumerable<JRK.JevRunner.Requests.ChoiceCriteria>` | Call `AddChoice(item.Choice, item.Instruction)` for each entry |
| `[ScoreQuestion]` | `Instructions`, `Criterias` assignable to `IEnumerable<string>` | Call `AddCriteria(item)` for each entry |

`Instructions` must be compatible with the builder constructor (the runtime accepts
`JevMessage`, including its implicit conversion from strings). `ChoiceCriteria`
is the runtime type with `Choice` and `Instruction` string properties; structurally
similar custom types are not substitutes. The score collection is named **`Criterias`**,
matching the runtime API. Arrays, lists, and other collections implementing the
required generic enumerable contract are supported.

The generator enumerates each collection in its supplied order and fully configures
the builder before adding the question to the request. Empty collections add no
entries. Properties must be accessible, non-static, non-indexed, and have an accessible
getter; fields and write-only properties do not satisfy this contract. Missing or
unreadable properties produce JEV002. Incompatible collection or instruction types
produce JEV004 through binding of the emitted code to the runtime API.

For a question property named `Budget`, the generated `GetRequest()` adds a
question named `Budget`, and `GetBudgetAnswer(Response)` retrieves that named
typed answer. Choice and score accessors return the full `ChoiceAnswer` and
`ScoreAnswer` objects, including probabilities and confidence, rather than just a
selected string or numeric score. Question names come from property identifiers, not their types.
Existing members must not collide with these generated method names.

See [Examples.cs](../JRK.JevRunner.SourceGenerators.Sample/Examples.cs) for a
complete mixed noul, choice, and score example that constructs a request and consumes
all three typed answer accessors against the actual runtime API.

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
