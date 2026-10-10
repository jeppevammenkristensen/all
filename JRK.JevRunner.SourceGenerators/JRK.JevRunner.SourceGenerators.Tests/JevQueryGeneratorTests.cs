using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace JRK.JevRunner.SourceGenerators.Tests;

public class JevQueryGeneratorTests
{
    [Fact]
    public void NoulQueryGeneratesCompilableRequestAndTypedAnswerAccessor()
    {
        var (result, output) = Generate(QuerySource() + """

            public static class Consumer
            {
                public static bool Verify()
                {
                    var query = new TestNamespace.TravelQuery();
                    var request = query.GetRequest();
                    var expected = new JRK.JevRunner.Responses.NoulAnswer();
                    var response = new JRK.JevRunner.Response();
                    response.Answers.Add("Budget", expected);
                    return request.State == query.State && request.Model == query.JevModel
                        && request.Questions.Count == 1
                        && request.Questions[0].Name == "Budget"
                        && request.Questions[0].Instructions == query.Budget.Instructions
                        && object.ReferenceEquals(expected, query.GetBudgetAnswer(response));
                }
            }
            """);

        var generated = AssertSuccessful(result, output);
        Assert.Contains("nameof(this.@Budget)", generated);
        Assert.Contains("global::JRK.JevRunner.Request GetRequest()", generated);
        Assert.Contains("global::JRK.JevRunner.Responses.NoulAnswer @GetBudgetAnswer", generated);
        Assert.Contains("/// <summary>", generated);
        AssertConsumerSucceeds(output);
    }

    [Theory]
    [InlineData("class")]
    [InlineData("record class")]
    public void MultipleQuestionsUsePropertyNamesIncludingEscapedIdentifiers(string declaration)
    {
        var (result, output) = Generate($$"""
            namespace TestNamespace
            {
                [JRK.JevRunner.Annotation.JevQuery]
                public partial {{declaration}} TravelQuery
                {
                    public string State => "travel state";
                    public string JevModel => "jev-latest";
                    public BudgetQuestion Budget { get; } = new();
                    public BudgetQuestion @event { get; } = new();
                }

                [JRK.JevRunner.Annotation.NoulQuestion]
                public class BudgetQuestion
                {
                    public string Instructions => "Is the budget sufficient?";
                }
            }

            public static class Consumer
            {
                public static bool Verify()
                {
                    var query = new TestNamespace.TravelQuery();
                    var request = query.GetRequest();
                    var budgetAnswer = new JRK.JevRunner.Responses.NoulAnswer();
                    var eventAnswer = new JRK.JevRunner.Responses.NoulAnswer();
                    var response = new JRK.JevRunner.Response();
                    response.Answers.Add("Budget", budgetAnswer);
                    response.Answers.Add("event", eventAnswer);
                    return request.Questions.Count == 2
                        && request.Questions[0].Name == "Budget"
                        && request.Questions[1].Name == "event"
                        && object.ReferenceEquals(budgetAnswer, query.GetBudgetAnswer(response))
                        && object.ReferenceEquals(eventAnswer, query.GeteventAnswer(response));
                }
            }
            """);

        AssertSuccessful(result, output);
        AssertConsumerSucceeds(output);
    }

    [Fact]
    public void NestedGenericRecordQueryGeneratesInsideItsContainingType()
    {
        var (result, output) = Generate("""
            namespace TestNamespace
            {
                public partial class Container<T> where T : class
                {
                    [JRK.JevRunner.Annotation.JevQuery]
                    public partial record class Query
                    {
                        public string State => "state";
                        public string JevModel => "model";
                        public Question Budget { get; } = new();
                    }
                }

                [JRK.JevRunner.Annotation.NoulQuestion]
                public class Question
                {
                    public string Instructions => "instructions";
                }
            }
            """);

        var generated = AssertSuccessful(result, output);
        Assert.Contains("partial class @Container<@T>", generated);
        Assert.Contains("partial record class @Query", generated);
        var query = output.GetTypeByMetadataName("TestNamespace.Container`1+Query");
        Assert.NotNull(query);
        Assert.Single(query.GetMembers("GetRequest"));
        Assert.Single(query.GetMembers("GetBudgetAnswer"));
    }

    [Theory]
    [InlineData("State")]
    [InlineData("JevModel")]
    [InlineData("Instructions")]
    public void MissingRequiredPropertyReportsJev002(string missingProperty)
    {
        var source = QuerySource(
            state: missingProperty == "State" ? "" : "public string State => \"state\";",
            model: missingProperty == "JevModel" ? "" : "public string JevModel => \"model\";",
            instructions: missingProperty == "Instructions" ? "" : "public string Instructions => \"instructions\";");

        var diagnostic = AssertRejected(source, "JEV002");

        Assert.Contains($"'{missingProperty}'", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("class")]
    [InlineData("record class")]
    [InlineData("static partial class")]
    [InlineData("partial struct")]
    [InlineData("partial record struct")]
    public void UnsupportedQueryDeclarationReportsJev001(string declaration)
    {
        AssertRejected(QuerySource(declaration: declaration), "JEV001");
    }

    [Fact]
    public void NonpartialContainingTypeReportsJev001()
    {
        AssertRejected("""
            public class Container
            {
                [JRK.JevRunner.Annotation.JevQuery]
                public partial class Query
                {
                    public string State => "state";
                    public string JevModel => "model";
                }
            }
            """, "JEV001");
    }

    [Theory]
    [InlineData("UnknownQuestion")]
    [InlineData("OddMarker")]
    [InlineData("NoulQuestion, UnknownQuestion")]
    public void UnsupportedQuestionMarkerReportsJev003(string marker)
    {
        var diagnostic = AssertRejected(QuerySource(marker: marker) + """

            namespace JRK.JevRunner.Annotation
            {
                public sealed class UnknownQuestionAttribute : System.Attribute, IQuestionAttribute { }
                public sealed class OddMarkerAttribute : System.Attribute, IQuestionAttribute { }
            }
            """, "JEV003");

        Assert.Contains("Budget", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("public void GetRequest() { }", "GetRequest")]
    [InlineData("public int GetRequest => 0;", "GetRequest")]
    [InlineData("public void GetBudgetAnswer() { }", "GetBudgetAnswer")]
    public void GeneratedMemberCollisionReportsJev005(string existingMember, string memberName)
    {
        var diagnostic = AssertRejected(QuerySource(extraMember: existingMember), "JEV005");

        Assert.Contains($"'{memberName}'", diagnostic.GetMessage());
    }

    [Fact]
    public void IncompatibleRequestConstructorArgumentReportsJev004()
    {
        var diagnostic = AssertRejected(QuerySource(state: "public int State => 42;"), "JEV004");

        Assert.Contains("request/response API", diagnostic.GetMessage());
    }

    private static string QuerySource(
        string declaration = "partial class",
        string state = "public string State => \"travel state\";",
        string model = "public string JevModel => \"jev-latest\";",
        string instructions = "public string Instructions => \"Is the budget sufficient?\";",
        string marker = "NoulQuestion",
        string extraMember = "") => $$"""
        namespace TestNamespace
        {
            using JRK.JevRunner.Annotation;

            [JevQuery]
            public {{declaration}} TravelQuery
            {
                {{state}}
                {{model}}
                public BudgetQuestion Budget { get; } = new();
                {{extraMember}}
            }

            [{{marker}}]
            public class BudgetQuestion
            {
                {{instructions}}
            }
        }
        """;

    private static (GeneratorDriverRunResult Result, Compilation Output) Generate(string source)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create("JevQueryTests_" + Guid.NewGuid().ToString("N"),
            new[]
            {
                CSharpSyntaxTree.ParseText(RuntimeContract, parseOptions, cancellationToken: cancellationToken),
                CSharpSyntaxTree.ParseText(source, parseOptions, path: "Query.cs", cancellationToken: cancellationToken)
            },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new JevQueryGenerator().AsSourceGenerator() }, parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, cancellationToken);
        return (driver.GetRunResult(), output);
    }

    private static string AssertSuccessful(GeneratorDriverRunResult result, Compilation output)
    {
        var generatorResult = Assert.Single(result.Results);
        Assert.Null(generatorResult.Exception);
        Assert.Empty(result.Diagnostics);
        Assert.Empty(output.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var generated = Assert.Single(generatorResult.GeneratedSources);
        Assert.StartsWith("JevQuery_", generated.HintName);
        Assert.EndsWith(".g.cs", generated.HintName);
        using var assembly = new MemoryStream();
        var emit = output.Emit(assembly, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        return generated.SourceText.ToString();
    }

    private static Diagnostic AssertRejected(string source, string diagnosticId)
    {
        var (result, _) = Generate(source);
        var generatorResult = Assert.Single(result.Results);
        Assert.Null(generatorResult.Exception);
        Assert.Empty(generatorResult.GeneratedSources);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(diagnosticId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
        Assert.Equal("Query.cs", diagnostic.Location.SourceTree?.FilePath);
        return diagnostic;
    }

    private static void AssertConsumerSucceeds(Compilation output)
    {
        using var assembly = new MemoryStream();
        var emit = output.Emit(assembly, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var consumer = Assembly.Load(assembly.ToArray()).GetType("Consumer", throwOnError: true)!;
        var verify = consumer.GetMethod("Verify", BindingFlags.Public | BindingFlags.Static)!;
        Assert.True(Assert.IsType<bool>(verify.Invoke(null, null)));
    }

    // Deliberately independent of JRK.JevRunner: the runtime bundles this analyzer, so the
    // generator tests exercise its binding contract without introducing a runtime dependency.
    private const string RuntimeContract = """
        namespace JRK.JevRunner.Annotation
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class JevQueryAttribute : System.Attribute { }
            public interface IQuestionAttribute { }
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class NoulQuestionAttribute : System.Attribute, IQuestionAttribute { }
        }

        namespace JRK.JevRunner
        {
            public sealed class Request
            {
                public Request(string state, string model) { State = state; Model = model; }
                public string State { get; }
                public string Model { get; }
                public System.Collections.Generic.List<Requests.NoulQuestionBuilder> Questions { get; } = new();
            }

            public sealed class Response
            {
                public System.Collections.Generic.Dictionary<string, Responses.NoulAnswer> Answers { get; } = new();
                public Responses.NoulAnswer GetRequiredNoulAnswer(string name) => Answers[name];
            }
        }

        namespace JRK.JevRunner.Requests
        {
            public sealed class NoulQuestionBuilder
            {
                public NoulQuestionBuilder(string name, string instructions)
                { Name = name; Instructions = instructions; }
                public string Name { get; }
                public string Instructions { get; }
            }

            public static class RequestExtensions
            {
                public static void AddQuestion(JRK.JevRunner.Request request, NoulQuestionBuilder builder)
                    => request.Questions.Add(builder);
            }
        }

        namespace JRK.JevRunner.Responses
        {
            public sealed class NoulAnswer { }
        }
        """;
}
