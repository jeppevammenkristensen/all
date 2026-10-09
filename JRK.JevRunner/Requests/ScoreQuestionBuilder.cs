using System.Collections.Immutable;

namespace JRK.JevRunner.Requests;

/// <summary>
/// Configures the name, instruction, and criteria of a score question.
/// </summary>
public class ScoreQuestionBuilder : IQuestionBuilder<ScoreQuestion>
{
    /// <summary>
    /// Gets the name identifying the built question in a request.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the instruction to include in the built question.
    /// </summary>
    public JevMessage Instruction { get; }

    /// <summary>
    /// Creates a builder with the supplied question name and instruction and no criteria.
    /// </summary>
    public ScoreQuestionBuilder(string name, JevMessage instruction)
    {
        Name = name;
        Instruction = instruction;
    }

    /// <summary>
    /// Appends a scoring criterion and returns this builder for further configuration.
    /// </summary>
    public ScoreQuestionBuilder AddCriteria(string criteria)
    {
        Criterias = Criterias.Add(criteria);
        return this;
    }
    
    /// <summary>
    /// Creates a score question from the builder's current name, instruction, and criteria.
    /// </summary>
    public ScoreQuestion Build()
    {
        return new ScoreQuestion(Name, Instruction, [.. Criterias]);
    }

    /// <summary>
    /// Creates a score question from the current configuration and returns it as a request question.
    /// </summary>
    Question IQuestionBuilder<ScoreQuestion>.BuildQuestion()
    {
        return Build();
    }

    /// <summary>
    /// Gets the scoring criteria in the order they were added.
    /// </summary>
    public ImmutableArray<JevMessage> Criterias { get; private set; } = [];
}
