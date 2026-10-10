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


[NoulQuestion]
public partial class Question
{
    public string[] Instructions { get; set; }
    public string Yes { get; set; }
    public string No { get; set; }
}


[JevQuery]
public partial class JevQuery
{
    public JevQuery(FileData state)
    {
        State = state;
        JevModel = JevModel.Latest;
        DangerZone = new Question()
        {
            Instructions = ["Contains bla kind of text"],
        };
    }

    public FileData State { get; private set; }    
    public JevModel JevModel { get; private set; }
    
    public Question DangerZone { get; private set; }
}


