using AutoSpectre;
using JRK.JevRunner;
using JRK.JevRunner.Requests;
using Playground;
using Spectre.Console;

var connector = new Connector().Prompt();
var jevRunner = connector.GetRunner();

var result = await jevRunner.Execute(new Request("The brown fox jumped over the lazy dog",JevModel.Latest).AddQuestion(new NoulQuestionBuilder("dog_was_jumped_over", "Did any animal jump over a lazy dog")));

AnsiConsole.MarkupLineInterpolated($"[green]{result.GetRequiredNoulAnswer("dog_was_jumped_over")}[/]");