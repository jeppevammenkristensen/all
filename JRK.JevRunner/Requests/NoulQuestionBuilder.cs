namespace JRK.JevRunner.Requests;

/// <summary>
/// Configures the name, instructions, and optional yes/no criteria of a noul question.
/// </summary>
public class NoulQuestionBuilder : IQuestionBuilder<NoulQuestion>
{
    /// <summary>
    /// Creates a builder with the supplied question name and initial instruction.
    /// </summary>
    public NoulQuestionBuilder(string name, JevMessage instruction)
    {
        Name = name;
        Instruction = instruction;
    }
    
    /// <summary>
    /// Sets the definitions for yes and no answers and returns this builder.
    /// </summary>
    public NoulQuestionBuilder AddYesNo(string yesDescription, string noDescription)
    {
        Criteria = new YesNo(yesDescription, noDescription);
        return this;
    }

    /// <summary>
    /// Gets or sets the instruction to include in the built question.
    /// </summary>
    public JevMessage Instruction { get; set; }

    /// <summary>
    /// Creates a noul question from the builder's current name, instruction, and criteria.
    /// </summary>
    public NoulQuestion Build()
    {
        return new NoulQuestion(Name, Instruction, Criteria);
    }

    /// <summary>
    /// Creates a noul question from the current configuration and returns it as a request question.
    /// </summary>
    Question IQuestionBuilder<NoulQuestion>.BuildQuestion()
    {
        return Build();
    }

    /// <summary>
    /// Gets or sets the optional definitions for yes and no answers.
    /// </summary>
    public YesNo? Criteria { get; set; }

    /// <summary>
    /// Gets the name identifying the built question in a request.
    /// </summary>
    public string Name { get; }
}
