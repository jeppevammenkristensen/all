using AutoSpectre;
using JRK.JevRunner;
using JRK.JevRunner.Annotation;
using JRK.JevRunner.Requests;
using Playground;
using Spectre.Console;

var connector = new Connector().Prompt();
var jevRunner = connector.GetRunner();


var jevQuery = new JevQuery(new FileData("Some.txt", "bla bla bla"));
var response = await jevQuery.GetRequest().Execute(jevRunner);
AnsiConsole.MarkupLineInterpolated($"[green]{jevQuery.GetDangerZoneAnswer(response)}[/]");


public record FileData(string File, string Data);


/// <summary>Provides instructions and optional yes/no criteria for evaluating file content.</summary>
public partial class QuestionDefinition : INoulQuestionDefinition<string[]>
{
    public QuestionDefinition(string[] instructions)
    {
        Instructions = instructions;
        Yes = "Hello";
    }

    public string[] Instructions { get; set; }
    public string? Yes { get; }
    public string? No { get; } = null;
}


/// <summary>Provides instructions and ordered choices for classifying file content.</summary>
public class ChoiceQuestionDefinition : IChoiceQuestionDefinition<string>
{
    public ChoiceQuestionDefinition(string instructions, params ChoiceCriteria[] choices)
    {
        Instructions = instructions;
        Choices = choices;
    }

    public string Instructions { get; }
    public ChoiceCriteria[] Choices { get; }
}

/// <summary>Provides instructions and criteria for scoring the supplied file's information quality.</summary>
public class ScoreQuestionDefinition : IScoreQuestionDefinition<string>
{
    public ScoreQuestionDefinition(string instructions, params string[] criterias)
    {
        Instructions = instructions;
        Criterias = criterias;
    }

    public string Instructions { get; }
    public string[] Criterias { get; }
}

[JevQuery]
public partial class JevQuery
{
    public JevQuery(FileData state)
    {
        State = state;
        JevModel = JevModel.Latest;
        DangerZone = new QuestionDefinition(["Contains bla kind of text"])
        {
            //Yes = "The file contains filler or placeholder text such as 'bla'.",
            //No = "The file contains meaningful text without filler or placeholder content."
        };
        ChoiceQuestionDefinition = new ChoiceQuestionDefinition(
            "Classify the text in the supplied file.",
            new ChoiceCriteria("Placeholder", "The text is filler or placeholder content, such as 'bla bla bla'."),
            new ChoiceCriteria("Meaningful",
                "The text conveys meaningful information rather than placeholder content."));
        ScoreQuestionDefinition = new ScoreQuestionDefinition(
            "Score the information quality of the text in the supplied file.",
            "The text conveys meaningful information rather than filler or placeholder content.",
            "The text is clear and understandable.",
            "The text provides specific, useful details.");
    }

    public FileData State { get; private set; }
    public JevModel JevModel { get; private set; }

    public QuestionDefinition DangerZone { get; private set; }
    public ChoiceQuestionDefinition ChoiceQuestionDefinition { get; private set; }
    public ScoreQuestionDefinition ScoreQuestionDefinition { get; private set; }
}