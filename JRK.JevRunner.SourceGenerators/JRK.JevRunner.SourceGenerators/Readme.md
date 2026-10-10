# Jev query source generator

`JevQueryGenerator` generates request construction and typed answer accessors for
classes and record classes annotated with `JRK.JevRunner.Annotation.JevQueryAttribute`.

## Query contract

- The query and all containing types must be partial classes or record classes.
  The query must be non-static, and none of these types may be file-local.
- Supply readable instance properties named `State` and `JevModel`, compatible with
  the runtime `Request` constructor.
- Each question property's type implements exactly one construction of a supported
  definition interface from `JRK.JevRunner.Annotation`. Discovery compares Roslyn
  original-definition symbol identities, including inherited interfaces. Interface-typed
  properties, explicit implementations, and type parameters constrained to a definition
  interface are supported. Unrelated structural or same-named lookalikes are ignored.
- The generator explicitly maps each interface to its runtime builder, answer, and
  `Response` accessor. There is no mapping inferred from question or attribute names.
  Multiple supported constructions (including two instruction types of the same
  interface) produce JEV003 rather than selecting one arbitrarily.

### Strict question configuration contract

| Interface | Required definition properties | Explicit runtime mapping and configuration |
| --- | --- | --- |
| `INoulQuestionDefinition<TInstruction>` | `Instructions`, `string? Yes`, `string? No` | `NoulQuestionBuilder`, `NoulAnswer`, `GetRequiredNoulAnswer`; apply optional criteria |
| `IChoiceQuestionDefinition<TInstruction>` | `Instructions`, `ChoiceCriteria[] Choices` | `ChoiceQuestionBuilder`, `ChoiceAnswer`, `GetRequiredChoiceAnswer`; call `AddChoice(item.Choice, item.Instruction)` for each entry |
| `IScoreQuestionDefinition<TInstruction>` | `Instructions`, `string[] Criterias` | `ScoreQuestionBuilder`, `ScoreAnswer`, `GetRequiredScoreAnswer`; call `AddCriteria(item)` for each entry |

`Instructions` must be compatible with the builder constructor (the runtime accepts
`JevMessage`, including its implicit conversion from strings). `ChoiceCriteria`
is the runtime type with `Choice` and `Instruction` string properties; structurally
similar custom types are not substitutes. The score collection is named **`Criterias`**,
matching the runtime API. The definition contracts require arrays; generated configuration
enumerates them in order. Score definitions have only the instruction generic parameter.

For noul questions, both non-null descriptions are passed to `AddYesNo`. Both
are required to generate criteria: if either is null, the builder's criteria are
left unset and the runtime omits the JSON `criteria` member. Empty strings are
supplied values, not missing descriptions. The generated request reads both
descriptions at runtime.

The generator enumerates each collection in its supplied order and fully configures
the builder before adding the question to the request. Empty collections add no
entries. The query's question properties must be accessible, non-static, non-indexed,
and have an accessible getter. Each question is read once and cast to the matched
interface; all instructions and configuration are accessed through that contract,
so concrete public member names and visibility do not affect explicit implementations.
Missing or unreadable query properties and incomplete concrete interface implementations
produce JEV002. Invalid interface implementations also produce ordinary C# compiler
errors (for example, a wrongly typed collection). Incompatible instruction types or
runtime API signatures produce JEV004 through binding of the emitted code.

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
| JEV002 | Missing or unreadable query property, or incomplete question definition implementation |
| JEV003 | Multiple supported interface constructions or missing explicit builder/answer/accessor mapping |
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
`JRK.JevRunner.Tests/GeneratedNoulCriteriaTest.cs` additionally verifies actual
generated runtime JSON: criteria are included when both descriptions are supplied
and omitted when either or both are null.

From the repository root, run the tests with:

```powershell
dotnet run --project JRK.JevRunner.SourceGenerators/JRK.JevRunner.SourceGenerators.Tests/JRK.JevRunner.SourceGenerators.Tests.csproj
dotnet run --project JRK.JevRunner.Tests/JRK.JevRunner.Tests.csproj
```

For debugging, use the Roslyn component profile in
[launchSettings.json](Properties/launchSettings.json) against the sample, or debug
`JevQueryGeneratorTests` in Rider.
