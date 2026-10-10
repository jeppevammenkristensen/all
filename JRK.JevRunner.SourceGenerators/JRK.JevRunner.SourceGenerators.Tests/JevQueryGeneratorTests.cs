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

    [Theory]
    [InlineData("Choice")]
    [InlineData("Score")]
    [InlineData("Mixed")]
    public void ConfiguredQuestionsPreserveOrderingAndReturnFullTypedAnswers(string kind)
    {
        var choice = kind != "Score";
        var score = kind != "Choice";
        var mixed = kind == "Mixed";
        var (result, output) = Generate($$"""
                                          namespace TestNamespace
                                          {
                                              [JRK.JevRunner.Annotation.JevQuery]
                                              public partial class TravelQuery<__jevQuestion0, __jevItems0, __jevItem0, request, response>
                                              {
                                                  public string State => "travel state";
                                                  public string JevModel => "jev-latest";
                                                  {{(mixed ? "public BudgetQuestion Budget { get; } = new();" : "")}}
                                                  {{(choice ? "public DestinationQuestion @event { get; } = new();" : "")}}
                                                  {{(score ? "public ComfortQuestion __jevQuestion1 { get; } = new();" : "")}}
                                              }

                                              [JRK.JevRunner.Annotation.NoulQuestion]
                                              public class BudgetQuestion
                                              {
                                                  public string Instructions => "Is the budget sufficient?";
                                              }

                                              [JRK.JevRunner.Annotation.ChoiceQuestion]
                                              public class DestinationQuestion
                                              {
                                                  public string Instructions => "Choose a destination.";
                                                  public System.Collections.Generic.IEnumerable<JRK.JevRunner.Requests.ChoiceCriteria> Choices
                                                      => new JRK.JevRunner.Requests.ChoiceCriteria[]
                                                      {
                                                          new("city", "Prefer museums."), new("coast", "Prefer beaches.")
                                                      };
                                              }

                                              [JRK.JevRunner.Annotation.ScoreQuestion]
                                              public class ComfortQuestion
                                              {
                                                  public string Instructions => "Rate comfort.";
                                                  public System.Collections.Generic.IEnumerable<string> Criterias
                                                      => new string[] { "basic", "comfortable", "luxurious" };
                                              }
                                          }

                                          public static class Consumer
                                          {
                                              public static bool Verify()
                                              {
                                                  var query = new TestNamespace.TravelQuery<int, int, int, int, int>();
                                                  var request = query.GetRequest();
                                                  var response = new JRK.JevRunner.Response();
                                                  if (request.State != query.State || request.Model != query.JevModel
                                                      || request.Questions.Count != {{(mixed ? 3 : 1)}}) return false;
                                                  var index = 0;
                                                  {{(mixed ? """
                                                             var noul = new JRK.JevRunner.Responses.NoulAnswer();
                                                             response.Answers.Add("Budget", noul);
                                                             if (request.Questions[index++].Name != "Budget"
                                                                 || !object.ReferenceEquals(noul, query.GetBudgetAnswer(response))) return false;
                                                             """ : "")}}
                                                  {{(choice ? """
                                                              var choiceQuestion = request.Questions[index++];
                                                              if (choiceQuestion.Name != "event" || choiceQuestion.Instructions != query.@event.Instructions
                                                                  || choiceQuestion.Choices.Count != 2
                                                                  || choiceQuestion.Choices[0].Choice != "city"
                                                                  || choiceQuestion.Choices[0].Instruction != "Prefer museums."
                                                                  || choiceQuestion.Choices[1].Choice != "coast"
                                                                  || choiceQuestion.Choices[1].Instruction != "Prefer beaches.") return false;
                                                              var choiceAnswer = new JRK.JevRunner.Responses.ChoiceAnswer
                                                              {
                                                                  Choice = "coast", Probabilities = new double[] { 0.2, 0.8 }, Confidence = 0.6
                                                              };
                                                              response.Answers.Add("event", choiceAnswer);
                                                              JRK.JevRunner.Responses.ChoiceAnswer actualChoice = query.GeteventAnswer(response);
                                                              if (!object.ReferenceEquals(choiceAnswer, actualChoice) || actualChoice.Choice != "coast"
                                                                  || actualChoice.Probabilities[1] != 0.8 || actualChoice.Confidence != 0.6) return false;
                                                              """ : "")}}
                                                  {{(score ? """
                                                             var scoreQuestion = request.Questions[index++];
                                                             if (scoreQuestion.Name != "__jevQuestion1"
                                                                 || scoreQuestion.Instructions != query.__jevQuestion1.Instructions
                                                                 || scoreQuestion.Criterias.Count != 3
                                                                 || scoreQuestion.Criterias[0] != "basic"
                                                                 || scoreQuestion.Criterias[1] != "comfortable"
                                                                 || scoreQuestion.Criterias[2] != "luxurious") return false;
                                                             var scoreAnswer = new JRK.JevRunner.Responses.ScoreAnswer
                                                             {
                                                                 Score = 1.7, LegendProbabilities = new double[] { 0.1, 0.1, 0.8 }, Confidence = 0.7
                                                             };
                                                             response.Answers.Add("__jevQuestion1", scoreAnswer);
                                                             JRK.JevRunner.Responses.ScoreAnswer actualScore = query.Get__jevQuestion1Answer(response);
                                                             if (!object.ReferenceEquals(scoreAnswer, actualScore) || actualScore.Score != 1.7
                                                                 || actualScore.LegendProbabilities[2] != 0.8 || actualScore.Confidence != 0.7) return false;
                                                             """ : "")}}
                                                  return true;
                                              }
                                          }
                                          """);

        var generated = AssertSuccessful(result, output);
        if (choice)
        {
            Assert.Contains(
                "global::System.Collections.Generic.IEnumerable<global::JRK.JevRunner.Requests.ChoiceCriteria>",
                generated);
            Assert.Contains(".@AddChoice(", generated);
            Assert.Contains("global::JRK.JevRunner.Responses.ChoiceAnswer @GeteventAnswer", generated);
        }

        if (score)
        {
            Assert.Contains("global::System.Collections.Generic.IEnumerable<global::System.String>", generated);
            Assert.Contains(".@AddCriteria(", generated);
            Assert.Contains("global::JRK.JevRunner.Responses.ScoreAnswer @Get__jevQuestion1Answer", generated);
        }

        AssertConsumerSucceeds(output);
    }

    [Theory]
    [InlineData("ChoiceQuestion", "Choices", "")]
    [InlineData("ScoreQuestion", "Criterias", "")]
    [InlineData("ChoiceQuestion", "Choices", "public string[] Choices;")]
    [InlineData("ScoreQuestion", "Criterias", "public string[] Criterias;")]
    [InlineData("ChoiceQuestion", "Choices", "public static string[] Choices => new string[0];")]
    [InlineData("ScoreQuestion", "Criterias", "public static string[] Criterias => new string[0];")]
    [InlineData("ChoiceQuestion", "Choices", "public string[] Choices { set { } }")]
    [InlineData("ScoreQuestion", "Criterias", "public string[] Criterias { set { } }")]
    [InlineData("ChoiceQuestion", "Choices", "private string[] Choices => new string[0];")]
    [InlineData("ScoreQuestion", "Criterias", "private string[] Criterias => new string[0];")]
    [InlineData("ChoiceQuestion", "Choices", "public string[] Choices { private get; set; }")]
    [InlineData("ScoreQuestion", "Criterias", "public string[] Criterias { private get; set; }")]
    public void MissingOrUnreadableConfigurationReportsJev002(string marker, string property, string member)
    {
        var diagnostic = AssertRejected(QuerySource(marker: marker,
            instructions: "public string Instructions => \"instructions\"; " + member), "JEV002");

        Assert.Contains(property, diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("ChoiceQuestion", "Choices", "string[]")]
    [InlineData("ChoiceQuestion", "Choices", "object[]")]
    [InlineData("ChoiceQuestion", "Choices", "(string Choice, string Instruction)[]")]
    [InlineData("ChoiceQuestion", "Choices", "int")]
    [InlineData("ScoreQuestion", "Criterias", "int[]")]
    [InlineData("ScoreQuestion", "Criterias", "object[]")]
    [InlineData("ScoreQuestion", "Criterias", "string")]
    [InlineData("ScoreQuestion", "Criterias", "int")]
    public void IncompatibleConfigurationCollectionReportsJev004(string marker, string property, string type)
    {
        AssertRejected(QuerySource(marker: marker,
                instructions: $"public string Instructions => \"instructions\"; public {type} {property} => default;"),
            "JEV004");
    }

    [Theory]
    [InlineData("ChoiceQuestion")]
    [InlineData("ScoreQuestion")]
    public void ConfiguredQuestionRequiresInstructions(string marker)
    {
        var diagnostic = AssertRejected(QuerySource(marker: marker, instructions: ""), "JEV002");

        Assert.Contains("Instructions", diagnostic.GetMessage());
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
            new[] {new JevQueryGenerator().AsSourceGenerator()}, parseOptions: parseOptions);
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
                                               [System.AttributeUsage(System.AttributeTargets.Class)]
                                               public sealed class ChoiceQuestionAttribute : System.Attribute, IQuestionAttribute { }
                                               [System.AttributeUsage(System.AttributeTargets.Class)]
                                               public sealed class ScoreQuestionAttribute : System.Attribute, IQuestionAttribute { }
                                           }

                                           namespace JRK.JevRunner
                                           {
                                               public sealed class Request
                                               {
                                                   public Request(string state, string model) { State = state; Model = model; }
                                                   public string State { get; }
                                                   public string Model { get; }
                                                   public System.Collections.Generic.List<Requests.Question> Questions { get; } = new();
                                               }

                                               public sealed class Response
                                               {
                                                   public System.Collections.Generic.Dictionary<string, object> Answers { get; } = new();
                                                   public Responses.NoulAnswer GetRequiredNoulAnswer(string name) => (Responses.NoulAnswer)Answers[name];
                                                   public Responses.ChoiceAnswer GetRequiredChoiceAnswer(string name) => (Responses.ChoiceAnswer)Answers[name];
                                                   public Responses.ScoreAnswer GetRequiredScoreAnswer(string name) => (Responses.ScoreAnswer)Answers[name];
                                               }
                                           }

                                           namespace JRK.JevRunner.Requests
                                           {
                                               public sealed class ChoiceCriteria
                                               {
                                                   public ChoiceCriteria(string choice, string instruction) { Choice = choice; Instruction = instruction; }
                                                   public string Choice { get; }
                                                   public string Instruction { get; }
                                               }

                                               public sealed class Question
                                               {
                                                   public Question(string name, string instructions) { Name = name; Instructions = instructions; }
                                                   public string Name { get; }
                                                   public string Instructions { get; }
                                                   public System.Collections.Generic.List<ChoiceCriteria> Choices { get; } = new();
                                                   public System.Collections.Generic.List<string> Criterias { get; } = new();
                                               }

                                               public abstract class QuestionBuilder
                                               {
                                                   protected QuestionBuilder(string name, string instructions) { Name = name; Instructions = instructions; }
                                                   public string Name { get; }
                                                   public string Instructions { get; }
                                                   public abstract Question Build();
                                               }

                                               public sealed class NoulQuestionBuilder : QuestionBuilder
                                               {
                                                   public NoulQuestionBuilder(string name, string instructions) : base(name, instructions) { }
                                                   public override Question Build() => new Question(Name, Instructions);
                                               }

                                               public sealed class ChoiceQuestionBuilder : QuestionBuilder
                                               {
                                                   public ChoiceQuestionBuilder(string name, string instructions) : base(name, instructions) { }
                                                   private readonly System.Collections.Generic.List<ChoiceCriteria> choices = new();
                                                   public ChoiceQuestionBuilder AddChoice(string choice, string instruction)
                                                   { choices.Add(new ChoiceCriteria(choice, instruction)); return this; }
                                                   public override Question Build()
                                                   {
                                                       var question = new Question(Name, Instructions);
                                                       question.Choices.AddRange(choices);
                                                       return question;
                                                   }
                                               }

                                               public sealed class ScoreQuestionBuilder : QuestionBuilder
                                               {
                                                   public ScoreQuestionBuilder(string name, string instructions) : base(name, instructions) { }
                                                   private readonly System.Collections.Generic.List<string> criterias = new();
                                                   public ScoreQuestionBuilder AddCriteria(string criteria) { criterias.Add(criteria); return this; }
                                                   public override Question Build()
                                                   {
                                                       var question = new Question(Name, Instructions);
                                                       question.Criterias.AddRange(criterias);
                                                       return question;
                                                   }
                                               }

                                               public static class RequestExtensions
                                               {
                                                   public static void AddQuestion(JRK.JevRunner.Request request, QuestionBuilder builder)
                                                       => request.Questions.Add(builder.Build());
                                               }
                                           }

                                           namespace JRK.JevRunner.Responses
                                           {
                                               public sealed class NoulAnswer { }
                                               public sealed class ChoiceAnswer
                                               {
                                                   public string Choice { get; set; }
                                                   public double[] Probabilities { get; set; }
                                                   public double Confidence { get; set; }
                                               }
                                               public sealed class ScoreAnswer
                                               {
                                                   public double Score { get; set; }
                                                   public double[] LegendProbabilities { get; set; }
                                                   public double Confidence { get; set; }
                                               }
                                           }
                                           """;
}