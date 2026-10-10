using Microsoft.CodeAnalysis;

namespace JRK.JevRunner.SourceGenerators;

/// <summary>
/// Couples a query property with its builder, typed response lookup, and configuration contract.
/// </summary>
internal sealed class QuestionMapping
{
    public QuestionMapping(IPropertySymbol property, INamedTypeSymbol @interface, INamedTypeSymbol builder,
        INamedTypeSymbol answer,
        string accessor, ConfigurationKind configuration)
    {
        Property = property;
        Interface = @interface;
        Builder = builder;
        Answer = answer;
        Accessor = accessor;
        Configuration = configuration;
    }

    public IPropertySymbol Property { get; }
    public INamedTypeSymbol Interface { get; }
    public INamedTypeSymbol Builder { get; }
    public INamedTypeSymbol Answer { get; }
    public string Accessor { get; }
    public ConfigurationKind Configuration { get; }
}
