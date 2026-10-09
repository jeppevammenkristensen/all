using System.Collections.Immutable;

namespace JRK.JevRunner.Requests;

/// <summary>
/// Configures the name, instruction, and available choices of a choice question.
/// </summary>
public class ChoiceQuestionBuilder : IQuestionBuilder<ChoiceQuestion>
{
    /// <summary>
    /// Creates a builder with the supplied question name and instruction and no choices.
    /// </summary>
    public ChoiceQuestionBuilder(string name, JevMessage instruction)
    {
        Name = name;
        Instruction = instruction;
    }

    /// <summary>
    /// Appends a choice and its selection instruction, returning this builder for further configuration.
    /// </summary>
    public ChoiceQuestionBuilder AddChoice(string choice, string instruction)
    {
        Choices = Choices.Add(new ChoiceCriteria(choice, instruction));
        return this;
    }
    
    /// <summary>
    /// Gets or sets the instruction to include in the built question.
    /// </summary>
    private JevMessage Instruction { get; set; }

    /// <summary>
    /// Gets or sets the name identifying the built question in a request.
    /// </summary>
    private string Name { get; set; }
    
    /// <summary>
    /// Gets or sets the available choices and their selection instructions in insertion order.
    /// </summary>
    private ImmutableArray<ChoiceCriteria> Choices { get; set; } =  ImmutableArray<ChoiceCriteria>.Empty;

    /// <summary>
    /// Creates a choice question from the builder's current name, instruction, and choices.
    /// </summary>
    public ChoiceQuestion Build()
    {
        return new ChoiceQuestion(Name, Instruction, Choices);
    }

    /// <summary>
    /// Creates a choice question from the current configuration and returns it as a request question.
    /// </summary>
    Question IQuestionBuilder<ChoiceQuestion>.BuildQuestion()
    {
        return Build();
    }
}
